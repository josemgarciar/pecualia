using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

internal static class PorcineCapacitySupport
{
    internal static void EnsureWithinCapacity(
        LivestockFarm farm,
        FarmCensusResponse snapshot,
        string? animalType,
        int quantity)
    {
        if (farm.LivestockSpecies != LivestockSpecies.Porcine || quantity <= 0)
        {
            return;
        }

        var mothersCount = snapshot.SowsForLive;
        var fatteningCount = snapshot.SowsReposition + snapshot.MalesReposition + snapshot.Baits;
        var bucket = ResolveBucket(animalType);

        if (bucket == PorcineCapacityBucket.Mothers)
        {
            mothersCount += quantity;
        }
        else if (bucket == PorcineCapacityBucket.Fattening)
        {
            fatteningCount += quantity;
        }

        if (farm.PorcineMothersCapacity is int mothersCapacity && mothersCount > mothersCapacity)
        {
            throw new DomainException("No puedes agregar más madres que la capacidad máxima autorizada de madres.");
        }

        if (farm.PorcineFatteningCapacity is int fatteningCapacity && fatteningCount > fatteningCapacity)
        {
            throw new DomainException("No puedes superar la capacidad máxima autorizada de cebo (machos autoreposición + hembras autoreposición + cebo).");
        }
    }

    internal static PorcineCapacityBucket ResolveBucket(string? animalType)
    {
        var normalizedType = FarmCensusProjectionSupport.NormalizeType(animalType);
        if (string.IsNullOrWhiteSpace(normalizedType))
        {
            return PorcineCapacityBucket.None;
        }

        if (normalizedType.Contains("bait", StringComparison.Ordinal) || normalizedType.Contains("cebo", StringComparison.Ordinal))
        {
            return PorcineCapacityBucket.Fattening;
        }

        if (normalizedType.Contains("reposition", StringComparison.Ordinal) && (normalizedType.Contains("sow", StringComparison.Ordinal) || normalizedType.Contains("cerda", StringComparison.Ordinal)))
        {
            return PorcineCapacityBucket.Fattening;
        }

        if (normalizedType.Contains("reposition", StringComparison.Ordinal) || normalizedType.Contains("repos", StringComparison.Ordinal))
        {
            return PorcineCapacityBucket.Fattening;
        }

        if (normalizedType.Contains("sow", StringComparison.Ordinal) || normalizedType.Contains("cerda", StringComparison.Ordinal) || normalizedType.Contains("madre", StringComparison.Ordinal))
        {
            return PorcineCapacityBucket.Mothers;
        }

        return PorcineCapacityBucket.None;
    }
}

internal enum PorcineCapacityBucket
{
    None = 0,
    Mothers = 1,
    Fattening = 2
}
