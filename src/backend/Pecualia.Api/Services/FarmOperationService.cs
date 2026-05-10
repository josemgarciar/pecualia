using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

public interface IFarmOperationService
{
    Task<IReadOnlyList<FarmBirthResponse>> GetBirthsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);

    Task<FarmBirthResponse> CreateBirthAsync(long userId, UserRole role, long farmId, CreateFarmBirthRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<FarmDeathResponse>> GetDeathsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);

    Task<FarmDeathResponse> CreateDeathAsync(long userId, UserRole role, long farmId, CreateFarmDeathRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<FarmVaccinationResponse>> GetVaccinationsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);

    Task<FarmVaccinationResponse> CreateVaccinationAsync(long userId, UserRole role, long farmId, CreateFarmVaccinationRequest request, CancellationToken cancellationToken);

    Task<FarmVaccinationResponse> UpdateVaccinationAsync(long userId, UserRole role, long farmId, long vaccinationId, UpdateFarmVaccinationRequest request, CancellationToken cancellationToken);

    Task DeleteVaccinationAsync(long userId, UserRole role, long farmId, long vaccinationId, CancellationToken cancellationToken);

    Task<FarmCensusResponse> GetCensusAsync(long userId, UserRole role, long farmId, int? year, CancellationToken cancellationToken);

    Task<FarmCensusResponse> UpdateCensusAsync(long userId, UserRole role, long farmId, int year, UpdateFarmCensusRequest request, CancellationToken cancellationToken);

    Task<FarmBalanceResponse> GetBalanceAsync(long userId, UserRole role, long farmId, int? year, CancellationToken cancellationToken);

    Task<IReadOnlyList<FarmIncidentResponse>> GetIncidentsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);

    Task<FarmIncidentResponse> CreateIncidentAsync(long userId, UserRole role, long farmId, CreateFarmIncidentRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<FarmInspectionResponse>> GetInspectionsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);

    Task<FarmInspectionResponse> CreateInspectionAsync(long userId, UserRole role, long farmId, CreateFarmInspectionRequest request, CancellationToken cancellationToken);
}

public sealed class FarmOperationService(PecualiaDbContext dbContext, IClock clock) : IFarmOperationService
{
    private const string BirthBalanceCause = "Nacimiento";

