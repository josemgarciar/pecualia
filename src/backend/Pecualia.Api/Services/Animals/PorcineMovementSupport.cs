using Pecualia.Api.Contracts.FarmOperations;

namespace Pecualia.Api.Services;

internal static class PorcineMovementSupport
{
    internal static PorcineMovementBreakdown BuildBreakdown(string? animalType, int quantity)
    {
        if (quantity <= 0)
        {
            return new PorcineMovementBreakdown();
        }

        return ResolveBucket(animalType) switch
        {
            PorcineMovementBucket.Baits => new PorcineMovementBreakdown { Baits = quantity },
            PorcineMovementBucket.Boars => new PorcineMovementBreakdown { Boars = quantity },
            PorcineMovementBucket.Piglets => new PorcineMovementBreakdown { Piglets = quantity },
            PorcineMovementBucket.PigsReposition => new PorcineMovementBreakdown { PigsReposition = quantity },
            PorcineMovementBucket.Sows => new PorcineMovementBreakdown { Sows = quantity },
            PorcineMovementBucket.SowsReposition => new PorcineMovementBreakdown { SowsReposition = quantity },
            _ => new PorcineMovementBreakdown { Rears = quantity }
        };
    }

    internal static PorcineMovementBucket ResolveBucket(string? animalType)
    {
        var normalizedType = FarmCensusProjectionSupport.NormalizeType(animalType);
        if (string.IsNullOrWhiteSpace(normalizedType))
        {
            return PorcineMovementBucket.Rears;
        }

        if (normalizedType.Contains("bait", StringComparison.Ordinal) || normalizedType.Contains("cebo", StringComparison.Ordinal))
        {
            return PorcineMovementBucket.Baits;
        }

        if (normalizedType.Contains("boar", StringComparison.Ordinal) || normalizedType.Contains("verraco", StringComparison.Ordinal))
        {
            return PorcineMovementBucket.Boars;
        }

        if (normalizedType.Contains("piglet", StringComparison.Ordinal) || normalizedType.Contains("lech", StringComparison.Ordinal))
        {
            return PorcineMovementBucket.Piglets;
        }

        if ((normalizedType.Contains("reposition", StringComparison.Ordinal) || normalizedType.Contains("repos", StringComparison.Ordinal)) &&
            (normalizedType.Contains("sow", StringComparison.Ordinal) ||
             normalizedType.Contains("cerda", StringComparison.Ordinal) ||
             normalizedType.Contains("madre", StringComparison.Ordinal) ||
             normalizedType.Contains("hembra", StringComparison.Ordinal)))
        {
            return PorcineMovementBucket.SowsReposition;
        }

        if (normalizedType.Contains("reposition", StringComparison.Ordinal) || normalizedType.Contains("repos", StringComparison.Ordinal))
        {
            return PorcineMovementBucket.PigsReposition;
        }

        if (normalizedType.Contains("sow", StringComparison.Ordinal) ||
            normalizedType.Contains("cerda", StringComparison.Ordinal) ||
            normalizedType.Contains("madre", StringComparison.Ordinal))
        {
            return PorcineMovementBucket.Sows;
        }

        return PorcineMovementBucket.Rears;
    }

    internal static int GetAvailableAnimals(FarmCensusResponse snapshot, string? animalType)
    {
        return ResolveBucket(animalType) switch
        {
            PorcineMovementBucket.Baits => snapshot.Baits,
            PorcineMovementBucket.Boars => snapshot.Boars,
            PorcineMovementBucket.Piglets => snapshot.Piglets,
            PorcineMovementBucket.PigsReposition => snapshot.MalesReposition,
            PorcineMovementBucket.Sows => snapshot.SowsForLive,
            PorcineMovementBucket.SowsReposition => snapshot.SowsReposition,
            _ => snapshot.Rears
        };
    }
}

internal enum PorcineMovementBucket
{
    Rears = 0,
    Baits = 1,
    Boars = 2,
    Piglets = 3,
    PigsReposition = 4,
    Sows = 5,
    SowsReposition = 6
}

internal sealed class PorcineMovementBreakdown
{
    public int Baits { get; init; }

    public int Boars { get; init; }

    public int Piglets { get; init; }

    public int PigsReposition { get; init; }

    public int Rears { get; init; }

    public int Sows { get; init; }

    public int SowsReposition { get; init; }
}
