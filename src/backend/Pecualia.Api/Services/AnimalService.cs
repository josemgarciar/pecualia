using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Animals;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

public interface IAnimalService
{
    Task<IReadOnlyList<AnimalListItemResponse>> GetAnimalsAsync(
        long userId,
        UserRole role,
        long? farmId,
        long? movementId,
        string? search,
        string? species,
        string? sex,
        string? status,
        CancellationToken cancellationToken);

    Task<AnimalPageResponse> GetFarmAnimalsPageAsync(
        long userId,
        UserRole role,
        long farmId,
        long? movementId,
        string? search,
        string? species,
        string? sex,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AnimalDetailResponse> GetAnimalAsync(long userId, UserRole role, long animalId, CancellationToken cancellationToken);

    Task<AnimalDetailResponse> CreateAnimalAsync(long userId, UserRole role, CreateAnimalRequest request, CancellationToken cancellationToken);

    Task<CreateAnimalsAutorrepositionResponse> CreateAutorrepositionAnimalsAsync(
        long userId,
        UserRole role,
        long farmId,
        CreateAnimalsAutorrepositionRequest request,
        CancellationToken cancellationToken);

    Task<AnimalDetailResponse> UpdateAnimalAsync(long userId, UserRole role, long animalId, UpdateAnimalRequest request, CancellationToken cancellationToken);

    Task<AnimalDetailResponse> DischargeAnimalAsync(long userId, UserRole role, long animalId, DischargeAnimalRequest request, CancellationToken cancellationToken);

    Task DeleteAnimalAsync(long userId, UserRole role, long animalId, CancellationToken cancellationToken);
}

public sealed class AnimalService(PecualiaDbContext dbContext) : IAnimalService
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private const string AutorrepositionBalanceCause = "Autorreposicion";

    public async Task<IReadOnlyList<AnimalListItemResponse>> GetAnimalsAsync(
        long userId,
        UserRole role,
        long? farmId,
        long? movementId,
        string? search,
        string? species,
        string? sex,
        string? status,
        CancellationToken cancellationToken)
    {
        var animals = await BuildFilteredAnimalQuery(userId, role, farmId, movementId, search, species, sex, status)
            .AsNoTracking()
            .Include(entity => entity.LivestockFarm)
            .Include(entity => entity.Porcino)
            .OrderBy(entity => entity.Identification)
            .ToListAsync(cancellationToken);

        return animals.Select(MapListItem).ToList();
    }

    public async Task<AnimalPageResponse> GetFarmAnimalsPageAsync(
        long userId,
        UserRole role,
        long farmId,
        long? movementId,
        string? search,
        string? species,
        string? sex,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedPageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        var filteredQuery = BuildFilteredAnimalQuery(userId, role, farmId, movementId, search, species, sex, status);
        var totalCount = await filteredQuery.CountAsync(cancellationToken);
        var activeCount = await filteredQuery.CountAsync(entity => entity.DischargeDate == null, cancellationToken);
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var normalizedPage = page <= 0 ? 1 : Math.Min(page, totalPages);

        var animals = await filteredQuery
            .AsNoTracking()
            .Include(entity => entity.LivestockFarm)
            .Include(entity => entity.Porcino)
            .OrderBy(entity => entity.Identification)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var mappedAnimals = animals.Select(MapListItem).ToList();
        mappedAnimals = await PopulateGuideSeriesAsync(mappedAnimals, farmId, cancellationToken);

        return new AnimalPageResponse(
            mappedAnimals,
            totalCount,
            activeCount,
            normalizedPage,
            normalizedPageSize);
    }

    public async Task<AnimalDetailResponse> GetAnimalAsync(long userId, UserRole role, long animalId, CancellationToken cancellationToken)
    {
        var animal = await BuildAccessibleAnimalQuery(userId, role)
            .AsNoTracking()
            .Include(entity => entity.LivestockFarm)
            .SingleOrDefaultAsync(entity => entity.Id == animalId, cancellationToken);

        if (animal is null)
        {
            throw new DomainException("Animal no encontrado.");
        }

        return await MapDetailAsync(animal, cancellationToken);
    }

