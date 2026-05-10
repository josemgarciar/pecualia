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

    Task<FarmDetailResponse> UpdateFarmAsync(long userId, UserRole role, long farmId, UpdateFarmRequest request, CancellationToken cancellationToken);

    Task<FarmSummaryResponse> GetSummaryAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);

    Task<FarmDetailResponse> GetDetailAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);
}

public sealed class FarmService(PecualiaDbContext dbContext, IClock clock) : IFarmService
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
        var normalizedRegaCode = DomainValidators.NormalizeRegaCode(request.RegaCode);
        if (string.IsNullOrWhiteSpace(normalizedRegaCode))
        {
            throw new DomainException("El código REGA es obligatorio.");
        }

        if (!DomainValidators.IsValidRegaCode(normalizedRegaCode))
        {
            throw new DomainException("El código REGA no es válido. Debe seguir el formato ES seguido de 12 dígitos.");
        }

        if (await dbContext.Farms.AnyAsync(entity => entity.RegaCode == normalizedRegaCode, cancellationToken))
        {
            throw new DomainException("Ya existe una explotación con ese código REGA.");
        }

        if (request.LivestockSpecies is not (LivestockSpecies.Ovine or LivestockSpecies.Caprine or LivestockSpecies.Porcine))
        {
            throw new DomainException("La especie indicada no está soportada en esta versión.");
        }

        if (request.LivestockSpecies == LivestockSpecies.Porcine && string.IsNullOrWhiteSpace(request.PorcineRegistryNumber))
        {
            throw new DomainException("El número de registro porcino es obligatorio para explotaciones porcinas.");
        }

        if (request.ProductionCapacity < 0)
        {
            throw new DomainException("La capacidad productiva debe ser un número entero igual o mayor que cero.");
        }

        if (request.Spindle is <= 0)
        {
            throw new DomainException("El huso debe ser un número entero positivo.");
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

        await EnsureFarmPlanCapacityAsync(userId, role, cancellationToken);

        var farm = new LivestockFarm
        {
            FarmerId = request.FarmerId,
            Name = request.Name.Trim(),
            RegaCode = normalizedRegaCode,
            LivestockSpecies = request.LivestockSpecies,
            Regime = request.Regime,
            Town = Normalize(request.Town),
            Province = Normalize(request.Province),
            Address = Normalize(request.Address),
            ZipCode = Normalize(request.ZipCode),
            AuthorisedCapacity = request.LivestockSpecies == LivestockSpecies.Porcine ? request.AuthorisedCapacity : null,
            PorcineRegistryNumber = request.LivestockSpecies == LivestockSpecies.Porcine ? Normalize(request.PorcineRegistryNumber).ToUpperInvariant() : string.Empty,
            LivestockType = Normalize(request.LivestockType),
            ProductionCapacity = request.ProductionCapacity,
            Responsible = Normalize(request.Responsible),
            ZootechnicClassification = Normalize(request.ZootechnicClassification),
            Spindle = request.Spindle,
            XCoordinate = request.XCoordinate,
            YCoordinate = request.YCoordinate
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

    public async Task<FarmDetailResponse> UpdateFarmAsync(long userId, UserRole role, long farmId, UpdateFarmRequest request, CancellationToken cancellationToken)
    {
        var farm = await BuildAccessibleQuery(userId, role)
            .SingleOrDefaultAsync(entity => entity.Id == farmId, cancellationToken);

        if (farm is null)
        {
            throw new DomainException("Explotación no encontrada.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("El nombre de la explotación es obligatorio.");
        }

        var normalizedRegaCode = DomainValidators.NormalizeRegaCode(request.RegaCode);
        if (string.IsNullOrWhiteSpace(normalizedRegaCode))
        {
            throw new DomainException("El código REGA es obligatorio.");
        }

        if (!DomainValidators.IsValidRegaCode(normalizedRegaCode))
        {
            throw new DomainException("El código REGA no es válido. Debe seguir el formato ES seguido de 12 dígitos.");
        }

        if (await dbContext.Farms.AnyAsync(entity => entity.Id != farmId && entity.RegaCode == normalizedRegaCode, cancellationToken))
        {
            throw new DomainException("Ya existe una explotación con ese código REGA.");
        }

        if (request.Regime is not (FarmRegime.Extensive or FarmRegime.SemiExtensive or FarmRegime.Intensive))
        {
            throw new DomainException("El régimen indicado no es válido.");
        }

        if (farm.LivestockSpecies == LivestockSpecies.Porcine && string.IsNullOrWhiteSpace(request.PorcineRegistryNumber))
        {
            throw new DomainException("El número de registro porcino es obligatorio para explotaciones porcinas.");
        }

        if (request.ProductionCapacity < 0)
        {
            throw new DomainException("La capacidad productiva debe ser un número entero igual o mayor que cero.");
        }

        if (request.Spindle is <= 0)
        {
            throw new DomainException("El huso debe ser un número entero positivo.");
        }

        if (string.IsNullOrWhiteSpace(request.Town))
        {
            throw new DomainException("La localidad es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(request.Province))
        {
            throw new DomainException("La provincia es obligatoria.");
        }

        farm.Name = request.Name.Trim();
        farm.RegaCode = normalizedRegaCode;
        farm.Regime = request.Regime;
        farm.Town = Normalize(request.Town);
        farm.Province = Normalize(request.Province);
        farm.Address = Normalize(request.Address);
        farm.ZipCode = Normalize(request.ZipCode);
        farm.AuthorisedCapacity = farm.LivestockSpecies == LivestockSpecies.Porcine ? request.AuthorisedCapacity : null;
        farm.PorcineRegistryNumber = farm.LivestockSpecies == LivestockSpecies.Porcine ? Normalize(request.PorcineRegistryNumber).ToUpperInvariant() : string.Empty;
        farm.LivestockType = Normalize(request.LivestockType);
        farm.ProductionCapacity = request.ProductionCapacity;
        farm.Responsible = Normalize(request.Responsible);
        farm.ZootechnicClassification = Normalize(request.ZootechnicClassification);
        farm.Spindle = request.Spindle;
        farm.XCoordinate = request.XCoordinate;
        farm.YCoordinate = request.YCoordinate;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetDetailAsync(userId, role, farmId, cancellationToken);
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
            $"{farm.Farmer.User.Name} {farm.Farmer.User.Surname}".Trim(),
            farm.Animals.Count,
            farm.AuthorisedCapacity,
            EmptyToNull(farm.PorcineRegistryNumber),
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
            BuildFarmerName(farm.Farmer),
            farm.Animals.Count,
            farm.AuthorisedCapacity,
            EmptyToNull(farm.PorcineRegistryNumber),
            farm.Regime?.ToString(),
            EmptyToNull(farm.LivestockType),
            farm.ProductionCapacity,
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

    private async Task EnsureFarmPlanCapacityAsync(long userId, UserRole role, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var subscription = await dbContext.Subscriptions
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.UserId == userId, cancellationToken);

        var planType = SubscriptionPlanSupport.ResolveEffectivePlanType(subscription, today);
        var farmLimit = SubscriptionPlanSupport.GetFarmLimit(role, planType);
        if (farmLimit is null)
        {
            return;
        }

        var currentFarmCount = role == UserRole.Manager
            ? await dbContext.Farms.CountAsync(entity => entity.Farmer.ManagerId == userId, cancellationToken)
            : await dbContext.Farms.CountAsync(entity => entity.FarmerId == userId, cancellationToken);

        if (currentFarmCount >= farmLimit.Value)
        {
            throw new DomainException(SubscriptionPlanSupport.BuildFarmLimitError(role, planType, farmLimit.Value));
        }
    }

    private static FarmListItemResponse Map(LivestockFarm farm)
    {
        return new FarmListItemResponse(
            farm.Id,
            farm.FarmerId,
            farm.Name,
            farm.RegaCode,
            farm.LivestockSpecies.ToString(),
            EmptyToNull(farm.Town),
            EmptyToNull(farm.Province),
            BuildFarmerName(farm.Farmer),
            farm.Animals.Count,
            farm.AuthorisedCapacity,
            EmptyToNull(farm.PorcineRegistryNumber));
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
