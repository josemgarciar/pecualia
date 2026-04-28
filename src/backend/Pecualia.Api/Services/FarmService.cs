using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Farms;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

public interface IFarmService
{
    Task<IReadOnlyList<FarmListItemResponse>> GetAccessibleFarmsAsync(long userId, UserRole role, CancellationToken cancellationToken);

    Task<FarmListItemResponse> CreateFarmAsync(long userId, UserRole role, CreateFarmRequest request, CancellationToken cancellationToken);

    Task<FarmSummaryResponse> GetSummaryAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);

    Task<FarmDetailResponse> GetDetailAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);
}

public sealed class FarmService(PecualiaDbContext dbContext) : IFarmService
{
    public async Task<IReadOnlyList<FarmListItemResponse>> GetAccessibleFarmsAsync(long userId, UserRole role, CancellationToken cancellationToken)
    {
        var query = BuildAccessibleQuery(userId, role);

        var farms = await query
            .AsNoTracking()
            .Include(entity => entity.Farmer)
            .ThenInclude(entity => entity.User)
            .Include(entity => entity.Animals)
            .OrderBy(entity => entity.Name)
            .ToListAsync(cancellationToken);

        return farms.Select(Map).ToList();
    }

    public async Task<FarmListItemResponse> CreateFarmAsync(long userId, UserRole role, CreateFarmRequest request, CancellationToken cancellationToken)
    {
        if (await dbContext.Farms.AnyAsync(entity => entity.RegaCode == request.RegaCode.Trim().ToUpperInvariant(), cancellationToken))
        {
            throw new DomainException("Ya existe una explotación con ese código REGA.");
        }

        if (request.LivestockSpecies is not (LivestockSpecies.Ovine or LivestockSpecies.Caprine or LivestockSpecies.Porcine))
        {
            throw new DomainException("La especie indicada no está soportada en esta versión.");
        }

        if (role == UserRole.Manager)
        {
            var canManage = await dbContext.Farmers.AnyAsync(entity => entity.UserId == request.FarmerId && entity.ManagerId == userId, cancellationToken);
            if (!canManage)
            {
                throw new DomainException("No puedes crear explotaciones para ese ganadero.");
            }
        }
        else if (request.FarmerId != userId)
        {
            throw new DomainException("No puedes crear explotaciones para otro ganadero.");
        }

        var farm = new LivestockFarm
        {
            FarmerId = request.FarmerId,
            Name = request.Name.Trim(),
            RegaCode = request.RegaCode.Trim().ToUpperInvariant(),
            LivestockSpecies = request.LivestockSpecies,
            Regime = request.Regime,
            Town = Normalize(request.Town),
            Province = Normalize(request.Province),
            Address = Normalize(request.Address),
            ZipCode = Normalize(request.ZipCode),
            AuthorisedCapacity = request.LivestockSpecies == LivestockSpecies.Porcine ? request.AuthorisedCapacity : null,
            Responsible = Normalize(request.Responsible),
            ZootechnicClassification = Normalize(request.ZootechnicClassification),
            XCoordinate = request.XCoordinate,
            YCoordinate = request.YCoordinate,
            Status = FarmStatus.Active
        };

        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync(cancellationToken);

        var createdFarm = await dbContext.Farms
            .Include(entity => entity.Farmer)
            .ThenInclude(entity => entity.User)
            .Include(entity => entity.Animals)
            .SingleAsync(entity => entity.Id == farm.Id, cancellationToken);

        return Map(createdFarm);
    }

    public async Task<FarmSummaryResponse> GetSummaryAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        var farm = await BuildAccessibleQuery(userId, role)
            .AsNoTracking()
            .Include(entity => entity.Farmer)
            .ThenInclude(entity => entity.User)
            .Include(entity => entity.Animals)
            .SingleOrDefaultAsync(entity => entity.Id == farmId, cancellationToken);

        if (farm is null)
        {
            throw new DomainException("Explotación no encontrada.");
        }

        return new FarmSummaryResponse(
            farm.Id,
            farm.Name,
            farm.RegaCode,
            farm.LivestockSpecies.ToString(),
            farm.Status.ToString(),
            $"{farm.Farmer.User.Name} {farm.Farmer.User.Surname}".Trim(),
            farm.Animals.Count,
            farm.AuthorisedCapacity,
            farm.Regime?.ToString(),
            EmptyToNull(farm.Town),
            EmptyToNull(farm.Province),
            EmptyToNull(farm.Responsible));
    }

    public async Task<FarmDetailResponse> GetDetailAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        var farm = await BuildAccessibleQuery(userId, role)
            .AsNoTracking()
            .Include(entity => entity.Farmer)
            .ThenInclude(entity => entity.User)
            .Include(entity => entity.Animals)
            .SingleOrDefaultAsync(entity => entity.Id == farmId, cancellationToken);

        if (farm is null)
        {
            throw new DomainException("Explotación no encontrada.");
        }

        return new FarmDetailResponse(
            farm.Id,
            farm.FarmerId,
            farm.Name,
            farm.RegaCode,
            farm.LivestockSpecies.ToString(),
            farm.Status.ToString(),
            BuildFarmerName(farm.Farmer),
            farm.Animals.Count,
            farm.AuthorisedCapacity,
            farm.Regime?.ToString(),
            EmptyToNull(farm.LivestockType),
            EmptyToNull(farm.ProductionCapacity),
            EmptyToNull(farm.Town),
            EmptyToNull(farm.Province),
            EmptyToNull(farm.Address),
            EmptyToNull(farm.ZipCode),
            EmptyToNull(farm.Responsible),
            EmptyToNull(farm.ZootechnicClassification),
            farm.Spindle,
            farm.XCoordinate,
            farm.YCoordinate);
    }

    private IQueryable<LivestockFarm> BuildAccessibleQuery(long userId, UserRole role)
    {
        return role == UserRole.Manager
            ? dbContext.Farms.Where(entity => entity.Farmer.ManagerId == userId)
            : dbContext.Farms.Where(entity => entity.FarmerId == userId);
    }

    private static FarmListItemResponse Map(LivestockFarm farm)
    {
        return new FarmListItemResponse(
            farm.Id,
            farm.FarmerId,
            farm.Name,
            farm.RegaCode,
            farm.LivestockSpecies.ToString(),
            farm.Status.ToString(),
            EmptyToNull(farm.Town),
            EmptyToNull(farm.Province),
            BuildFarmerName(farm.Farmer),
            farm.Animals.Count,
            farm.AuthorisedCapacity);
    }

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string BuildFarmerName(Farmer farmer)
    {
        return farmer.PersonType == PersonType.Company
            ? farmer.CompanyName?.Trim() ?? farmer.LegalRepresentative?.Trim() ?? farmer.User.Name
            : $"{farmer.User.Name} {farmer.User.Surname} {farmer.SecondSurname}".Replace("  ", " ").Trim();
    }
}