    public async Task<IReadOnlyList<FarmBirthResponse>> GetBirthsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);

        var births = await dbContext.AnimalBirths
            .AsNoTracking()
            .Where(entity => entity.LivestockFarmId == farmId)
            .OrderByDescending(entity => entity.BirthDate)
            .ThenByDescending(entity => entity.Id)
            .ToListAsync(cancellationToken);

        return births.Select(MapBirth).ToList();
    }

    public async Task<FarmBirthResponse> CreateBirthAsync(long userId, UserRole role, long farmId, CreateFarmBirthRequest request, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        EnsureOvineOrCaprineFarm(farm);

        if (request.OffspringNumber <= 0)
        {
            throw new DomainException("El número de crías debe ser mayor que cero.");
        }

        if (request.BirthWeight is < 0)
        {
            throw new DomainException("El peso de nacimiento no puede ser negativo.");
        }

        var birth = new AnimalBirth
        {
            LivestockFarmId = farm.Id,
            BirthDate = request.BirthDate,
            BirthWeight = request.BirthWeight,
            Observations = NormalizeNullable(request.Observations),
            OffspringNumber = request.OffspringNumber
        };

        dbContext.AnimalBirths.Add(birth);
        AddBalanceEvent(farm.Id, request.BirthDate, BirthBalanceCause, request.OffspringNumber);

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapBirth(birth);
    }

    public async Task<IReadOnlyList<FarmDeathResponse>> GetDeathsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);

        var deaths = await dbContext.Animals
            .AsNoTracking()
            .Where(entity => entity.LivestockFarmId == farmId && entity.DischargeCause == AnimalDischargeCause.Muerte && entity.DischargeDate != null)
            .OrderByDescending(entity => entity.DischargeDate)
            .ThenBy(entity => entity.Identification)
            .ToListAsync(cancellationToken);

        return deaths.Select(MapDeath).ToList();
    }

    public async Task<FarmDeathResponse> CreateDeathAsync(long userId, UserRole role, long farmId, CreateFarmDeathRequest request, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        var identification = NormalizeIdentifier(farm.LivestockSpecies, request.Identification, "El crotal o lote no es válido.");
        var animal = await dbContext.Animals
            .SingleOrDefaultAsync(entity => entity.LivestockFarmId == farm.Id && entity.Identification == identification, cancellationToken);

        if (animal is null)
        {
            throw new DomainException("No existe un animal activo con ese crotal en la explotación.");
        }

        if (animal.DischargeDate is not null)
        {
            throw new DomainException("El animal ya está dado de baja.");
        }

        var destinationCode = NormalizeDeathDestinationCode(farm.LivestockSpecies, request.DestinationCode);

        animal.DischargeDate = request.DischargeDate;
        animal.DischargeCause = AnimalDischargeCause.Muerte;
        animal.DestinationCode = destinationCode;

        AddBalanceEvent(farm.Id, request.DischargeDate, AnimalDischargeCause.Muerte.ToString(), 1, destinationCode: animal.DestinationCode);

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapDeath(animal);
    }

    public async Task<IReadOnlyList<FarmVaccinationResponse>> GetVaccinationsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);

        var vaccinations = await dbContext.Vaccinations
            .AsNoTracking()
            .Include(entity => entity.Animal)
            .Where(entity => entity.Animal.LivestockFarmId == farmId)
            .OrderByDescending(entity => entity.VaccinationDate)
            .ThenByDescending(entity => entity.Id)
            .ToListAsync(cancellationToken);

        return vaccinations.Select(MapVaccination).ToList();
    }

    public async Task<FarmVaccinationResponse> CreateVaccinationAsync(long userId, UserRole role, long farmId, CreateFarmVaccinationRequest request, CancellationToken cancellationToken)
    {
        await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);

        var animal = await LoadFarmAnimalAsync(farmId, request.AnimalIdentification, cancellationToken);
        var vaccinationType = NormalizeNullable(request.VaccinationType);
        var observations = NormalizeNullable(request.Observations);

        if (vaccinationType is null)
        {
            throw new DomainException("El tipo de vacunación es obligatorio.");
        }

        if (request.NextDose is not null && request.NextDose < request.VaccinationDate)
        {
            throw new DomainException("La próxima dosis no puede ser anterior a la fecha de vacunación.");
        }

        var vaccination = new Vaccination
        {
            AnimalId = animal.Id,
            VaccinationDate = request.VaccinationDate,
            NextDose = request.NextDose,
            VaccinationType = vaccinationType,
            Observations = observations
        };

        dbContext.Vaccinations.Add(vaccination);
        await dbContext.SaveChangesAsync(cancellationToken);

        vaccination.Animal = animal;
        return MapVaccination(vaccination);
    }

    public async Task<FarmVaccinationResponse> UpdateVaccinationAsync(long userId, UserRole role, long farmId, long vaccinationId, UpdateFarmVaccinationRequest request, CancellationToken cancellationToken)
    {
        await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);

        var vaccination = await dbContext.Vaccinations
            .Include(entity => entity.Animal)
            .SingleOrDefaultAsync(entity => entity.Id == vaccinationId && entity.Animal.LivestockFarmId == farmId, cancellationToken);

        if (vaccination is null)
        {
            throw new DomainException("Vacunación no encontrada.");
        }

        var animal = await LoadFarmAnimalAsync(farmId, request.AnimalIdentification, cancellationToken);
        var vaccinationType = NormalizeNullable(request.VaccinationType);
        var observations = NormalizeNullable(request.Observations);

        if (vaccinationType is null)
        {
            throw new DomainException("El tipo de vacunación es obligatorio.");
        }

        if (request.NextDose is not null && request.NextDose < request.VaccinationDate)
        {
            throw new DomainException("La próxima dosis no puede ser anterior a la fecha de vacunación.");
        }

        vaccination.AnimalId = animal.Id;
        vaccination.Animal = animal;
        vaccination.VaccinationDate = request.VaccinationDate;
        vaccination.NextDose = request.NextDose;
        vaccination.VaccinationType = vaccinationType;
        vaccination.Observations = observations;

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapVaccination(vaccination);
    }

    public async Task DeleteVaccinationAsync(long userId, UserRole role, long farmId, long vaccinationId, CancellationToken cancellationToken)
    {
        await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);

        var vaccination = await dbContext.Vaccinations
            .Include(entity => entity.Animal)
            .SingleOrDefaultAsync(entity => entity.Id == vaccinationId && entity.Animal.LivestockFarmId == farmId, cancellationToken);

        if (vaccination is null)
        {
            throw new DomainException("Vacunación no encontrada.");
        }

        dbContext.Vaccinations.Remove(vaccination);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<FarmCensusResponse> GetCensusAsync(long userId, UserRole role, long farmId, int? year, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);

        var targetYear = NormalizeYear(year);
        var availableYears = await LoadAvailableCensusYearsAsync(farm.Id, cancellationToken);
        var census = await LoadAnnualCensusAsync(farm.Id, targetYear, cancellationToken);

        return census is null
            ? BuildEmptyCensusResponse(farm, targetYear, availableYears)
            : MapCensus(farm, census, targetYear, availableYears);
    }

    public async Task<FarmCensusResponse> UpdateCensusAsync(long userId, UserRole role, long farmId, int year, UpdateFarmCensusRequest request, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        ValidateCensus(farm, request);

        var targetYear = NormalizeYear(year);
        var census = await LoadAnnualCensusAsync(farm.Id, targetYear, cancellationToken);

        if (census is null)
        {
            census = new Census
            {
                LivestockFarmId = farm.Id,
                CensusDate = new DateOnly(targetYear, 1, 1),
                OvinoCaprino = IsOvineOrCaprine(farm) ? new CensusOvinoCaprino() : null,
                Porcino = farm.LivestockSpecies == LivestockSpecies.Porcine ? new CensusPorcino() : null
            };
            dbContext.Census.Add(census);
        }

        if (IsOvineOrCaprine(farm))
        {
            census.OvinoCaprino ??= new CensusOvinoCaprino();
            census.OvinoCaprino.NonReproductiveUnder4Months = request.NonReproductiveUnder4Months ?? 0;
            census.OvinoCaprino.NonReproductiveBetween4And12Months = request.NonReproductiveBetween4And12Months ?? 0;
            census.OvinoCaprino.ReproductiveFemale = request.ReproductiveFemales ?? 0;
            census.OvinoCaprino.ReproductiveMale = request.ReproductiveMales ?? 0;
        }
        else
        {
            census.Porcino ??= new CensusPorcino();
            census.Porcino.Boars = request.Boars ?? 0;
            census.Porcino.Sow = request.SowsForLive ?? 0;
            census.Porcino.SowsReposition = request.SowsReposition ?? 0;
            census.Porcino.PigsReposition = request.MalesReposition ?? 0;
            census.Porcino.Piglets = request.Piglets ?? 0;
            census.Porcino.Rears = request.Rears ?? 0;
            census.Porcino.Baits = request.Baits ?? 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var availableYears = await LoadAvailableCensusYearsAsync(farm.Id, cancellationToken);
        return MapCensus(farm, census, targetYear, availableYears);
    }

    public async Task<FarmBalanceResponse> GetBalanceAsync(long userId, UserRole role, long farmId, int? year, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        var targetYear = NormalizeYear(year);

        var balances = await dbContext.Balances
            .AsNoTracking()
            .Where(entity => entity.LivestockFarmId == farm.Id && entity.BalanceDate.Year == targetYear)
            .ToListAsync(cancellationToken);

        var months = Enumerable.Range(1, 12)
            .Select(month => BuildMonthlyBalance(month, balances.Where(entity => entity.BalanceDate.Month == month)))
            .ToList();

        return new FarmBalanceResponse(
            farm.Id,
            targetYear,
            months.Sum(entity => entity.Registrations),
            months.Sum(entity => entity.Births),
            months.Sum(entity => entity.Deaths),
            months.Sum(entity => entity.Departures),
            months.Sum(entity => entity.MovementEntries),
            months.Sum(entity => entity.MovementDepartures),
            months.Sum(entity => entity.Balance),
            months);
    }

    public async Task<IReadOnlyList<FarmIncidentResponse>> GetIncidentsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);

        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .Include(entity => entity.Animal)
            .Where(entity => entity.LivestockFarmId == farmId)
            .OrderByDescending(entity => entity.IncidentDate)
            .ThenByDescending(entity => entity.Id)
            .ToListAsync(cancellationToken);

        return incidents.Select(MapIncident).ToList();
    }

    public async Task<FarmIncidentResponse> CreateIncidentAsync(long userId, UserRole role, long farmId, CreateFarmIncidentRequest request, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        var animalIdentification = NormalizeIdentifierOrNull(farm.LivestockSpecies, request.AnimalIdentification, "La identificación del animal relacionada con la incidencia no es válida.");
        var lastIdentification = NormalizeIdentifierOrNull(farm.LivestockSpecies, request.LastIdentification, "La identificación anterior no es válida.");
        var newIdentification = NormalizeIdentifierOrNull(farm.LivestockSpecies, request.NewIdentification, "La nueva identificación no es válida.");
        var changeReason = NormalizeNullable(request.ChangeReason);
        var description = NormalizeNullable(request.Description);

        if (animalIdentification is null && changeReason is null && description is null && lastIdentification is null && newIdentification is null)
        {
            throw new DomainException("Debes completar al menos un dato descriptivo de la incidencia.");
        }

        Animal? animal = null;
        if (animalIdentification is not null)
        {
            animal = await dbContext.Animals
                .SingleOrDefaultAsync(entity => entity.LivestockFarmId == farm.Id && entity.Identification == animalIdentification, cancellationToken);

            if (animal is null)
            {
                throw new DomainException("El animal indicado no pertenece a esta explotación.");
            }

            lastIdentification ??= animal.Identification;
        }

        var incident = new Incident
        {
            LivestockFarmId = farm.Id,
            AnimalId = animal?.Id,
            IncidentDate = request.IncidentDate,
            ChangeReason = changeReason,
            Description = description,
            LastIdentification = lastIdentification,
            NewIdentification = newIdentification
        };

        dbContext.Incidents.Add(incident);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (animal is not null)
        {
            incident.Animal = animal;
        }

        return MapIncident(incident);
    }

    public async Task<IReadOnlyList<FarmInspectionResponse>> GetInspectionsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);

        var inspections = await dbContext.Inspections
            .AsNoTracking()
            .Where(entity => entity.LivestockFarmId == farmId)
            .OrderByDescending(entity => entity.InspectionDate)
            .ThenByDescending(entity => entity.Id)
            .ToListAsync(cancellationToken);

        return inspections.Select(MapInspection).ToList();
    }

    public async Task<FarmInspectionResponse> CreateInspectionAsync(long userId, UserRole role, long farmId, CreateFarmInspectionRequest request, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        var reason = NormalizeNullable(request.Reason);
        var observations = NormalizeNullable(request.Observations);
        var veterinary = NormalizeNullable(request.Veterinary);

        if (reason is null && observations is null)
        {
            throw new DomainException("Debes indicar al menos el motivo o las observaciones de la inspección.");
        }

        if (request.TaggedAnimals is < 0)
        {
            throw new DomainException("El número de animales revisados no puede ser negativo.");
        }

        var inspection = new Inspection
        {
            LivestockFarmId = farm.Id,
            InspectionDate = request.InspectionDate,
            Reason = reason,
            Observations = observations,
            Veterinary = veterinary,
            TaggedAnimals = request.TaggedAnimals
        };

        dbContext.Inspections.Add(inspection);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapInspection(inspection);
    }

    private IQueryable<LivestockFarm> BuildAccessibleFarmQuery(long userId, UserRole role)
    {
        return role == UserRole.Manager
            ? dbContext.Farms.Where(entity => entity.Farmer.ManagerId == userId)
            : dbContext.Farms.Where(entity => entity.FarmerId == userId);
    }

    private async Task<LivestockFarm> LoadAccessibleFarmAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        var farm = await BuildAccessibleFarmQuery(userId, role)
            .SingleOrDefaultAsync(entity => entity.Id == farmId, cancellationToken);

        return farm ?? throw new DomainException("Explotación no encontrada.");
    }

    private async Task<Animal> LoadFarmAnimalAsync(long farmId, string identification, CancellationToken cancellationToken)
    {
        var farm = await dbContext.Farms
            .AsNoTracking()
            .SingleAsync(entity => entity.Id == farmId, cancellationToken);
        var normalizedIdentification = NormalizeIdentifier(farm.LivestockSpecies, identification, "Debes indicar un crotal o lote válido.");

        var animal = await dbContext.Animals
            .SingleOrDefaultAsync(entity => entity.LivestockFarmId == farmId && entity.Identification == normalizedIdentification, cancellationToken);

        return animal ?? throw new DomainException("El animal indicado no pertenece a esta explotación.");
    }

    private static bool IsOvineOrCaprine(LivestockFarm farm) =>
        farm.LivestockSpecies is LivestockSpecies.Ovine or LivestockSpecies.Caprine;

    private static void EnsureOvineOrCaprineFarm(LivestockFarm farm)
    {
        if (!IsOvineOrCaprine(farm))
        {
            throw new DomainException("Esta operación está disponible solo para explotaciones ovinas o caprinas.");
        }
    }

    private static void ValidateCensus(LivestockFarm farm, UpdateFarmCensusRequest request)
    {
        var values = IsOvineOrCaprine(farm)
            ? new int?[]
            {
                request.NonReproductiveUnder4Months,
                request.NonReproductiveBetween4And12Months,
                request.ReproductiveFemales,
                request.ReproductiveMales
            }
            : new int?[]
            {
                request.Boars,
                request.SowsForLive,
                request.SowsReposition,
                request.MalesReposition,
                request.Piglets,
                request.Rears,
                request.Baits
            };

        if (values.Any(value => value is null))
        {
            throw new DomainException("Debes completar todas las categorías del censo.");
        }

        if (values.Any(value => value < 0))
        {
            throw new DomainException("Las categorías del censo no pueden tener valores negativos.");
        }
    }

    private int NormalizeYear(int? year)
    {
        var targetYear = year ?? clock.UtcNow.Year;
        if (targetYear < 2000 || targetYear > clock.UtcNow.Year + 1)
        {
            throw new DomainException("El año indicado no es válido.");
        }

        return targetYear;
    }

    private async Task<Census?> LoadAnnualCensusAsync(long farmId, int year, CancellationToken cancellationToken)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);

        return await dbContext.Census
            .Include(entity => entity.OvinoCaprino)
            .Include(entity => entity.Porcino)
            .Where(entity => entity.LivestockFarmId == farmId && entity.CensusDate >= start && entity.CensusDate <= end)
            .OrderByDescending(entity => entity.CensusDate == start)
            .ThenByDescending(entity => entity.CensusDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<int>> LoadAvailableCensusYearsAsync(long farmId, CancellationToken cancellationToken)
    {
        return await dbContext.Census
            .AsNoTracking()
            .Where(entity => entity.LivestockFarmId == farmId)
            .Select(entity => entity.CensusDate.Year)
            .Distinct()
            .OrderByDescending(year => year)
            .ToListAsync(cancellationToken);
    }

    private void AddBalanceEvent(long farmId, DateOnly date, string cause, int numberOfAnimals, string? destinationCode = null)
    {
        dbContext.Balances.Add(new Balance
        {
            LivestockFarmId = farmId,
            BalanceDate = date,
            ModificationCause = cause,
            NumberOfAnimals = numberOfAnimals,
            DestinationLivestockCode = destinationCode
        });
    }

    private static FarmMonthlyBalanceResponse BuildMonthlyBalance(int month, IEnumerable<Balance> balances)
    {
        var registrations = 0;
        var births = 0;
        var deaths = 0;
        var departures = 0;
        var movementEntries = 0;
        var movementDepartures = 0;

        foreach (var balance in balances)
        {
            if (IsCause(balance.ModificationCause, "Entrada"))
            {
                registrations += balance.NumberOfAnimals;
                movementEntries += balance.NumberOfAnimals;
            }
            else if (IsCause(balance.ModificationCause, BirthBalanceCause))
            {
                births += balance.NumberOfAnimals;
            }
            else if (IsCause(balance.ModificationCause, AnimalDischargeCause.Muerte.ToString()))
            {
                deaths += balance.NumberOfAnimals;
            }
            else if (IsCause(balance.ModificationCause, AnimalDischargeCause.Salida.ToString()))
            {
                departures += balance.NumberOfAnimals;
                movementDepartures += balance.NumberOfAnimals;
            }
            else if (IsCause(balance.ModificationCause, "Autorreposicion") || IsCause(balance.ModificationCause, "Autorreposición"))
            {
                registrations += balance.NumberOfAnimals;
            }
        }

        return new FarmMonthlyBalanceResponse(
            month,
            registrations,
            births,
            deaths,
            departures,
            movementEntries,
            movementDepartures,
            registrations + births - deaths - departures);
    }

    private static bool IsCause(string value, string expected)
    {
        return value.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static FarmBirthResponse MapBirth(AnimalBirth birth)
    {
        return new FarmBirthResponse(
            birth.Id,
            birth.LivestockFarmId,
            birth.BirthDate,
            birth.OffspringNumber,
            birth.BirthWeight,
            EmptyToNull(birth.Observations));
    }

    private static FarmDeathResponse MapDeath(Animal animal)
    {
        return new FarmDeathResponse(
            animal.Id,
            animal.LivestockFarmId,
            animal.Identification,
            EmptyToNull(animal.Breed),
            EmptyToNull(animal.Sex),
            animal.BirthYear,
            animal.DischargeDate!.Value,
            animal.DischargeCause!.Value.ToString(),
            EmptyToNull(animal.DestinationCode));
    }

    private static FarmVaccinationResponse MapVaccination(Vaccination vaccination)
    {
        return new FarmVaccinationResponse(
            vaccination.Id,
            vaccination.Animal.LivestockFarmId,
            vaccination.AnimalId,
            vaccination.Animal.Identification,
            EmptyToNull(vaccination.Animal.Breed),
            vaccination.VaccinationDate,
            vaccination.NextDose,
            vaccination.VaccinationType,
            EmptyToNull(vaccination.Observations));
    }

    private static FarmCensusResponse BuildEmptyCensusResponse(LivestockFarm farm, int year, IReadOnlyList<int> availableYears)
    {
        return new FarmCensusResponse(
            null,
            farm.Id,
            year,
            farm.LivestockSpecies.ToString(),
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            availableYears);
    }

    private static FarmCensusResponse MapCensus(LivestockFarm farm, Census census, int year, IReadOnlyList<int> availableYears)
    {
        var ovineDetail = census.OvinoCaprino;
        var porcineDetail = census.Porcino;
        var under4 = ovineDetail?.NonReproductiveUnder4Months ?? 0;
        var between4And12 = ovineDetail?.NonReproductiveBetween4And12Months ?? 0;
        var reproductiveFemales = ovineDetail?.ReproductiveFemale ?? 0;
        var reproductiveMales = ovineDetail?.ReproductiveMale ?? 0;
        var boars = porcineDetail?.Boars ?? 0;
        var sowsForLive = porcineDetail?.Sow ?? 0;
        var sowsReposition = porcineDetail?.SowsReposition ?? 0;
        var malesReposition = porcineDetail?.PigsReposition ?? 0;
        var piglets = porcineDetail?.Piglets ?? 0;
        var rears = porcineDetail?.Rears ?? 0;
        var baits = porcineDetail?.Baits ?? 0;
        var total = under4 + between4And12 + reproductiveFemales + reproductiveMales + boars + sowsForLive + sowsReposition + malesReposition + piglets + rears + baits;

        return new FarmCensusResponse(
            census.Id,
            census.LivestockFarmId,
            year,
            farm.LivestockSpecies.ToString(),
            under4,
            between4And12,
            reproductiveFemales,
            reproductiveMales,
            boars,
            sowsForLive,
            sowsReposition,
            malesReposition,
            piglets,
            rears,
            baits,
            total,
            availableYears);
    }

    private static FarmIncidentResponse MapIncident(Incident incident)
    {
        return new FarmIncidentResponse(
            incident.Id,
            incident.LivestockFarmId,
            incident.AnimalId,
            EmptyToNull(incident.Animal?.Identification),
            incident.IncidentDate,
            EmptyToNull(incident.ChangeReason),
            EmptyToNull(incident.Description),
            EmptyToNull(incident.LastIdentification),
            EmptyToNull(incident.NewIdentification));
    }

    private static FarmInspectionResponse MapInspection(Inspection inspection)
    {
        return new FarmInspectionResponse(
            inspection.Id,
            inspection.LivestockFarmId,
            inspection.InspectionDate,
            EmptyToNull(inspection.Reason),
            EmptyToNull(inspection.Observations),
            EmptyToNull(inspection.Veterinary),
            inspection.TaggedAnimals);
    }

    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeDeathDestinationCode(LivestockSpecies species, string? destinationCode)
    {
        var normalizedDestinationCode = NormalizeNullable(destinationCode)?.ToUpperInvariant();

        if (normalizedDestinationCode is null)
        {
            throw new DomainException("El destino de una baja por muerte es obligatorio.");
        }

        if (species == LivestockSpecies.Porcine)
        {
            if (normalizedDestinationCode != "MER")
            {
                throw new DomainException("En ganado porcino, una baja por muerte solo puede registrarse con destino MER.");
            }

            return normalizedDestinationCode;
        }

        if (normalizedDestinationCode is not ("SANDACH" or "MER"))
        {
            throw new DomainException("El destino de una baja por muerte debe ser SANDACH o MER.");
        }

        return normalizedDestinationCode;
    }

    private static string NormalizeIdentifier(LivestockSpecies species, string? value, string invalidMessage)
    {
        var normalizedValue = NormalizeNullable(value);
        if (normalizedValue is null)
        {
            throw new DomainException("Debes indicar el crotal o lote del animal.");
        }

        var normalizedIdentification = DomainValidators.NormalizeAnimalIdentification(normalizedValue);
        if (!DomainValidators.IsValidAnimalIdentification(species, normalizedIdentification))
        {
            throw new DomainException(invalidMessage);
        }

        return normalizedIdentification;
    }

    private static string? NormalizeIdentifierOrNull(LivestockSpecies species, string? value, string invalidMessage)
    {
        var normalizedValue = NormalizeNullable(value);
        if (normalizedValue is null)
        {
            return null;
        }

        var normalizedIdentification = DomainValidators.NormalizeAnimalIdentification(normalizedValue);
        if (!DomainValidators.IsValidAnimalIdentification(species, normalizedIdentification))
        {
            throw new DomainException(invalidMessage);
        }

        return normalizedIdentification;
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