    public async Task<AnimalDetailResponse> CreateAnimalAsync(long userId, UserRole role, CreateAnimalRequest request, CancellationToken cancellationToken)
    {
        var farm = await BuildAccessibleFarmQuery(userId, role)
            .SingleOrDefaultAsync(entity => entity.Id == request.FarmId, cancellationToken);

        if (farm is null)
        {
            throw new DomainException("Explotación no encontrada.");
        }

        var identification = DomainValidators.NormalizeAnimalIdentification(request.Identification);
        if (string.IsNullOrWhiteSpace(identification))
        {
            throw new DomainException("La identificación del animal es obligatoria.");
        }

        if (!DomainValidators.IsValidAnimalIdentification(farm.LivestockSpecies, identification))
        {
            throw new DomainException(BuildAnimalIdentificationFormatMessage(farm.LivestockSpecies));
        }

        if (await dbContext.Animals.AnyAsync(entity => entity.Identification == identification, cancellationToken))
        {
            throw new DomainException("Ya existe un animal con esa identificación.");
        }

        var animal = BuildBaseAnimal(farm, identification, request);

        dbContext.Animals.Add(animal);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            dbContext.PorcinoAnimals.Add(BuildPorcinoAnimal(animal.Id, request));
        }
        else
        {
            dbContext.OvinoCaprinoAnimals.Add(BuildOvinoCaprinoAnimal(animal.Id, farm, request));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAnimalAsync(userId, role, animal.Id, cancellationToken);
    }

