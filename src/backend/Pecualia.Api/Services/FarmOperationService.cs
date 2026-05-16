using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

public interface IFarmOperationService
{
    Task<IReadOnlyList<FarmBirthResponse>> GetBirthsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);

    Task<FarmAutorrepositionAvailabilityResponse> GetAutorrepositionAvailabilityAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FarmPendingPorcineTransitionResponse>> GetPendingPorcineTransitionsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FarmPendingPorcineTransitionResponse>> ResolvePorcineTransitionAsync(long userId, UserRole role, long farmId, long birthId, ResolvePorcineTransitionRequest request, CancellationToken cancellationToken);

    Task<FarmBirthResponse> CreateBirthAsync(long userId, UserRole role, long farmId, CreateFarmBirthRequest request, CancellationToken cancellationToken);

    Task<FarmBirthResponse> UpdateBirthAsync(long userId, UserRole role, long farmId, long birthId, UpdateFarmBirthRequest request, CancellationToken cancellationToken);

    Task DeleteBirthAsync(long userId, UserRole role, long farmId, long birthId, CancellationToken cancellationToken);

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

public sealed class FarmOperationService(PecualiaDbContext dbContext, IClock clock, IFarmCensusProjectionService censusProjectionService) : IFarmOperationService
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

    public async Task<FarmAutorrepositionAvailabilityResponse> GetAutorrepositionAvailabilityAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        EnsureOvineOrCaprineFarm(farm);
        var today = DateOnly.FromDateTime(clock.UtcNow.Date);
        return await CalculateAutorrepositionAvailabilityAsync(farm, today, cancellationToken);
    }

    public async Task<IReadOnlyList<FarmPendingPorcineTransitionResponse>> GetPendingPorcineTransitionsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        EnsurePorcineFarm(farm);

        var today = DateOnly.FromDateTime(clock.UtcNow.Date);
        return await BuildPendingPorcineTransitionsAsync(farm, today, cancellationToken);
    }

    public async Task<IReadOnlyList<FarmPendingPorcineTransitionResponse>> ResolvePorcineTransitionAsync(long userId, UserRole role, long farmId, long birthId, ResolvePorcineTransitionRequest request, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        EnsurePorcineFarm(farm);

        var today = DateOnly.FromDateTime(clock.UtcNow.Date);
        var birth = await dbContext.AnimalBirths
            .Include(entity => entity.PorcineTransitionDecision)
            .SingleOrDefaultAsync(entity => entity.Id == birthId && entity.LivestockFarmId == farmId, cancellationToken);

        if (birth is null)
        {
            throw new DomainException("Nacimiento no encontrado.");
        }

        var effectiveDate = PorcineTransitionSupport.GetDecisionDate(birth.BirthDate);
        if (effectiveDate > today)
        {
            throw new DomainException("La reclasificación porcina no se puede resolver antes de que el lote cumpla 3 meses.");
        }

        var consumedAnimals = await dbContext.Animals
            .AsNoTracking()
            .Include(entity => entity.Porcino)
            .Where(entity =>
                entity.SourceBirthId == birth.Id &&
                entity.RegistrationDate != null &&
                entity.RegistrationDate <= today)
            .ToListAsync(cancellationToken);

        var currentConsumption = BuildPorcineDecisionConsumption(consumedAnimals);
        var remainingQuantities = birth.PorcineTransitionDecision is null
            ? new PorcineDecisionRemainingQuantities(
                Math.Max(0, birth.OffspringNumber - consumedAnimals.Count),
                0,
                0)
            : PorcineTransitionSupport.CalculateRemainingQuantities(birth.PorcineTransitionDecision, currentConsumption);
        var expectedTotal = birth.PorcineTransitionDecision is null
            ? Math.Max(0, birth.OffspringNumber - consumedAnimals.Count)
            : remainingQuantities.Total;

        if (request.ToRears < 0 || request.ToSowsReposition < 0 || request.ToMalesReposition < 0)
        {
            throw new DomainException("Las cantidades de reclasificación no pueden ser negativas.");
        }

        if (request.ToRears + request.ToSowsReposition + request.ToMalesReposition != expectedTotal)
        {
            throw new DomainException("La suma del reparto debe coincidir exactamente con los animales pendientes de reclasificación.");
        }

        Balance balance;
        if (birth.PorcineTransitionDecision?.BalanceId is long existingBalanceId)
        {
            balance = await dbContext.Balances.SingleAsync(entity => entity.Id == existingBalanceId, cancellationToken);
            balance.BalanceDate = effectiveDate;
            balance.ModificationCause = AnimalRegistrationCause.Autorreposicion.ToString();
            balance.NumberOfAnimals = expectedTotal;
        }
        else
        {
            balance = new Balance
            {
                LivestockFarmId = farm.Id,
                BalanceDate = effectiveDate,
                ModificationCause = AnimalRegistrationCause.Autorreposicion.ToString(),
                NumberOfAnimals = expectedTotal
            };
            dbContext.Balances.Add(balance);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var balanceDetail = await dbContext.BalancePorcino.SingleOrDefaultAsync(entity => entity.BalanceId == balance.Id, cancellationToken);
        if (balanceDetail is null)
        {
            balanceDetail = new BalancePorcino { BalanceId = balance.Id };
            dbContext.BalancePorcino.Add(balanceDetail);
        }

        balanceDetail.Baits = 0;
        balanceDetail.Boars = 0;
        balanceDetail.Breed = null;
        balanceDetail.Piglets = expectedTotal;
        balanceDetail.PigsReposition = request.ToMalesReposition;
        balanceDetail.Rear = request.ToRears;
        balanceDetail.SowsForLive = 0;
        balanceDetail.SowsReposition = request.ToSowsReposition;
        balanceDetail.Tag = null;
        balanceDetail.Type = "Reclasificación porcina";

        if (birth.PorcineTransitionDecision is null)
        {
            birth.PorcineTransitionDecision = new PorcineBirthTransitionDecision
            {
                BirthId = birth.Id
            };
            dbContext.PorcineBirthTransitionDecisions.Add(birth.PorcineTransitionDecision);
        }

        birth.PorcineTransitionDecision.EffectiveDate = effectiveDate;
        birth.PorcineTransitionDecision.ToRears = request.ToRears;
        birth.PorcineTransitionDecision.ToSowsReposition = request.ToSowsReposition;
        birth.PorcineTransitionDecision.ToMalesReposition = request.ToMalesReposition;
        birth.PorcineTransitionDecision.BaselineRearsConsumed = currentConsumption.Rears;
        birth.PorcineTransitionDecision.BaselineSowsRepositionConsumed = currentConsumption.Sows;
        birth.PorcineTransitionDecision.BaselineMalesRepositionConsumed = currentConsumption.Males;
        birth.PorcineTransitionDecision.ResolvedAt = clock.UtcNow.UtcDateTime;
        birth.PorcineTransitionDecision.BalanceId = balance.Id;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildPendingPorcineTransitionsAsync(farm, today, cancellationToken);
    }

    public async Task<FarmBirthResponse> CreateBirthAsync(long userId, UserRole role, long farmId, CreateFarmBirthRequest request, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        ValidateBirthRequest(farm, request.BirthDate, request.OffspringNumber, request.BirthWeight, DateOnly.FromDateTime(clock.UtcNow.Date));

        var balance = new Balance
        {
            LivestockFarmId = farm.Id,
            BalanceDate = request.BirthDate,
            ModificationCause = BirthBalanceCause,
            NumberOfAnimals = request.OffspringNumber
        };

        dbContext.Balances.Add(balance);
        await dbContext.SaveChangesAsync(cancellationToken);

        var birth = new AnimalBirth
        {
            LivestockFarmId = farm.Id,
            BirthDate = request.BirthDate,
            BirthWeight = request.BirthWeight,
            Observations = NormalizeNullable(request.Observations),
            OffspringNumber = request.OffspringNumber,
            BalanceId = balance.Id
        };

        dbContext.AnimalBirths.Add(birth);

        await dbContext.SaveChangesAsync(cancellationToken);

        if (farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            dbContext.BalancePorcino.Add(new BalancePorcino
            {
                BalanceId = balance.Id,
                Piglets = request.OffspringNumber
            });
        }
        else
        {
            dbContext.BalanceOvinoCaprino.Add(new BalanceOvinoCaprino
            {
                BalanceId = balance.Id,
                NonReproductiveUnder4Months = request.OffspringNumber
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapBirth(birth);
    }

    public async Task<FarmBirthResponse> UpdateBirthAsync(long userId, UserRole role, long farmId, long birthId, UpdateFarmBirthRequest request, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        ValidateBirthRequest(farm, request.BirthDate, request.OffspringNumber, request.BirthWeight, DateOnly.FromDateTime(clock.UtcNow.Date));

        var birth = await dbContext.AnimalBirths
            .Include(entity => entity.Balance)
            .Include(entity => entity.PorcineTransitionDecision)
            .SingleOrDefaultAsync(entity => entity.Id == birthId && entity.LivestockFarmId == farmId, cancellationToken);

        if (birth is null)
        {
            throw new DomainException("Nacimiento no encontrado.");
        }

        var consumedAnimals = await dbContext.Animals.CountAsync(entity => entity.SourceBirthId == birth.Id, cancellationToken);
        if (request.OffspringNumber < consumedAnimals)
        {
            throw new DomainException("No puedes declarar menos crías que las ya consumidas por autoreposición.");
        }

        birth.BirthDate = request.BirthDate;
        birth.OffspringNumber = request.OffspringNumber;
        birth.BirthWeight = request.BirthWeight;
        birth.Observations = NormalizeNullable(request.Observations);

        if (birth.Balance is not null)
        {
            birth.Balance.BalanceDate = request.BirthDate;
            birth.Balance.NumberOfAnimals = request.OffspringNumber;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (birth.Balance is not null)
        {
            if (birth.Balance.LivestockFarmId == 0)
            {
                birth.Balance.LivestockFarmId = farmId;
            }

            if (farm.LivestockSpecies == LivestockSpecies.Porcine)
            {
                var detail = await dbContext.BalancePorcino.SingleOrDefaultAsync(entity => entity.BalanceId == birth.Balance.Id, cancellationToken);
                if (detail is null)
                {
                    detail = new BalancePorcino
                    {
                        BalanceId = birth.Balance.Id
                    };
                    dbContext.BalancePorcino.Add(detail);
                }

                detail.Baits = 0;
                detail.Boars = 0;
                detail.Breed = null;
                detail.Piglets = request.OffspringNumber;
                detail.PigsReposition = 0;
                detail.Rear = 0;
                detail.SowsForLive = 0;
                detail.SowsReposition = 0;
                detail.Tag = null;
                detail.Type = null;
            }
            else
            {
                var detail = await dbContext.BalanceOvinoCaprino.SingleOrDefaultAsync(entity => entity.BalanceId == birth.Balance.Id, cancellationToken);
                if (detail is null)
                {
                    detail = new BalanceOvinoCaprino
                    {
                        BalanceId = birth.Balance.Id
                    };
                    dbContext.BalanceOvinoCaprino.Add(detail);
                }

                detail.NonReproductiveUnder4Months = request.OffspringNumber;
                detail.NonReproductiveBetween4And12Months = 0;
                detail.ReproductiveFemales = 0;
                detail.ReproductiveMales = 0;
                detail.TransportTicketNumber = null;
                detail.TransporterName = null;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (birth.PorcineTransitionDecision is not null)
        {
            if (PorcineTransitionSupport.GetDecisionDate(request.BirthDate) > DateOnly.FromDateTime(clock.UtcNow.Date))
            {
                if (birth.PorcineTransitionDecision.BalanceId is long transitionBalanceId)
                {
                    var transitionBalance = await dbContext.Balances.SingleOrDefaultAsync(entity => entity.Id == transitionBalanceId, cancellationToken);
                    if (transitionBalance is not null)
                    {
                        var transitionDetail = await dbContext.BalancePorcino.SingleOrDefaultAsync(entity => entity.BalanceId == transitionBalance.Id, cancellationToken);
                        if (transitionDetail is not null)
                        {
                            dbContext.BalancePorcino.Remove(transitionDetail);
                        }

                        dbContext.Balances.Remove(transitionBalance);
                    }
                }

                dbContext.PorcineBirthTransitionDecisions.Remove(birth.PorcineTransitionDecision);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                birth.PorcineTransitionDecision.EffectiveDate = PorcineTransitionSupport.GetDecisionDate(request.BirthDate);
                if (birth.PorcineTransitionDecision.BalanceId is long transitionBalanceId)
                {
                    var transitionBalance = await dbContext.Balances.SingleOrDefaultAsync(entity => entity.Id == transitionBalanceId, cancellationToken);
                    if (transitionBalance is not null)
                    {
                        transitionBalance.BalanceDate = birth.PorcineTransitionDecision.EffectiveDate;
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return MapBirth(birth);
    }

    public async Task DeleteBirthAsync(long userId, UserRole role, long farmId, long birthId, CancellationToken cancellationToken)
    {
        await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);

        var birth = await dbContext.AnimalBirths
            .Include(entity => entity.Balance)
            .Include(entity => entity.PorcineTransitionDecision)
            .SingleOrDefaultAsync(entity => entity.Id == birthId && entity.LivestockFarmId == farmId, cancellationToken);

        if (birth is null)
        {
            throw new DomainException("Nacimiento no encontrado.");
        }

        var consumedAnimals = await dbContext.Animals.AnyAsync(entity => entity.SourceBirthId == birth.Id, cancellationToken);
        if (consumedAnimals)
        {
            throw new DomainException("No puedes eliminar un nacimiento que ya ha sido consumido por autoreposición.");
        }

        dbContext.AnimalBirths.Remove(birth);

        if (birth.Balance is not null)
        {
            dbContext.Balances.Remove(birth.Balance);
        }

        if (birth.PorcineTransitionDecision?.BalanceId is long transitionBalanceId)
        {
            var transitionBalance = await dbContext.Balances.SingleOrDefaultAsync(entity => entity.Id == transitionBalanceId, cancellationToken);
            if (transitionBalance is not null)
            {
                var transitionDetail = await dbContext.BalancePorcino.SingleOrDefaultAsync(entity => entity.BalanceId == transitionBalance.Id, cancellationToken);
                if (transitionDetail is not null)
                {
                    dbContext.BalancePorcino.Remove(transitionDetail);
                }

                dbContext.Balances.Remove(transitionBalance);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FarmDeathResponse>> GetDeathsAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);

        var animalDeaths = await dbContext.Animals
            .AsNoTracking()
            .Include(entity => entity.Porcino)
            .Where(entity => entity.LivestockFarmId == farmId && entity.DischargeCause == AnimalDischargeCause.Muerte && entity.DischargeDate != null)
            .OrderByDescending(entity => entity.DischargeDate)
            .ThenBy(entity => entity.Identification)
            .ToListAsync(cancellationToken);

        var deaths = animalDeaths
            .Select(MapDeath)
            .ToList();

        if (farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            var aggregateDeaths = await dbContext.Balances
                .AsNoTracking()
                .Include(entity => entity.Porcino)
                .Where(entity =>
                    entity.LivestockFarmId == farmId &&
                    entity.OriginLivestockCode == BalanceMarkers.PorcineAggregateDeath &&
                    entity.ModificationCause == AnimalDischargeCause.Muerte.ToString())
                .OrderByDescending(entity => entity.BalanceDate)
                .ThenByDescending(entity => entity.Id)
                .ToListAsync(cancellationToken);

            deaths.AddRange(aggregateDeaths.Select(MapAggregateDeath));
        }

        return deaths
            .OrderByDescending(entity => entity.DischargeDate)
            .ThenByDescending(entity => entity.Id)
            .ToList();
    }

    public async Task<FarmDeathResponse> CreateDeathAsync(long userId, UserRole role, long farmId, CreateFarmDeathRequest request, CancellationToken cancellationToken)
    {
        var farm = await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        var quantity = NormalizeDeathQuantity(request.Quantity);
        var destinationCode = NormalizeDeathDestinationCode(farm.LivestockSpecies, request.DestinationCode);
        var porcineAnimalType = farm.LivestockSpecies == LivestockSpecies.Porcine
            ? NormalizePorcineDeathAnimalType(request.AnimalType)
            : null;
        var identification = NormalizeIdentifierOrNull(farm.LivestockSpecies, request.Identification, "El crotal o lote no es válido.");

        if (farm.LivestockSpecies == LivestockSpecies.Porcine && identification is null)
        {
            var snapshot = await censusProjectionService.BuildSnapshotAsync(farm, request.DischargeDate, cancellationToken);
            if (PorcineMovementSupport.GetAvailableAnimals(snapshot, porcineAnimalType) < quantity)
            {
                throw new DomainException("No hay suficientes animales disponibles en esa categoría para registrar la baja.");
            }

            var aggregateBalance = await AddBalanceEventAsync(
                farm,
                request.DischargeDate,
                AnimalDischargeCause.Muerte.ToString(),
                quantity,
                cancellationToken,
                destinationCode: destinationCode,
                originCode: BalanceMarkers.PorcineAggregateDeath,
                porcineAnimalType: porcineAnimalType);
            await dbContext.SaveChangesAsync(cancellationToken);

            return MapAggregateDeath(aggregateBalance);
        }

        if (identification is null)
        {
            throw new DomainException("Debes indicar el crotal o lote del animal.");
        }

        if (quantity != 1)
        {
            throw new DomainException("Si indicas un crotal individual, el número de animales debe ser 1.");
        }

        var animal = await dbContext.Animals
            .Include(entity => entity.Porcino)
            .SingleOrDefaultAsync(entity => entity.LivestockFarmId == farm.Id && entity.Identification == identification, cancellationToken);

        if (animal is null)
        {
            throw new DomainException("No existe un animal activo con ese crotal en la explotación.");
        }

        if (animal.DischargeDate is not null)
        {
            throw new DomainException("El animal ya está dado de baja.");
        }

        if (farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            if (animal.Porcino is null)
            {
                animal.Porcino = new PorcinoAnimal
                {
                    AnimalId = animal.Id,
                    AnimalType = porcineAnimalType!
                };
                dbContext.PorcinoAnimals.Add(animal.Porcino);
            }
            else
            {
                animal.Porcino.AnimalType = porcineAnimalType!;
            }
        }

        animal.DischargeDate = request.DischargeDate;
        animal.DischargeCause = AnimalDischargeCause.Muerte;
        animal.DestinationCode = destinationCode;
        await dbContext.SaveChangesAsync(cancellationToken);

        var balance = await AddBalanceEventAsync(
            farm,
            request.DischargeDate,
            AnimalDischargeCause.Muerte.ToString(),
            1,
            cancellationToken,
            destinationCode: animal.DestinationCode,
            animal: animal,
            porcineAnimalType: porcineAnimalType);
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
        var today = DateOnly.FromDateTime(clock.UtcNow.Date);
        var asOfDate = targetYear == today.Year ? today : new DateOnly(targetYear, 12, 31);
        return await censusProjectionService.BuildCensusResponseAsync(farm, targetYear, asOfDate, cancellationToken);
    }

    public async Task<FarmCensusResponse> UpdateCensusAsync(long userId, UserRole role, long farmId, int year, UpdateFarmCensusRequest request, CancellationToken cancellationToken)
    {
        await LoadAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        _ = NormalizeYear(year);
        _ = request;
        throw new DomainException("El censo se calcula automáticamente y no admite edición manual.");
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

    private static void EnsurePorcineFarm(LivestockFarm farm)
    {
        if (farm.LivestockSpecies != LivestockSpecies.Porcine)
        {
            throw new DomainException("Esta operación está disponible solo para explotaciones porcinas.");
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

    private async Task<Balance> AddBalanceEventAsync(
        LivestockFarm farm,
        DateOnly date,
        string cause,
        int numberOfAnimals,
        CancellationToken cancellationToken,
        string? destinationCode = null,
        Animal? animal = null,
        string? originCode = null,
        string? porcineAnimalType = null)
    {
        var balance = new Balance
        {
            LivestockFarmId = farm.Id,
            BalanceDate = date,
            ModificationCause = cause,
            NumberOfAnimals = numberOfAnimals,
            DestinationLivestockCode = destinationCode,
            OriginLivestockCode = originCode
        };

        dbContext.Balances.Add(balance);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            BalancePorcino? porcineDetail = null;

            if (animal is not null)
            {
                var bucket = ResolvePorcineDeathBucket(animal, date);
                porcineDetail = new BalancePorcino
                {
                    BalanceId = balance.Id,
                    Baits = bucket == "baits" ? 1 : 0,
                    Boars = bucket == "boars" ? 1 : 0,
                    Breed = animal.Breed,
                    Piglets = bucket == "piglets" ? 1 : 0,
                    PigsReposition = bucket == "pigs_reposition" ? 1 : 0,
                    Rear = bucket == "rears" ? 1 : 0,
                    SowsForLive = bucket == "sows" ? 1 : 0,
                    SowsReposition = bucket == "sows_reposition" ? 1 : 0,
                    Tag = animal.Porcino?.Tag,
                    Type = animal.Porcino?.AnimalType
                };
            }
            else if (!string.IsNullOrWhiteSpace(porcineAnimalType))
            {
                var breakdown = PorcineMovementSupport.BuildBreakdown(porcineAnimalType, numberOfAnimals);
                porcineDetail = new BalancePorcino
                {
                    BalanceId = balance.Id,
                    Baits = breakdown.Baits,
                    Boars = breakdown.Boars,
                    Piglets = breakdown.Piglets,
                    PigsReposition = breakdown.PigsReposition,
                    Rear = breakdown.Rears,
                    SowsForLive = breakdown.Sows,
                    SowsReposition = breakdown.SowsReposition,
                    Type = porcineAnimalType.Trim()
                };
            }

            if (porcineDetail is not null)
            {
                dbContext.BalancePorcino.Add(porcineDetail);
                balance.Porcino = porcineDetail;
            }
        }
        else if (animal is not null)
        {
            var ovineBucket = ResolveOvineDeathBucket(animal, date);
            dbContext.BalanceOvinoCaprino.Add(new BalanceOvinoCaprino
            {
                BalanceId = balance.Id,
                NonReproductiveUnder4Months = ovineBucket == "under4" ? 1 : 0,
                NonReproductiveBetween4And12Months = ovineBucket == "from4to12" ? 1 : 0,
                ReproductiveFemales = ovineBucket == "female" ? 1 : 0,
                ReproductiveMales = ovineBucket == "male" ? 1 : 0
            });
        }

        return balance;
    }

    private static string NormalizePorcineDeathAnimalType(string? animalType)
    {
        var normalizedAnimalType = NormalizeNullable(animalType);
        if (normalizedAnimalType is null)
        {
            throw new DomainException("Debes indicar el tipo de animal para registrar la baja en porcino.");
        }

        return normalizedAnimalType;
    }

    private static int NormalizeDeathQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("El número de animales debe ser mayor que cero.");
        }

        return quantity;
    }

    private static string ResolveOvineDeathBucket(Animal animal, DateOnly asOfDate)
    {
        if (animal.RegistrationCause == AnimalRegistrationCause.Autorreposicion)
        {
            var normalizedSex = FarmCensusProjectionSupport.NormalizeSex(animal.Sex);
            if (normalizedSex == "female")
            {
                return "female";
            }

            if (normalizedSex == "male")
            {
                return "male";
            }

            return "from4to12";
        }

        var birthDate = FarmCensusProjectionSupport.ResolveBirthDate(animal);
        if (birthDate is not null && FarmCensusProjectionSupport.IsYoungerThanMonths(birthDate.Value, asOfDate, 4))
        {
            return "under4";
        }

        if (birthDate is not null && FarmCensusProjectionSupport.IsYoungerThanMonths(birthDate.Value, asOfDate, 12))
        {
            return "from4to12";
        }

        var sex = FarmCensusProjectionSupport.NormalizeSex(animal.Sex);
        return sex == "male" ? "male" : "female";
    }

    private static string ResolvePorcineDeathBucket(Animal animal, DateOnly asOfDate)
    {
        var type = FarmCensusProjectionSupport.NormalizeType(animal.Porcino?.AnimalType);
        if (string.IsNullOrWhiteSpace(type))
        {
            var birthDate = FarmCensusProjectionSupport.ResolveBirthDate(animal);
            if (birthDate is not null && PorcineTransitionSupport.IsPigletStage(birthDate.Value, asOfDate))
            {
                return "piglets";
            }

            return "rears";
        }

        if (type.Contains("bait", StringComparison.Ordinal) || type.Contains("cebo", StringComparison.Ordinal))
        {
            return "baits";
        }

        if (type.Contains("boar", StringComparison.Ordinal) || type.Contains("verraco", StringComparison.Ordinal))
        {
            return "boars";
        }

        if (type.Contains("piglet", StringComparison.Ordinal) || type.Contains("lech", StringComparison.Ordinal))
        {
            return "piglets";
        }

        if (type.Contains("reposition", StringComparison.Ordinal) && (type.Contains("sow", StringComparison.Ordinal) || type.Contains("cerda", StringComparison.Ordinal)))
        {
            return "sows_reposition";
        }

        if (type.Contains("reposition", StringComparison.Ordinal) || type.Contains("repos", StringComparison.Ordinal))
        {
            return "pigs_reposition";
        }

        if (type.Contains("sow", StringComparison.Ordinal) || type.Contains("cerda", StringComparison.Ordinal))
        {
            return "sows";
        }

        return "rears";
    }

    private async Task<FarmAutorrepositionAvailabilityResponse> CalculateAutorrepositionAvailabilityAsync(LivestockFarm farm, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        var snapshot = await censusProjectionService.BuildSnapshotAsync(farm, asOfDate, cancellationToken);
        var availableAnimals = farm.LivestockSpecies == LivestockSpecies.Porcine
            ? snapshot.Piglets + snapshot.Rears + snapshot.SowsReposition + snapshot.MalesReposition + snapshot.PendingPorcineTransitions
            : snapshot.NonReproductiveUnder4Months + snapshot.NonReproductiveBetween4And12Months;
        var eligibleAnimals = farm.LivestockSpecies == LivestockSpecies.Porcine
            ? snapshot.Rears + snapshot.SowsReposition + snapshot.MalesReposition
            : snapshot.NonReproductiveBetween4And12Months;
        return new FarmAutorrepositionAvailabilityResponse(availableAnimals, eligibleAnimals);
    }

    private static void ValidateBirthRequest(LivestockFarm farm, DateOnly birthDate, int offspringNumber, decimal? birthWeight, DateOnly today)
    {
        if (offspringNumber <= 0)
        {
            throw new DomainException("El número de crías debe ser mayor que cero.");
        }

        if (birthWeight is < 0)
        {
            throw new DomainException("El peso de nacimiento no puede ser negativo.");
        }

        if (birthDate > today)
        {
            throw new DomainException("La fecha de nacimiento no puede estar en el futuro.");
        }

        if (farm.LivestockSpecies == LivestockSpecies.Porcine && birthDate.AddMonths(3) < today)
        {
            throw new DomainException("En porcino no puedes registrar nacimientos con más de 3 meses de antigüedad.");
        }
    }

    private async Task<IReadOnlyList<FarmPendingPorcineTransitionResponse>> BuildPendingPorcineTransitionsAsync(LivestockFarm farm, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        var births = await dbContext.AnimalBirths
            .AsNoTracking()
            .Include(entity => entity.PorcineTransitionDecision)
            .Where(entity => entity.LivestockFarmId == farm.Id && entity.BirthDate.AddMonths(PorcineTransitionSupport.DecisionAgeMonths) <= asOfDate)
            .OrderBy(entity => entity.BirthDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        var pendingBirths = births
            .Where(entity => entity.PorcineTransitionDecision is null)
            .ToList();
        if (pendingBirths.Count == 0)
        {
            return [];
        }

        var birthIds = pendingBirths.Select(entity => entity.Id).ToArray();
        var consumedByBirthId = await dbContext.Animals
            .AsNoTracking()
            .Where(entity =>
                entity.SourceBirthId != null &&
                birthIds.Contains(entity.SourceBirthId.Value) &&
                entity.RegistrationDate != null &&
                entity.RegistrationDate <= asOfDate)
            .GroupBy(entity => entity.SourceBirthId!.Value)
            .Select(entity => new { BirthId = entity.Key, Count = entity.Count() })
            .ToDictionaryAsync(entity => entity.BirthId, entity => entity.Count, cancellationToken);

        return pendingBirths
            .Select(entity => MapPendingPorcineTransition(
                entity,
                Math.Max(0, entity.OffspringNumber - consumedByBirthId.GetValueOrDefault(entity.Id)),
                asOfDate))
            .Where(entity => entity.PendingQuantity > 0)
            .ToList();
    }

    private static PorcineDecisionConsumption BuildPorcineDecisionConsumption(IEnumerable<Animal> animals)
    {
        var rears = 0;
        var sows = 0;
        var males = 0;

        foreach (var animal in animals)
        {
            switch (PorcineTransitionSupport.ResolveBranch(animal.Porcino?.AnimalType))
            {
                case PorcineTransitionBranch.Sows:
                    sows++;
                    break;
                case PorcineTransitionBranch.Males:
                    males++;
                    break;
                default:
                    rears++;
                    break;
            }
        }

        return new PorcineDecisionConsumption(rears, sows, males);
    }

    private static FarmPendingPorcineTransitionResponse MapPendingPorcineTransition(AnimalBirth birth, int pendingQuantity, DateOnly asOfDate)
    {
        var dueDate = PorcineTransitionSupport.GetDecisionDate(birth.BirthDate);
        var finalTransitionDate = PorcineTransitionSupport.GetFinalTransitionDate(birth.BirthDate);
        return new FarmPendingPorcineTransitionResponse(
            birth.Id,
            birth.LivestockFarmId,
            birth.BirthDate,
            dueDate,
            finalTransitionDate,
            pendingQuantity,
            finalTransitionDate <= asOfDate,
            birth.Observations);
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
            animal.Id,
            animal.LivestockFarmId,
            1,
            animal.Identification,
            EmptyToNull(animal.Porcino?.AnimalType),
            EmptyToNull(animal.Breed),
            EmptyToNull(animal.Sex),
            FarmCensusProjectionSupport.ResolveBirthYear(animal),
            animal.DischargeDate!.Value,
            animal.DischargeCause!.Value.ToString(),
            EmptyToNull(animal.DestinationCode));
    }

    private static FarmDeathResponse MapAggregateDeath(Balance balance)
    {
        return new FarmDeathResponse(
            balance.Id,
            null,
            balance.LivestockFarmId,
            balance.NumberOfAnimals,
            null,
            EmptyToNull(balance.Porcino?.Type),
            EmptyToNull(balance.Porcino?.Breed),
            null,
            null,
            balance.BalanceDate,
            balance.ModificationCause,
            EmptyToNull(balance.DestinationLivestockCode));
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
            0,
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

    private string NormalizeDeathDestinationCode(LivestockSpecies species, string? destinationCode)
    {
        return MerCodeSupport.NormalizeDeathDestinationCode(species, destinationCode, clock.UtcNow.Year);
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
