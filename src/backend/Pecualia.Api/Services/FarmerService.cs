using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Farmers;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

public interface IFarmerService
{
    Task<IReadOnlyList<FarmerListItemResponse>> GetManagedFarmersAsync(long managerUserId, string? search, string? province, string? status, CancellationToken cancellationToken);

    Task<FarmerDetailResponse> GetManagedFarmerDetailAsync(long managerUserId, long farmerUserId, CancellationToken cancellationToken);

    Task<FarmerListItemResponse> CreateManagedFarmerAsync(long managerUserId, CreateFarmerRequest request, CancellationToken cancellationToken);

    Task<FarmerListItemResponse> UpdateManagedFarmerAsync(long managerUserId, long farmerUserId, UpdateFarmerRequest request, CancellationToken cancellationToken);

    Task<bool> ResendActivationAsync(long managerUserId, long farmerUserId, CancellationToken cancellationToken);

    Task UnlinkManagedFarmerAsync(long managerUserId, long farmerUserId, CancellationToken cancellationToken);
}

public sealed class FarmerService(PecualiaDbContext dbContext, IAuthService authService, IClock clock, IFarmCensusProjectionService censusProjectionService) : IFarmerService
{
    public async Task<IReadOnlyList<FarmerListItemResponse>> GetManagedFarmersAsync(long managerUserId, string? search, string? province, string? status, CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim().ToLowerInvariant();
        var normalizedProvince = string.IsNullOrWhiteSpace(province) ? null : province.Trim();
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim();

        var query = dbContext.Farmers
            .AsNoTracking()
            .Where(entity => entity.ManagerId == managerUserId)
            .Include(entity => entity.User)
            .Include(entity => entity.Farms)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(entity =>
                (entity.User.Email != null && entity.User.Email.ToLower().Contains(normalizedSearch)) ||
                entity.NifCif.ToLower().Contains(normalizedSearch) ||
                (entity.Town != null && entity.Town.ToLower().Contains(normalizedSearch)) ||
                (entity.CompanyName != null && entity.CompanyName.ToLower().Contains(normalizedSearch)) ||
                entity.User.Name.ToLower().Contains(normalizedSearch) ||
                entity.User.Surname.ToLower().Contains(normalizedSearch) ||
                (entity.SecondSurname != null && entity.SecondSurname.ToLower().Contains(normalizedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(normalizedProvince))
        {
            query = query.Where(entity => entity.Province == normalizedProvince);
        }

        if (!string.IsNullOrWhiteSpace(normalizedStatus) &&
            Enum.TryParse<FarmerStatus>(normalizedStatus, true, out var parsedStatus))
        {
            query = query.Where(entity => entity.Status == parsedStatus);
        }

        var farmers = await query
            .OrderBy(entity => entity.CompanyName ?? entity.User.Surname)
            .ThenBy(entity => entity.User.Name)
            .ToListAsync(cancellationToken);

        return farmers.Select(Map).ToList();
    }

    public async Task<FarmerDetailResponse> GetManagedFarmerDetailAsync(long managerUserId, long farmerUserId, CancellationToken cancellationToken)
    {
        var farmer = await dbContext.Farmers
            .AsNoTracking()
            .AsSplitQuery()
            .Include(entity => entity.User)
            .Include(entity => entity.Farms)
            .SingleOrDefaultAsync(entity => entity.UserId == farmerUserId && entity.ManagerId == managerUserId, cancellationToken);

        return farmer is null
            ? throw new DomainException("Ganadero no encontrado.")
            : await MapDetailAsync(farmer, cancellationToken);
    }

    public async Task<FarmerListItemResponse> CreateManagedFarmerAsync(long managerUserId, CreateFarmerRequest request, CancellationToken cancellationToken)
    {
        ValidateFarmerRequest(request.PersonType, request.Name, request.FirstSurname, request.CompanyName, request.LegalRepresentative, request.NifCif, request.PhoneNumber, request.Town, request.Province);
        await EnsureManagedFarmerPlanCapacityAsync(managerUserId, cancellationToken);

        var email = NormalizeOptionalEmail(request.Email);
        if (email is not null && await dbContext.Users.AnyAsync(entity => entity.Email == email, cancellationToken))
        {
            throw new DomainException("Ya existe una cuenta con ese correo electrónico.");
        }

        if (await dbContext.Farmers.AnyAsync(entity => entity.NifCif == request.NifCif.Trim().ToUpperInvariant(), cancellationToken))
        {
            throw new DomainException("Ya existe un ganadero con ese NIF/CIF.");
        }

        var user = new AppUser
        {
            Email = email,
            Name = request.PersonType == PersonType.Company
                ? request.LegalRepresentative!.Trim()
                : request.Name!.Trim(),
            Surname = request.PersonType == PersonType.Company
                ? string.Empty
                : request.FirstSurname!.Trim(),
            Role = UserRole.Farmer,
            IsActive = false,
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow
        };

        var initialStatus = email is null
            ? FarmerStatus.Inactive
            : FarmerStatus.PendingActivation;

        var farmer = new Farmer
        {
            User = user,
            ManagerId = managerUserId,
            NifCif = request.NifCif.Trim().ToUpperInvariant(),
            SecondSurname = Normalize(request.SecondSurname),
            CompanyName = Normalize(request.CompanyName),
            LegalRepresentative = Normalize(request.LegalRepresentative),
            PhoneNumber = Normalize(request.PhoneNumber),
            Residence = Normalize(request.Residence),
            Town = Normalize(request.Town),
            Province = Normalize(request.Province),
            ZipCode = Normalize(request.ZipCode),
            PersonType = request.PersonType,
            BirthDate = request.BirthDate,
            Status = initialStatus
        };

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (email is not null)
        {
            await ((AuthService)authService).CreateActivationAsync(user, managerUserId, cancellationToken);
        }

        return Map(farmer);
    }

    public async Task<FarmerListItemResponse> UpdateManagedFarmerAsync(long managerUserId, long farmerUserId, UpdateFarmerRequest request, CancellationToken cancellationToken)
    {
        ValidateFarmerRequest(request.PersonType, request.Name, request.FirstSurname, request.CompanyName, request.LegalRepresentative, request.NifCif, request.PhoneNumber, request.Town, request.Province);

        var farmer = await LoadManagedFarmerAsync(managerUserId, farmerUserId, cancellationToken);
        var previousEmail = NormalizeOptionalEmail(farmer.User.Email);
        var normalizedEmail = NormalizeOptionalEmail(request.Email);
        var normalizedNif = request.NifCif.Trim().ToUpperInvariant();

        if (normalizedEmail is not null &&
            await dbContext.Users.AnyAsync(entity => entity.Email == normalizedEmail && entity.Id != farmerUserId, cancellationToken))
        {
            throw new DomainException("Ya existe una cuenta con ese correo electrónico.");
        }

        if (await dbContext.Farmers.AnyAsync(entity => entity.NifCif == normalizedNif && entity.UserId != farmerUserId, cancellationToken))
        {
            throw new DomainException("Ya existe un ganadero con ese NIF/CIF.");
        }

        farmer.User.Email = normalizedEmail;
        farmer.User.Name = request.PersonType == PersonType.Company
            ? request.LegalRepresentative!.Trim()
            : request.Name!.Trim();
        farmer.User.Surname = request.PersonType == PersonType.Company
            ? string.Empty
            : request.FirstSurname!.Trim();
        farmer.User.UpdatedAt = clock.UtcNow;
        farmer.NifCif = normalizedNif;
        farmer.SecondSurname = Normalize(request.SecondSurname);
        farmer.CompanyName = Normalize(request.CompanyName);
        farmer.LegalRepresentative = Normalize(request.LegalRepresentative);
        farmer.PhoneNumber = Normalize(request.PhoneNumber);
        farmer.Residence = Normalize(request.Residence);
        farmer.Town = Normalize(request.Town);
        farmer.Province = Normalize(request.Province);
        farmer.ZipCode = Normalize(request.ZipCode);
        farmer.PersonType = request.PersonType;
        farmer.BirthDate = request.BirthDate;
        farmer.Status = ResolveStoredFarmerStatus(farmer.User.IsActive, normalizedEmail);

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!farmer.User.IsActive &&
            normalizedEmail is not null &&
            !string.Equals(previousEmail, normalizedEmail, StringComparison.Ordinal))
        {
            await ((AuthService)authService).CreateActivationAsync(farmer.User, managerUserId, cancellationToken);
        }

        return Map(farmer);
    }

    public async Task<bool> ResendActivationAsync(long managerUserId, long farmerUserId, CancellationToken cancellationToken)
    {
        var farmer = await LoadManagedFarmerAsync(managerUserId, farmerUserId, cancellationToken);
        if (farmer.User.IsActive || string.IsNullOrWhiteSpace(farmer.User.Email))
        {
            return false;
        }

        await ((AuthService)authService).CreateActivationAsync(farmer.User, managerUserId, cancellationToken);
        return true;
    }

    public async Task UnlinkManagedFarmerAsync(long managerUserId, long farmerUserId, CancellationToken cancellationToken)
    {
        var farmer = await LoadManagedFarmerAsync(managerUserId, farmerUserId, cancellationToken);
        farmer.ManagerId = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Farmer> LoadManagedFarmerAsync(long managerUserId, long farmerUserId, CancellationToken cancellationToken)
    {
        var farmer = await dbContext.Farmers
            .AsSplitQuery()
            .Include(entity => entity.User)
            .Include(entity => entity.Farms)
            .SingleOrDefaultAsync(entity => entity.UserId == farmerUserId && entity.ManagerId == managerUserId, cancellationToken);

        return farmer ?? throw new DomainException("Ganadero no encontrado.");
    }

    private async Task EnsureManagedFarmerPlanCapacityAsync(long managerUserId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var subscription = await dbContext.Subscriptions
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.UserId == managerUserId, cancellationToken);

        var planType = SubscriptionPlanSupport.ResolveEffectivePlanType(subscription, today);
        var farmerLimit = SubscriptionPlanSupport.GetManagedFarmerLimit(planType);
        if (farmerLimit is null)
        {
            return;
        }

        var currentFarmerCount = await dbContext.Farmers.CountAsync(entity => entity.ManagerId == managerUserId, cancellationToken);
        if (currentFarmerCount >= farmerLimit.Value)
        {
            throw new DomainException(SubscriptionPlanSupport.BuildManagedFarmerLimitError(planType, farmerLimit.Value));
        }
    }

    private static FarmerListItemResponse Map(Farmer farmer)
    {
        var displayName = BuildDisplayName(farmer);
        var fullName = farmer.PersonType == PersonType.Individual
            ? $"{farmer.User.Name} {farmer.User.Surname} {farmer.SecondSurname}".Replace("  ", " ").Trim()
            : null;

        return new FarmerListItemResponse(
            farmer.UserId,
            displayName,
            fullName,
            EmptyToNull(farmer.User.Email),
            farmer.NifCif,
            EmptyToNull(farmer.PhoneNumber),
            EmptyToNull(farmer.Town),
            EmptyToNull(farmer.Province),
            farmer.PersonType.ToString(),
            MapStatusForResponse(farmer),
            !farmer.User.IsActive && !string.IsNullOrWhiteSpace(farmer.User.Email),
            farmer.Farms.Count);
    }

    private async Task<FarmerDetailResponse> MapDetailAsync(Farmer farmer, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var farms = new List<FarmerFarmItemResponse>(farmer.Farms.Count);

        foreach (var farm in farmer.Farms.OrderBy(entity => entity.Name))
        {
            var snapshot = await censusProjectionService.BuildSnapshotAsync(farm, today, cancellationToken);
            farms.Add(new FarmerFarmItemResponse(
                farm.Id,
                farm.Name,
                farm.RegaCode,
                farm.LivestockSpecies.ToString(),
                snapshot.Total));
        }

        return new FarmerDetailResponse(
            farmer.UserId,
            farmer.PersonType.ToString(),
            BuildDisplayName(farmer),
            farmer.PersonType == PersonType.Individual ? farmer.User.Name : null,
            farmer.PersonType == PersonType.Individual ? EmptyToNull(farmer.User.Surname) : null,
            farmer.PersonType == PersonType.Individual ? EmptyToNull(farmer.SecondSurname) : null,
            farmer.PersonType == PersonType.Individual ? farmer.BirthDate : null,
            farmer.PersonType == PersonType.Company ? EmptyToNull(farmer.CompanyName) : null,
            farmer.PersonType == PersonType.Company ? EmptyToNull(farmer.LegalRepresentative) : null,
            EmptyToNull(farmer.User.Email),
            farmer.NifCif,
            farmer.PhoneNumber ?? string.Empty,
            EmptyToNull(farmer.Residence),
            farmer.Town ?? string.Empty,
            farmer.Province ?? string.Empty,
            EmptyToNull(farmer.ZipCode),
            MapStatusForResponse(farmer),
            !farmer.User.IsActive && !string.IsNullOrWhiteSpace(farmer.User.Email),
            farms);
    }

    private static void ValidateFarmerRequest(
        PersonType personType,
        string? name,
        string? firstSurname,
        string? companyName,
        string? legalRepresentative,
        string nifCif,
        string phoneNumber,
        string town,
        string province)
    {
        if (personType == PersonType.Individual)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(firstSurname))
            {
                throw new DomainException("Nombre y primer apellido son obligatorios para persona física.");
            }
        }
        else if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(legalRepresentative))
        {
            throw new DomainException("Razón social y representante legal son obligatorios para persona jurídica.");
        }

        if (string.IsNullOrWhiteSpace(nifCif) ||
            string.IsNullOrWhiteSpace(phoneNumber) ||
            string.IsNullOrWhiteSpace(town) ||
            string.IsNullOrWhiteSpace(province))
        {
            throw new DomainException("NIF/CIF, teléfono, localidad y provincia son obligatorios.");
        }

        if (!DomainValidators.IsValidTaxIdentifier(personType, nifCif))
        {
            throw new DomainException(personType == PersonType.Company
                ? "El NIF de la persona jurídica no es válido."
                : "El DNI/NIF de la persona física no es válido.");
        }
    }

    private static string? NormalizeOptionalEmail(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }

    private static FarmerStatus ResolveStoredFarmerStatus(bool isActive, string? email)
    {
        if (isActive)
        {
            return FarmerStatus.Active;
        }

        return email is null
            ? FarmerStatus.Inactive
            : FarmerStatus.PendingActivation;
    }

    private static string MapStatusForResponse(Farmer farmer)
    {
        return string.IsNullOrWhiteSpace(farmer.User.Email)
            ? string.Empty
            : farmer.Status.ToString();
    }

    private static string BuildDisplayName(Farmer farmer)
    {
        return farmer.PersonType == PersonType.Company
            ? farmer.CompanyName?.Trim() ?? farmer.LegalRepresentative?.Trim() ?? farmer.User.Name
            : $"{farmer.User.Name} {farmer.User.Surname} {farmer.SecondSurname}".Replace("  ", " ").Trim();
    }

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
