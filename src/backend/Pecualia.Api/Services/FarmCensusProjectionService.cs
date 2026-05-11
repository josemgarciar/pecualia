using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

public interface IFarmCensusProjectionService
{
    Task<FarmCensusResponse> BuildSnapshotAsync(LivestockFarm farm, DateOnly asOfDate, CancellationToken cancellationToken);

    Task<FarmCensusResponse> BuildCensusResponseAsync(LivestockFarm farm, int year, DateOnly asOfDate, CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> GetAvailableYearsAsync(long farmId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Census>> BuildBookCensusesAsync(LivestockFarm farm, CancellationToken cancellationToken);
}

public sealed class FarmCensusProjectionService(PecualiaDbContext dbContext, IClock clock) : IFarmCensusProjectionService
{
    public async Task<FarmCensusResponse> BuildSnapshotAsync(LivestockFarm farm, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        var projection = await BuildProjectionAsync(farm, asOfDate, cancellationToken);
        return CreateResponse(farm, asOfDate.Year, projection, []);
    }

    public async Task<FarmCensusResponse> BuildCensusResponseAsync(LivestockFarm farm, int year, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        var projection = await BuildProjectionAsync(farm, asOfDate, cancellationToken);
        var availableYears = await GetAvailableYearsAsync(farm.Id, cancellationToken);
        return CreateResponse(farm, year, projection, availableYears);
    }

    public async Task<IReadOnlyList<int>> GetAvailableYearsAsync(long farmId, CancellationToken cancellationToken)
    {
        var birthYears = await dbContext.AnimalBirths
            .AsNoTracking()
            .Where(entity => entity.LivestockFarmId == farmId)
            .Select(entity => entity.BirthDate.Year)
            .ToListAsync(cancellationToken);

        var balanceYears = await dbContext.Balances
            .AsNoTracking()
            .Where(entity => entity.LivestockFarmId == farmId)
            .Select(entity => entity.BalanceDate.Year)
            .ToListAsync(cancellationToken);

        var censusYears = await dbContext.Census
            .AsNoTracking()
            .Where(entity => entity.LivestockFarmId == farmId)
            .Select(entity => entity.CensusDate.Year)
            .ToListAsync(cancellationToken);

        var animalYears = await dbContext.Animals
            .AsNoTracking()
            .Where(entity =>
                entity.LivestockFarmId == farmId ||
                (entity.SourceBirthId != null && entity.SourceBirth!.LivestockFarmId == farmId))
            .Select(entity => new
            {
                entity.BirthDate,
                entity.BirthYear,
                entity.RegistrationDate,
                entity.DischargeDate
            })
            .ToListAsync(cancellationToken);

        return birthYears
            .Concat(balanceYears)
            .Concat(censusYears)
            .Concat(animalYears.SelectMany(entity => new[]
            {
                entity.BirthDate?.Year,
                entity.BirthYear,
                entity.RegistrationDate?.Year,
                entity.DischargeDate?.Year
            }).Where(entity => entity is not null).Select(entity => entity!.Value))
            .Append(clock.UtcNow.Year)
            .Distinct()
            .OrderByDescending(entity => entity)
            .ToList();
    }

    public async Task<IReadOnlyList<Census>> BuildBookCensusesAsync(LivestockFarm farm, CancellationToken cancellationToken)
    {
        var years = await GetAvailableYearsAsync(farm.Id, cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow.Date);
        var censuses = new List<Census>(years.Count);

        foreach (var year in years.OrderBy(entity => entity))
        {
            var asOfDate = year == today.Year ? today : new DateOnly(year, 12, 31);
            var projection = await BuildProjectionAsync(farm, asOfDate, cancellationToken);
            censuses.Add(CreateSyntheticCensus(farm, asOfDate, projection));
        }

        return censuses;
    }

    private async Task<FarmCensusProjection> BuildProjectionAsync(LivestockFarm farm, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        var asOfExclusiveUtc = DateTime.SpecifyKind(asOfDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        var animals = await dbContext.Animals
            .AsNoTracking()
            .Include(entity => entity.Porcino)
            .Where(entity =>
                entity.LivestockFarmId == farm.Id &&
                (entity.RegistrationDate == null || entity.RegistrationDate <= asOfDate) &&
                (entity.DischargeDate == null || entity.DischargeDate > asOfDate))
            .ToListAsync(cancellationToken);

        var births = await dbContext.AnimalBirths
            .AsNoTracking()
            .Where(entity => entity.LivestockFarmId == farm.Id && entity.BirthDate <= asOfDate)
            .OrderBy(entity => entity.BirthDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        var unidentifiedMovements = await dbContext.MovementCertificates
            .AsNoTracking()
            .Where(entity =>
                entity.UnidentifiedCategory != null &&
                entity.Specie != LivestockSpecies.Porcine.ToString() &&
                (
                    (entity.OriginLivestockId == farm.Id && entity.DepartureDate < asOfExclusiveUtc) ||
                    (entity.DestinationLivestockId == farm.Id && (entity.ArrivalDate ?? entity.DepartureDate) < asOfExclusiveUtc)
                ))
            .ToListAsync(cancellationToken);

        var consumedUnidentifiedMovementsByAutorreposition = farm.LivestockSpecies == LivestockSpecies.Porcine
            ? 0
            : await dbContext.Animals
                .AsNoTracking()
                .Where(entity =>
                    entity.LivestockFarmId == farm.Id &&
                    entity.RegistrationCause == AnimalRegistrationCause.Autorreposicion &&
                    entity.SourceBirthId == null &&
                    entity.RegistrationDate != null &&
                    entity.RegistrationDate <= asOfDate)
                .CountAsync(cancellationToken);

        var birthIds = births.Select(entity => entity.Id).ToArray();
        var consumedByBirthId = birthIds.Length == 0
            ? new Dictionary<long, int>()
            : await dbContext.Animals
                .AsNoTracking()
                .Where(entity =>
                    entity.SourceBirthId != null &&
                    birthIds.Contains(entity.SourceBirthId.Value) &&
                    (entity.RegistrationDate == null || entity.RegistrationDate <= asOfDate))
                .GroupBy(entity => entity.SourceBirthId!.Value)
                .Select(entity => new { BirthId = entity.Key, Count = entity.Count() })
                .ToDictionaryAsync(entity => entity.BirthId, entity => entity.Count, cancellationToken);

        var projection = new FarmCensusProjection();

        foreach (var animal in animals)
        {
            if (farm.LivestockSpecies == LivestockSpecies.Porcine)
            {
                AccumulatePorcineAnimal(projection, animal, asOfDate);
            }
            else
            {
                AccumulateOvineOrCaprineAnimal(projection, animal, asOfDate);
            }
        }

        foreach (var birth in births)
        {
            var consumed = consumedByBirthId.GetValueOrDefault(birth.Id);
            var available = Math.Max(0, birth.OffspringNumber - consumed);

            if (available == 0)
            {
                continue;
            }

            if (farm.LivestockSpecies == LivestockSpecies.Porcine)
            {
                if (FarmCensusProjectionSupport.IsYoungerThanMonths(birth.BirthDate, asOfDate, 4))
                {
                    projection.Piglets += available;
                }
                else
                {
                    projection.Rears += available;
                }
            }
            else
            {
                if (FarmCensusProjectionSupport.IsYoungerThanMonths(birth.BirthDate, asOfDate, 4))
                {
                    projection.NonReproductiveUnder4Months += available;
                }
                else
                {
                    projection.NonReproductiveBetween4And12Months += available;
                }
            }
        }

        if (farm.LivestockSpecies is LivestockSpecies.Ovine or LivestockSpecies.Caprine)
        {
            foreach (var movement in unidentifiedMovements)
            {
                AccumulateUnidentifiedMovement(projection, farm.Id, movement);
            }

            projection.NonReproductiveBetween4And12Months = Math.Max(
                0,
                projection.NonReproductiveBetween4And12Months - consumedUnidentifiedMovementsByAutorreposition);
        }

        return projection;
    }

    private static void AccumulateOvineOrCaprineAnimal(FarmCensusProjection projection, Animal animal, DateOnly asOfDate)
    {
        if (animal.RegistrationCause == AnimalRegistrationCause.Autorreposicion)
        {
            var normalizedSex = FarmCensusProjectionSupport.NormalizeSex(animal.Sex);
            if (normalizedSex == "female")
            {
                projection.ReproductiveFemales++;
            }
            else if (normalizedSex == "male")
            {
                projection.ReproductiveMales++;
            }
            else
            {
                projection.NonReproductiveBetween4And12Months++;
            }

            return;
        }

        var birthDate = FarmCensusProjectionSupport.ResolveBirthDate(animal);
        if (birthDate is not null && FarmCensusProjectionSupport.IsYoungerThanMonths(birthDate.Value, asOfDate, 4))
        {
            projection.NonReproductiveUnder4Months++;
            return;
        }

        if (birthDate is not null && FarmCensusProjectionSupport.IsYoungerThanMonths(birthDate.Value, asOfDate, 12))
        {
            projection.NonReproductiveBetween4And12Months++;
            return;
        }

        var sex = FarmCensusProjectionSupport.NormalizeSex(animal.Sex);
        if (sex == "female")
        {
            projection.ReproductiveFemales++;
        }
        else if (sex == "male")
        {
            projection.ReproductiveMales++;
        }
        else
        {
            projection.NonReproductiveBetween4And12Months++;
        }
    }

    private static void AccumulatePorcineAnimal(FarmCensusProjection projection, Animal animal, DateOnly asOfDate)
    {
        var type = FarmCensusProjectionSupport.NormalizeType(animal.Porcino?.AnimalType);

        if (string.IsNullOrWhiteSpace(type))
        {
            var birthDate = FarmCensusProjectionSupport.ResolveBirthDate(animal);
            if (birthDate is not null && FarmCensusProjectionSupport.IsYoungerThanMonths(birthDate.Value, asOfDate, 4))
            {
                projection.Piglets++;
            }
            else
            {
                projection.Rears++;
            }

            return;
        }

        if (type.Contains("bait", StringComparison.Ordinal) || type.Contains("cebo", StringComparison.Ordinal))
        {
            projection.Baits++;
        }
        else if (type.Contains("boar", StringComparison.Ordinal) || type.Contains("verraco", StringComparison.Ordinal))
        {
            projection.Boars++;
        }
        else if (type.Contains("piglet", StringComparison.Ordinal) || type.Contains("lech", StringComparison.Ordinal))
        {
            projection.Piglets++;
        }
        else if (type.Contains("reposition", StringComparison.Ordinal) && (type.Contains("sow", StringComparison.Ordinal) || type.Contains("cerda", StringComparison.Ordinal)))
        {
            projection.SowsReposition++;
        }
        else if (type.Contains("reposition", StringComparison.Ordinal) || type.Contains("repos", StringComparison.Ordinal))
        {
            projection.MalesReposition++;
        }
        else if (type.Contains("sow", StringComparison.Ordinal) || type.Contains("cerda", StringComparison.Ordinal))
        {
            projection.SowsForLive++;
        }
        else
        {
            projection.Rears++;
        }
    }

    private static void AccumulateUnidentifiedMovement(FarmCensusProjection projection, long farmId, MovementCertificate movement)
    {
        if (movement.UnidentifiedCategory is null)
        {
            return;
        }

        var sign = movement.DestinationLivestockId == farmId ? 1 : -1;
        var count = movement.NumberOfAnimals * sign;

        if (movement.UnidentifiedCategory == MovementUnidentifiedCategory.Under4Months)
        {
            projection.NonReproductiveUnder4Months += count;
        }
        else
        {
            projection.NonReproductiveBetween4And12Months += count;
        }
    }

    private static Census CreateSyntheticCensus(LivestockFarm farm, DateOnly censusDate, FarmCensusProjection projection)
    {
        return new Census
        {
            LivestockFarmId = farm.Id,
            CensusDate = censusDate,
            OvinoCaprino = farm.LivestockSpecies == LivestockSpecies.Porcine
                ? null
                : new CensusOvinoCaprino
                {
                    NonReproductiveUnder4Months = projection.NonReproductiveUnder4Months,
                    NonReproductiveBetween4And12Months = projection.NonReproductiveBetween4And12Months,
                    ReproductiveFemale = projection.ReproductiveFemales,
                    ReproductiveMale = projection.ReproductiveMales
                },
            Porcino = farm.LivestockSpecies != LivestockSpecies.Porcine
                ? null
                : new CensusPorcino
                {
                    Boars = projection.Boars,
                    Sow = projection.SowsForLive,
                    SowsReposition = projection.SowsReposition,
                    PigsReposition = projection.MalesReposition,
                    Piglets = projection.Piglets,
                    Rears = projection.Rears,
                    Baits = projection.Baits
                }
        };
    }

    private static FarmCensusResponse CreateResponse(
        LivestockFarm farm,
        int year,
        FarmCensusProjection projection,
        IReadOnlyList<int> availableYears)
    {
        return new FarmCensusResponse(
            null,
            farm.Id,
            year,
            farm.LivestockSpecies.ToString(),
            projection.NonReproductiveUnder4Months,
            projection.NonReproductiveBetween4And12Months,
            projection.ReproductiveFemales,
            projection.ReproductiveMales,
            projection.Boars,
            projection.SowsForLive,
            projection.SowsReposition,
            projection.MalesReposition,
            projection.Piglets,
            projection.Rears,
            projection.Baits,
            projection.Total,
            availableYears);
    }

    private sealed class FarmCensusProjection
    {
        public int NonReproductiveUnder4Months { get; set; }

        public int NonReproductiveBetween4And12Months { get; set; }

        public int ReproductiveFemales { get; set; }

        public int ReproductiveMales { get; set; }

        public int Boars { get; set; }

        public int SowsForLive { get; set; }

        public int SowsReposition { get; set; }

        public int MalesReposition { get; set; }

        public int Piglets { get; set; }

        public int Rears { get; set; }

        public int Baits { get; set; }

        public int Total =>
            NonReproductiveUnder4Months +
            NonReproductiveBetween4And12Months +
            ReproductiveFemales +
            ReproductiveMales +
            Boars +
            SowsForLive +
            SowsReposition +
            MalesReposition +
            Piglets +
            Rears +
            Baits;
    }
}

public static class FarmCensusProjectionSupport
{
    public static DateOnly? ResolveBirthDate(Animal animal)
    {
        if (animal.BirthDate is not null)
        {
            return animal.BirthDate;
        }

        return animal.BirthYear is null
            ? null
            : new DateOnly(animal.BirthYear.Value, 1, 1);
    }

    public static int? ResolveBirthYear(Animal animal)
    {
        return animal.BirthDate?.Year ?? animal.BirthYear;
    }

    public static bool IsYoungerThanMonths(DateOnly birthDate, DateOnly asOfDate, int months)
    {
        return birthDate.AddMonths(months) > asOfDate;
    }

    public static bool IsOlderThanMonths(DateOnly birthDate, DateOnly asOfDate, int months)
    {
        return birthDate.AddMonths(months) < asOfDate;
    }

    public static string? NormalizeSex(string? sex)
    {
        return string.IsNullOrWhiteSpace(sex) ? null : sex.Trim().ToLowerInvariant();
    }

    public static string? NormalizeType(string? animalType)
    {
        return string.IsNullOrWhiteSpace(animalType) ? null : animalType.Trim().ToLowerInvariant();
    }
}
