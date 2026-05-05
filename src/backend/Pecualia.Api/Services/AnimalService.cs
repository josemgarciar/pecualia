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
        string? search,
        string? species,
        string? sex,
        string? status,
        CancellationToken cancellationToken);

    Task<AnimalDetailResponse> GetAnimalAsync(long userId, UserRole role, long animalId, CancellationToken cancellationToken);

    Task<AnimalDetailResponse> CreateAnimalAsync(long userId, UserRole role, CreateAnimalRequest request, CancellationToken cancellationToken);

    Task<AnimalDetailResponse> DischargeAnimalAsync(long userId, UserRole role, long animalId, DischargeAnimalRequest request, CancellationToken cancellationToken);
}

public sealed class AnimalService(PecualiaDbContext dbContext) : IAnimalService
{
    public async Task<IReadOnlyList<AnimalListItemResponse>> GetAnimalsAsync(
        long userId,
        UserRole role,
        long? farmId,
        string? search,
        string? species,
        string? sex,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = BuildAccessibleAnimalQuery(userId, role).AsNoTracking();

        if (farmId is not null)
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

        var animals = await query
            .Include(entity => entity.LivestockFarm)
            .OrderBy(entity => entity.Identification)
            .ToListAsync(cancellationToken);

        return animals.Select(MapListItem).ToList();
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

        var identification = request.Identification.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(identification))
        {
            throw new DomainException("La identificación del animal es obligatoria.");
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
            : Normalize(request.DestinationCode);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAnimalAsync(userId, role, animal.Id, cancellationToken);
    }

    private IQueryable<Animal> BuildAccessibleAnimalQuery(long userId, UserRole role)
    {
        return role == UserRole.Manager
            ? dbContext.Animals.Where(entity => entity.LivestockFarm.Farmer.ManagerId == userId)
            : dbContext.Animals.Where(entity => entity.LivestockFarm.FarmerId == userId);
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
            EmptyToNull(animal.Sex),
            animal.BirthYear,
            animal.RegistrationDate,
            FormatRegistrationCause(animal.RegistrationCause),
            animal.DischargeDate,
            FormatDischargeCause(animal.DischargeCause),
            BuildStatus(animal));
    }

    private async Task<AnimalDetailResponse> MapDetailAsync(Animal animal, CancellationToken cancellationToken)
    {
        var ovinoCaprino = await dbContext.OvinoCaprinoAnimals
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.AnimalId == animal.Id, cancellationToken);
        var porcino = await dbContext.PorcinoAnimals
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.AnimalId == animal.Id, cancellationToken);

        return new AnimalDetailResponse(
            animal.Id,
            animal.LivestockFarmId,
            animal.Identification,
            animal.LivestockFarm.LivestockSpecies.ToString(),
            animal.LivestockFarm.Name,
            animal.LivestockFarm.RegaCode,
            EmptyToNull(animal.Breed),
            EmptyToNull(animal.Sex),
            animal.BirthYear,
            animal.RegistrationDate,
            FormatRegistrationCause(animal.RegistrationCause),
            EmptyToNull(animal.OriginCode),
            EmptyToNull(animal.HealthDocumentNumber),
            animal.DischargeDate,
            FormatDischargeCause(animal.DischargeCause),
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

    private static T ApplyCommonFields<T>(T animal, LivestockFarm farm, string identification, CreateAnimalRequest request)
        where T : Animal
    {
        animal.LivestockFarmId = farm.Id;
        animal.Identification = identification;
        animal.BirthYear = request.BirthYear;
        animal.Breed = Normalize(request.Breed);
        animal.Sex = Normalize(request.Sex);
        animal.RegistrationDate = request.RegistrationDate;
        animal.RegistrationCause = request.RegistrationCause;
        animal.OriginCode = Normalize(request.OriginCode);
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

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? FormatRegistrationCause(AnimalRegistrationCause? value) => value?.ToString();

    private static string? FormatDischargeCause(AnimalDischargeCause? value) => value?.ToString();
}
