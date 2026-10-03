using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Dashboard;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

public interface IPendingTaskQueryService
{
    Task<IReadOnlyList<DashboardTaskResponse>> GetPendingTasksAsync(
        long userId,
        UserRole role,
        DateOnly today,
        DateTime now,
        CancellationToken cancellationToken);
}

public sealed class PendingTaskQueryService(PecualiaDbContext dbContext) : IPendingTaskQueryService
{
    public async Task<IReadOnlyList<DashboardTaskResponse>> GetPendingTasksAsync(
        long userId,
        UserRole role,
        DateOnly today,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var accessibleFarmIds = await GetAccessibleFarmIdsAsync(userId, role, cancellationToken);
        if (accessibleFarmIds.Count == 0)
        {
            return [];
        }

        var farms = await dbContext.Farms
            .AsNoTracking()
            .Where(entity => accessibleFarmIds.Contains(entity.Id))
            .ToListAsync(cancellationToken);

        var animals = await dbContext.Animals
            .AsNoTracking()
            .Where(entity => accessibleFarmIds.Contains(entity.LivestockFarmId))
            .ToListAsync(cancellationToken);

        var animalIds = animals.Select(entity => entity.Id).ToList();

        var births = await dbContext.AnimalBirths
            .AsNoTracking()
            .Include(entity => entity.PorcineTransitionDecision)
            .Where(entity => accessibleFarmIds.Contains(entity.LivestockFarmId))
            .ToListAsync(cancellationToken);

        var vaccinations = animalIds.Count == 0
            ? []
            : await dbContext.Vaccinations
                .AsNoTracking()
                .Where(entity => animalIds.Contains(entity.AnimalId))
                .ToListAsync(cancellationToken);

        var inspections = await dbContext.Inspections
            .AsNoTracking()
            .Include(entity => entity.LivestockFarm)
            .Where(entity => accessibleFarmIds.Contains(entity.LivestockFarmId))
            .ToListAsync(cancellationToken);

        var pendingMovementConfirmations = await dbContext.MovementCertificates
            .AsNoTracking()
            .Where(entity =>
                entity.DestinationLivestockId != null &&
                accessibleFarmIds.Contains(entity.DestinationLivestockId.Value) &&
                entity.Status == MovementStatus.Pending &&
                entity.ArrivalDate != null &&
                entity.ArrivalDate.Value <= now &&
                entity.ArrivalDate.Value.AddDays(DashboardService.MovementConfirmationGraceDays) >= now)
            .ToListAsync(cancellationToken);

        var farmNamesById = farms.ToDictionary(entity => entity.Id, entity => entity.Name);
        var farmSpeciesById = farms.ToDictionary(entity => entity.Id, entity => entity.LivestockSpecies);
        var animalFarmNames = animals.ToDictionary(
            entity => entity.Id,
            entity => farmNamesById.GetValueOrDefault(entity.LivestockFarmId, "Explotación"));
        var pendingPorcineTransitionBirths = births
            .Where(entity =>
                entity.PorcineTransitionDecision is null &&
                farmSpeciesById.GetValueOrDefault(entity.LivestockFarmId) == LivestockSpecies.Porcine &&
                PorcineTransitionSupport.GetDecisionDate(entity.BirthDate) <= today)
            .ToList();
        var pendingBirthIds = pendingPorcineTransitionBirths.Select(entity => entity.Id).ToArray();
        var consumedPendingBirthCounts = pendingBirthIds.Length == 0
            ? new Dictionary<long, int>()
            : await dbContext.Animals
                .AsNoTracking()
                .Where(entity =>
                    entity.SourceBirthId != null &&
                    pendingBirthIds.Contains(entity.SourceBirthId.Value) &&
                    entity.RegistrationDate != null &&
                    entity.RegistrationDate <= today)
                .GroupBy(entity => entity.SourceBirthId!.Value)
                .Select(entity => new { BirthId = entity.Key, Count = entity.Count() })
                .ToDictionaryAsync(entity => entity.BirthId, entity => entity.Count, cancellationToken);

        return DashboardService.BuildPendingTasks(
            vaccinations,
            inspections,
            pendingMovementConfirmations,
            pendingPorcineTransitionBirths,
            consumedPendingBirthCounts,
            animalFarmNames,
            farmNamesById,
            today);
    }

    private async Task<List<long>> GetAccessibleFarmIdsAsync(long userId, UserRole role, CancellationToken cancellationToken)
    {
        if (role == UserRole.Manager)
        {
            return await dbContext.Farms
                .AsNoTracking()
                .Where(entity => entity.Farmer.ManagerId == userId)
                .Select(entity => entity.Id)
                .ToListAsync(cancellationToken);
        }

        return await dbContext.Farms
            .AsNoTracking()
            .Where(entity => entity.FarmerId == userId)
            .Select(entity => entity.Id)
            .ToListAsync(cancellationToken);
    }
}