    public async Task<CreateAnimalsAutorrepositionResponse> CreateAutorrepositionAnimalsAsync(
        long userId,
        UserRole role,
        long farmId,
        CreateAnimalsAutorrepositionRequest request,
        CancellationToken cancellationToken)
    {
        var farm = await BuildAccessibleFarmQuery(userId, role)
            .SingleOrDefaultAsync(entity => entity.Id == farmId, cancellationToken);

        if (farm is null)
        {
            throw new DomainException("Explotación no encontrada.");
        }

        if (string.IsNullOrWhiteSpace(request.StartIdentification))
        {
            throw new DomainException("La identificación inicial es obligatoria.");
        }

        if (request.Quantity <= 0)
        {
            throw new DomainException("El número de animales debe ser mayor que cero.");
        }

        if (string.IsNullOrWhiteSpace(request.Breed))
        {
            throw new DomainException("La raza es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(request.Sex))
        {
            throw new DomainException("El sexo es obligatorio.");
        }

        if (request.RegistrationDate is null)
        {
            throw new DomainException("La fecha de alta es obligatoria.");
        }

        var eligibleBirths = await dbContext.AnimalBirths
            .Where(entity =>
                entity.LivestockFarmId == farm.Id &&
                entity.BirthDate.AddMonths(4) < request.RegistrationDate.Value)
            .OrderBy(entity => entity.BirthDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        if (eligibleBirths.Count == 0)
        {
            throw new DomainException("No hay animales no reproductores sin identificar con más de 4 meses disponibles para autoreposición.");
        }

        var eligibleBirthIds = eligibleBirths.Select(entity => entity.Id).ToArray();
        var consumedByBirthId = await dbContext.Animals
            .Where(entity =>
                entity.SourceBirthId != null &&
                eligibleBirthIds.Contains(entity.SourceBirthId.Value) &&
                (entity.RegistrationDate == null || entity.RegistrationDate <= request.RegistrationDate.Value))
            .GroupBy(entity => entity.SourceBirthId!.Value)
            .Select(entity => new { BirthId = entity.Key, Count = entity.Count() })
            .ToDictionaryAsync(entity => entity.BirthId, entity => entity.Count, cancellationToken);

        var availableUnits = eligibleBirths
            .SelectMany(entity => Enumerable.Repeat(
                new AllocatedBirthUnit(entity.Id, entity.BirthDate),
                Math.Max(0, entity.OffspringNumber - consumedByBirthId.GetValueOrDefault(entity.Id))))
            .ToList();

        if (request.Quantity > availableUnits.Count)
        {
            throw new DomainException("No puedes autoreponer más animales que los no reproductores sin identificar disponibles en el censo.");
        }

        var identifications = BuildConsecutiveIdentifications(farm.LivestockSpecies, request.StartIdentification, request.Quantity);
        var existingIdentifications = await dbContext.Animals
            .Where(entity => identifications.Contains(entity.Identification))
            .Select(entity => entity.Identification)
            .OrderBy(entity => entity)
            .ToListAsync(cancellationToken);

        if (existingIdentifications.Count > 0)
        {
            throw new DomainException($"Ya existen identificaciones dentro del rango indicado: {string.Join(", ", existingIdentifications.Take(5))}.");
        }

        var createRequest = new CreateAnimalRequest(
            farmId,
            string.Empty,
            availableUnits[0].BirthDate.Year,
            request.Breed,
            request.Sex,
            request.RegistrationDate,
            AnimalRegistrationCause.Autorreposicion,
            null,
            null,
            request.OvinoCaprino,
            request.Porcino);

        var allocatedUnits = availableUnits.Take(request.Quantity).ToList();
        var animals = identifications
            .Select((identification, index) =>
            {
                var animal = BuildBaseAnimal(farm, identification, createRequest);
                animal.BirthDate = allocatedUnits[index].BirthDate;
                animal.BirthYear = allocatedUnits[index].BirthDate.Year;
                animal.SourceBirthId = allocatedUnits[index].BirthId;
                return animal;
            })
            .ToList();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Animals.AddRange(animals);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            var porcinoAnimals = animals
                .Select(animal => BuildPorcinoAnimal(animal.Id, createRequest))
                .ToList();
            dbContext.PorcinoAnimals.AddRange(porcinoAnimals);
        }
        else
        {
            var ovinoCaprinoAnimals = animals
                .Select(animal => BuildOvinoCaprinoAnimal(animal.Id, farm, createRequest))
                .ToList();
            dbContext.OvinoCaprinoAnimals.AddRange(ovinoCaprinoAnimals);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var balance = new Balance
        {
            LivestockFarmId = farm.Id,
            BalanceDate = request.RegistrationDate.Value,
            ModificationCause = AutorrepositionBalanceCause,
            NumberOfAnimals = animals.Count
        };

        dbContext.Balances.Add(balance);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            dbContext.BalancePorcino.Add(new BalancePorcino
            {
                BalanceId = balance.Id,
                Baits = 0,
                Boars = 0,
                Breed = animals.Select(entity => entity.Breed).FirstOrDefault(entity => !string.IsNullOrWhiteSpace(entity)),
                Piglets = 0,
                PigsReposition = FarmCensusProjectionSupport.NormalizeType(request.Porcino?.AnimalType)?.Contains("sow", StringComparison.Ordinal) == true ||
                    FarmCensusProjectionSupport.NormalizeType(request.Porcino?.AnimalType)?.Contains("cerda", StringComparison.Ordinal) == true
                    ? 0
                    : animals.Count,
                Rear = 0,
                SowsForLive = 0,
                SowsReposition = FarmCensusProjectionSupport.NormalizeType(request.Porcino?.AnimalType)?.Contains("sow", StringComparison.Ordinal) == true ||
                    FarmCensusProjectionSupport.NormalizeType(request.Porcino?.AnimalType)?.Contains("cerda", StringComparison.Ordinal) == true
                    ? animals.Count
                    : 0,
                Tag = request.Porcino?.Tag,
                Type = request.Porcino?.AnimalType?.Trim()
            });
        }
        else
        {
            var normalizedSex = FarmCensusProjectionSupport.NormalizeSex(request.Sex);
            dbContext.BalanceOvinoCaprino.Add(new BalanceOvinoCaprino
            {
                BalanceId = balance.Id,
                NonReproductiveBetween4And12Months = 0,
                NonReproductiveUnder4Months = 0,
                ReproductiveFemales = normalizedSex == "female" ? animals.Count : 0,
                ReproductiveMales = normalizedSex == "male" ? animals.Count : 0
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreateAnimalsAutorrepositionResponse(
            animals.Count,
            animals[0].Identification,
            animals[^1].Identification);
    }

    public async Task<AnimalDetailResponse> UpdateAnimalAsync(long userId, UserRole role, long animalId, UpdateAnimalRequest request, CancellationToken cancellationToken)
    {
        var animal = await BuildAccessibleAnimalQuery(userId, role)
            .Include(entity => entity.LivestockFarm)
            .Include(entity => entity.OvinoCaprino)
            .Include(entity => entity.Porcino)
            .SingleOrDefaultAsync(entity => entity.Id == animalId, cancellationToken);

        if (animal is null)
        {
            throw new DomainException("Animal no encontrado.");
        }

        var identification = DomainValidators.NormalizeAnimalIdentification(request.Identification);
        if (string.IsNullOrWhiteSpace(identification))
        {
            throw new DomainException("La identificación del animal es obligatoria.");
        }

        if (!DomainValidators.IsValidAnimalIdentification(animal.LivestockFarm.LivestockSpecies, identification))
        {
            throw new DomainException(BuildAnimalIdentificationFormatMessage(animal.LivestockFarm.LivestockSpecies));
        }

        if (await dbContext.Animals.AnyAsync(entity => entity.Id != animalId && entity.Identification == identification, cancellationToken))
        {
            throw new DomainException("Ya existe un animal con esa identificación.");
        }

        ApplyCommonFields(animal, animal.LivestockFarm, identification, request);

        if (animal.LivestockFarm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            if (request.Porcino is null || string.IsNullOrWhiteSpace(request.Porcino.AnimalType))
            {
                throw new DomainException("El tipo porcino no coincide con la especie de la explotación");
            }

            if (animal.Porcino is null)
            {
                animal.Porcino = BuildPorcinoAnimal(animal.Id, request);
            }
            else
            {
                animal.Porcino.AnimalType = request.Porcino.AnimalType.Trim();
                animal.Porcino.IdentificationDate = request.Porcino.IdentificationDate;
                animal.Porcino.PigRegistrationNumber = Normalize(request.Porcino.PigRegistrationNumber);
                animal.Porcino.Tag = Normalize(request.Porcino.Tag);
            }
        }
        else
        {
            var speciesType = request.OvinoCaprino?.SpeciesType ?? animal.LivestockFarm.LivestockSpecies;
            if (speciesType is not (LivestockSpecies.Ovine or LivestockSpecies.Caprine) || speciesType != animal.LivestockFarm.LivestockSpecies)
            {
                throw new DomainException("El tipo ovino/caprino no coincide con la especie de la explotación.");
            }

            if (animal.OvinoCaprino is null)
            {
                animal.OvinoCaprino = BuildOvinoCaprinoAnimal(animal.Id, animal.LivestockFarm, new CreateAnimalRequest(
                    animal.LivestockFarmId,
                    identification,
                    request.BirthYear,
                    request.Breed,
                    request.Sex,
                    request.RegistrationDate,
                    request.RegistrationCause,
                    request.OriginCode,
                    request.HealthDocumentNumber,
                    request.OvinoCaprino,
                    null));
            }
            else
            {
                animal.OvinoCaprino.SpeciesType = speciesType;
                animal.OvinoCaprino.Genotyping = Normalize(request.OvinoCaprino?.Genotyping);
                animal.OvinoCaprino.DominantAllele = Normalize(request.OvinoCaprino?.DominantAllele);
                animal.OvinoCaprino.LowAllele = Normalize(request.OvinoCaprino?.LowAllele);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAnimalAsync(userId, role, animal.Id, cancellationToken);
    }

    public async Task<AnimalDetailResponse> DischargeAnimalAsync(long userId, UserRole role, long animalId, DischargeAnimalRequest request, CancellationToken cancellationToken)
    {
        var animal = await BuildAccessibleAnimalQuery(userId, role)
            .Include(entity => entity.LivestockFarm)
            .SingleOrDefaultAsync(entity => entity.Id == animalId, cancellationToken);

        if (animal is null)
        {
            throw new DomainException("Animal no encontrado.");
        }

        if (animal.DischargeDate is not null)
        {
            throw new DomainException("El animal ya está dado de baja.");
        }

        if (request.DischargeCause is not (AnimalDischargeCause.Salida or AnimalDischargeCause.Muerte))
        {
            throw new DomainException("La causa de baja es obligatoria.");
        }

        animal.DischargeDate = request.DischargeDate;
        animal.DischargeCause = request.DischargeCause;
        animal.DestinationCode = request.DischargeCause == AnimalDischargeCause.Muerte
            ? NormalizeDeathDestinationCode(animal.LivestockFarm.LivestockSpecies, request.DestinationCode)
            : NormalizeRegaDestinationCode(request.DestinationCode);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAnimalAsync(userId, role, animal.Id, cancellationToken);
    }

    public async Task DeleteAnimalAsync(long userId, UserRole role, long animalId, CancellationToken cancellationToken)
    {
        var animal = await BuildAccessibleAnimalQuery(userId, role)
            .Include(entity => entity.MovementCertificates)
            .SingleOrDefaultAsync(entity => entity.Id == animalId, cancellationToken);

        if (animal is null)
        {
            throw new DomainException("Animal no encontrado.");
        }

        if (animal.MovementCertificates.Count > 0)
        {
            throw new DomainException("No se puede eliminar un animal vinculado a movimientos registrados.");
        }

        dbContext.Animals.Remove(animal);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Animal> BuildAccessibleAnimalQuery(long userId, UserRole role)
    {
        return role == UserRole.Manager
            ? dbContext.Animals.Where(entity => entity.LivestockFarm.Farmer.ManagerId == userId)
            : dbContext.Animals.Where(entity => entity.LivestockFarm.FarmerId == userId);
    }

    private IQueryable<Animal> BuildFilteredAnimalQuery(
        long userId,
        UserRole role,
        long? farmId,
        long? movementId,
        string? search,
        string? species,
        string? sex,
        string? status)
    {
        var query = BuildAccessibleAnimalQuery(userId, role);

        if (movementId is not null && farmId is not null)
        {
            query = query.Where(entity =>
                dbContext.MovementCertificateAnimals.Any(link =>
                    link.AnimalId == entity.Id &&
                    link.MovementCertificateId == movementId.Value &&
                    (link.MovementCertificate.OriginLivestockId == farmId.Value || link.MovementCertificate.DestinationLivestockId == farmId.Value)));
        }
        else if (farmId is not null)
        {
            query = query.Where(entity => entity.LivestockFarmId == farmId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(entity =>
                entity.Identification.ToLower().Contains(normalizedSearch) ||
                (entity.Breed != null && entity.Breed.ToLower().Contains(normalizedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(species) && Enum.TryParse<LivestockSpecies>(species, true, out var speciesFilter))
        {
            query = query.Where(entity => entity.LivestockFarm.LivestockSpecies == speciesFilter);
        }

        if (!string.IsNullOrWhiteSpace(sex))
        {
            var normalizedSex = sex.Trim();
            query = query.Where(entity => entity.Sex == normalizedSex);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = status.Trim().Equals("Discharged", StringComparison.OrdinalIgnoreCase)
                ? query.Where(entity => entity.DischargeDate != null)
                : query.Where(entity => entity.DischargeDate == null);
        }

        return query;
    }

    private IQueryable<LivestockFarm> BuildAccessibleFarmQuery(long userId, UserRole role)
    {
        return role == UserRole.Manager
            ? dbContext.Farms.Where(entity => entity.Farmer.ManagerId == userId)
            : dbContext.Farms.Where(entity => entity.FarmerId == userId);
    }

    private static AnimalListItemResponse MapListItem(Animal animal)
    {
        return new AnimalListItemResponse(
            animal.Id,
            animal.LivestockFarmId,
            animal.Identification,
            animal.LivestockFarm.LivestockSpecies.ToString(),
            animal.LivestockFarm.Name,
            EmptyToNull(animal.Breed),
            BookDocumentSupport.MapBreedCode(animal.LivestockFarm.LivestockSpecies, animal.Breed),
            EmptyToNull(animal.Sex),
            BookDocumentSupport.MapSexCode(animal.Sex),
            FarmCensusProjectionSupport.ResolveBirthYear(animal),
            animal.LivestockFarm.LivestockSpecies == LivestockSpecies.Porcine ? animal.Porcino?.IdentificationDate ?? animal.RegistrationDate : animal.RegistrationDate,
            animal.RegistrationDate,
            FormatRegistrationCause(animal.RegistrationCause),
            BookDocumentSupport.MapRegistrationCauseCode(animal.RegistrationCause),
            EmptyToNull(animal.OriginCode),
            animal.DischargeDate,
            FormatDischargeCause(animal.DischargeCause),
            BookDocumentSupport.MapDischargeCauseCode(animal.DischargeCause),
            EmptyToNull(animal.DestinationCode),
            EmptyToNull(animal.HealthDocumentNumber),
            null,
            null,
            BuildStatus(animal));
    }

    private async Task<List<AnimalListItemResponse>> PopulateGuideSeriesAsync(
        List<AnimalListItemResponse> animals,
        long farmId,
        CancellationToken cancellationToken)
    {
        if (animals.Count == 0)
        {
            return animals;
        }

        var animalIds = animals.Select(entity => entity.Id).ToArray();
        var guideSeriesByAnimal = await dbContext.MovementCertificateAnimals
            .AsNoTracking()
            .Where(entity => animalIds.Contains(entity.AnimalId))
            .Include(entity => entity.MovementCertificate)
            .Select(entity => new
            {
                entity.AnimalId,
                entity.MovementCertificate.Serie,
                entity.MovementCertificate.DepartureDate,
                IsEntry = entity.MovementCertificate.DestinationLivestockId == farmId,
                IsExit = entity.MovementCertificate.OriginLivestockId == farmId
            })
            .ToListAsync(cancellationToken);

        var lookup = guideSeriesByAnimal
            .GroupBy(entity => entity.AnimalId)
            .ToDictionary(
                entity => entity.Key,
                entity => new
                {
                    Entry = entity
                        .Where(item => item.IsEntry && !string.IsNullOrWhiteSpace(item.Serie))
                        .OrderByDescending(item => item.DepartureDate)
                        .Select(item => item.Serie)
                        .FirstOrDefault(),
                    Exit = entity
                        .Where(item => item.IsExit && !string.IsNullOrWhiteSpace(item.Serie))
                        .OrderByDescending(item => item.DepartureDate)
                        .Select(item => item.Serie)
                        .FirstOrDefault()
                });

        return animals
            .Select(entity => lookup.TryGetValue(entity.Id, out var series)
                ? entity with { EntryGuideSerie = series.Entry, ExitGuideSerie = series.Exit }
                : entity)
            .ToList();
    }

    private async Task<AnimalDetailResponse> MapDetailAsync(Animal animal, CancellationToken cancellationToken)
    {
        var ovinoCaprino = await dbContext.OvinoCaprinoAnimals
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.AnimalId == animal.Id, cancellationToken);
        var porcino = await dbContext.PorcinoAnimals
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.AnimalId == animal.Id, cancellationToken);
        var entryGuideSerie = await dbContext.MovementCertificateAnimals
            .AsNoTracking()
            .Where(entity =>
                entity.AnimalId == animal.Id &&
                entity.MovementCertificate.DestinationLivestockId == animal.LivestockFarmId &&
                !string.IsNullOrWhiteSpace(entity.MovementCertificate.Serie))
            .OrderByDescending(entity => entity.MovementCertificate.DepartureDate)
            .Select(entity => entity.MovementCertificate.Serie)
            .FirstOrDefaultAsync(cancellationToken);

        return new AnimalDetailResponse(
            animal.Id,
            animal.LivestockFarmId,
            animal.Identification,
            animal.LivestockFarm.LivestockSpecies.ToString(),
            animal.LivestockFarm.Name,
            animal.LivestockFarm.RegaCode,
            EmptyToNull(animal.Breed),
            EmptyToNull(animal.Sex),
            FarmCensusProjectionSupport.ResolveBirthYear(animal),
            animal.RegistrationDate,
            FormatRegistrationCause(animal.RegistrationCause),
            animal.RegistrationCause?.ToString(),
            EmptyToNull(animal.OriginCode),
            EmptyToNull(animal.HealthDocumentNumber),
            EmptyToNull(entryGuideSerie),
            animal.DischargeDate,
            FormatDischargeCause(animal.DischargeCause),
            animal.DischargeCause?.ToString(),
            EmptyToNull(animal.DestinationCode),
            BuildStatus(animal),
            ovinoCaprino is null
                ? null
                : new OvinoCaprinoAnimalResponse(
                    ovinoCaprino.SpeciesType.ToString(),
                    EmptyToNull(ovinoCaprino.Genotyping),
                    EmptyToNull(ovinoCaprino.DominantAllele),
                    EmptyToNull(ovinoCaprino.LowAllele)),
            porcino is null
                ? null
                : new PorcinoAnimalResponse(
                    porcino.AnimalType,
                    porcino.IdentificationDate,
                    EmptyToNull(porcino.PigRegistrationNumber),
                    EmptyToNull(porcino.Tag)));
    }

    private static Animal BuildBaseAnimal(LivestockFarm farm, string identification, CreateAnimalRequest request)
    {
        if (farm.LivestockSpecies == LivestockSpecies.Porcine && request.OvinoCaprino is not null)
        {
            throw new DomainException("No puedes registrar datos ovino/caprino en una explotación porcina.");
        }

        if (farm.LivestockSpecies != LivestockSpecies.Porcine && request.Porcino is not null)
        {
            throw new DomainException("No puedes registrar datos porcinos en una explotación ovina/caprina.");
        }

        return ApplyCommonFields(new Animal(), farm, identification, request);
    }

    private static List<string> BuildConsecutiveIdentifications(LivestockSpecies species, string startIdentification, int numberOfAnimals)
    {
        var normalizedIdentification = DomainValidators.NormalizeAnimalIdentification(startIdentification);
        if (string.IsNullOrWhiteSpace(normalizedIdentification))
        {
            throw new DomainException("La identificación inicial es obligatoria.");
        }

        if (!DomainValidators.IsValidAnimalIdentification(species, normalizedIdentification))
        {
            throw new DomainException(BuildAnimalIdentificationFormatMessage(species));
        }

        var numericStartIndex = normalizedIdentification.Length;
        while (numericStartIndex > 0 && char.IsDigit(normalizedIdentification[numericStartIndex - 1]))
        {
            numericStartIndex--;
        }

        if (numericStartIndex == normalizedIdentification.Length)
        {
            throw new DomainException("La identificación inicial debe terminar en una parte numérica consecutiva.");
        }

        var prefix = normalizedIdentification[..numericStartIndex];
        var numericPart = normalizedIdentification[numericStartIndex..];
        if (!BigInteger.TryParse(numericPart, out var startNumber))
        {
            throw new DomainException("La identificación inicial no tiene un formato válido.");
        }

        var width = numericPart.Length;
        var identifications = new List<string>(numberOfAnimals);

        for (var offset = 0; offset < numberOfAnimals; offset++)
        {
            var currentNumber = startNumber + offset;
            var paddedNumber = currentNumber.ToString().PadLeft(width, '0');
            if (paddedNumber.Length > width)
            {
                throw new DomainException("El rango solicitado desborda la longitud numérica de la identificación inicial.");
            }

            var generatedIdentification = $"{prefix}{paddedNumber}";
            if (!DomainValidators.IsValidAnimalIdentification(species, generatedIdentification))
            {
                throw new DomainException(BuildAnimalIdentificationFormatMessage(species));
            }

            identifications.Add(generatedIdentification);
        }

        return identifications;
    }

    private static OvinoCaprinoAnimal BuildOvinoCaprinoAnimal(long animalId, LivestockFarm farm, CreateAnimalRequest request)
    {
        var speciesType = request.OvinoCaprino?.SpeciesType ?? farm.LivestockSpecies;
        if (speciesType is not (LivestockSpecies.Ovine or LivestockSpecies.Caprine) || speciesType != farm.LivestockSpecies)
        {
            throw new DomainException("El tipo ovino/caprino no coincide con la especie de la explotación.");
        }

        return new OvinoCaprinoAnimal
        {
            AnimalId = animalId,
            SpeciesType = speciesType,
            Genotyping = Normalize(request.OvinoCaprino?.Genotyping),
            DominantAllele = Normalize(request.OvinoCaprino?.DominantAllele),
            LowAllele = Normalize(request.OvinoCaprino?.LowAllele)
        };
    }

    private static PorcinoAnimal BuildPorcinoAnimal(long animalId, CreateAnimalRequest request)
    {
        if (request.Porcino is null || string.IsNullOrWhiteSpace(request.Porcino.AnimalType))
        {
            throw new DomainException("El tipo porcino no coincide con la especie de la explotación");
        }

        return new PorcinoAnimal
        {
            AnimalId = animalId,
            AnimalType = request.Porcino.AnimalType.Trim(),
            IdentificationDate = request.Porcino.IdentificationDate,
            PigRegistrationNumber = Normalize(request.Porcino.PigRegistrationNumber),
            Tag = Normalize(request.Porcino.Tag)
        };
    }

    private static PorcinoAnimal BuildPorcinoAnimal(long animalId, UpdateAnimalRequest request)
    {
        if (request.Porcino is null || string.IsNullOrWhiteSpace(request.Porcino.AnimalType))
        {
            throw new DomainException("El tipo porcino no coincide con la especie de la explotación");
        }

        return new PorcinoAnimal
        {
            AnimalId = animalId,
            AnimalType = request.Porcino.AnimalType.Trim(),
            IdentificationDate = request.Porcino.IdentificationDate,
            PigRegistrationNumber = Normalize(request.Porcino.PigRegistrationNumber),
            Tag = Normalize(request.Porcino.Tag)
        };
    }

    private static T ApplyCommonFields<T>(T animal, LivestockFarm farm, string identification, CreateAnimalRequest request)
        where T : Animal
    {
        animal.LivestockFarmId = farm.Id;
        animal.Identification = identification;
        animal.BirthYear = request.BirthYear;
        animal.BirthDate = request.BirthYear is null ? null : new DateOnly(request.BirthYear.Value, 1, 1);
        animal.Breed = Normalize(request.Breed);
        animal.Sex = Normalize(request.Sex);
        animal.RegistrationDate = request.RegistrationDate;
        animal.RegistrationCause = request.RegistrationCause;
        animal.OriginCode = NormalizeRegaOriginCode(request.OriginCode);
        animal.HealthDocumentNumber = Normalize(request.HealthDocumentNumber);
        return animal;
    }

    private static T ApplyCommonFields<T>(T animal, LivestockFarm farm, string identification, UpdateAnimalRequest request)
        where T : Animal
    {
        animal.LivestockFarmId = farm.Id;
        animal.Identification = identification;
        animal.BirthYear = request.BirthYear;
        animal.BirthDate = request.BirthYear is null ? null : new DateOnly(request.BirthYear.Value, 1, 1);
        animal.Breed = Normalize(request.Breed);
        animal.Sex = Normalize(request.Sex);
        animal.RegistrationDate = request.RegistrationDate;
        animal.RegistrationCause = request.RegistrationCause;
        animal.OriginCode = NormalizeRegaOriginCode(request.OriginCode);
        animal.HealthDocumentNumber = Normalize(request.HealthDocumentNumber);
        return animal;
    }

    private static string BuildStatus(Animal animal) => animal.DischargeDate is null ? "Active" : "Discharged";

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeDeathDestinationCode(LivestockSpecies species, string? destinationCode)
    {
        var normalizedDestinationCode = Normalize(destinationCode)?.ToUpperInvariant();

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

    private static string? NormalizeRegaOriginCode(string? originCode)
    {
        var normalizedOriginCode = Normalize(originCode)?.ToUpperInvariant();
        if (normalizedOriginCode is null)
        {
            return null;
        }

        if (!DomainValidators.IsValidRegaCode(normalizedOriginCode))
        {
            throw new DomainException("El código REGA de origen no es válido. Debe seguir el formato ES seguido de 12 dígitos.");
        }

        return normalizedOriginCode;
    }

    private static string? NormalizeRegaDestinationCode(string? destinationCode)
    {
        var normalizedDestinationCode = Normalize(destinationCode)?.ToUpperInvariant();
        if (normalizedDestinationCode is null)
        {
            return null;
        }

        if (!DomainValidators.IsValidRegaCode(normalizedDestinationCode))
        {
            throw new DomainException("El código REGA de destino no es válido. Debe seguir el formato ES seguido de 12 dígitos.");
        }

        return normalizedDestinationCode;
    }

    private static string BuildAnimalIdentificationFormatMessage(LivestockSpecies species)
    {
        return species == LivestockSpecies.Porcine
            ? "La identificación del animal no es válida. Para porcino se admite ES seguido de 12 dígitos o GT seguido de números."
            : "La identificación del animal no es válida. Para ovino/caprino se admite ES seguido de 12 dígitos o ES seguido de 12 dígitos y un sufijo.";
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? FormatRegistrationCause(AnimalRegistrationCause? value) => value?.ToString();

    private static string? FormatDischargeCause(AnimalDischargeCause? value) => value?.ToString();

    private sealed record AllocatedBirthUnit(long BirthId, DateOnly BirthDate);
}
