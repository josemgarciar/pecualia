using Pecualia.Api.Models.Entities;

namespace Pecualia.Api.Services;

internal static class PorcineTransitionSupport
{
    internal const int DecisionAgeMonths = 3;
    internal const int FinalAgeMonths = 6;

    internal static DateOnly GetDecisionDate(DateOnly birthDate) => birthDate.AddMonths(DecisionAgeMonths);

    internal static DateOnly GetFinalTransitionDate(DateOnly birthDate) => birthDate.AddMonths(FinalAgeMonths);

    internal static bool IsPigletStage(DateOnly birthDate, DateOnly asOfDate) =>
        birthDate.AddMonths(DecisionAgeMonths) > asOfDate;

    internal static bool IsIntermediateStage(DateOnly birthDate, DateOnly asOfDate) =>
        birthDate.AddMonths(DecisionAgeMonths) <= asOfDate &&
        birthDate.AddMonths(FinalAgeMonths) > asOfDate;

    internal static bool IsFinalStage(DateOnly birthDate, DateOnly asOfDate) =>
        birthDate.AddMonths(FinalAgeMonths) <= asOfDate;

    internal static PorcineTransitionBranch ResolveBranch(string? animalType)
    {
        return PorcineMovementSupport.ResolveBucket(animalType) switch
        {
            PorcineMovementBucket.Sows or PorcineMovementBucket.SowsReposition => PorcineTransitionBranch.Sows,
            PorcineMovementBucket.Boars or PorcineMovementBucket.PigsReposition => PorcineTransitionBranch.Males,
            _ => PorcineTransitionBranch.Rears
        };
    }

    internal static PorcineDecisionRemainingQuantities CalculateRemainingQuantities(
        PorcineBirthTransitionDecision decision,
        PorcineDecisionConsumption consumption)
    {
        return new PorcineDecisionRemainingQuantities(
            Math.Max(0, decision.ToRears - Math.Max(0, consumption.Rears - decision.BaselineRearsConsumed)),
            Math.Max(0, decision.ToSowsReposition - Math.Max(0, consumption.Sows - decision.BaselineSowsRepositionConsumed)),
            Math.Max(0, decision.ToMalesReposition - Math.Max(0, consumption.Males - decision.BaselineMalesRepositionConsumed)));
    }
}

internal enum PorcineTransitionBranch
{
    Rears = 0,
    Sows = 1,
    Males = 2
}

internal sealed record PorcineDecisionConsumption(int Rears, int Sows, int Males);

internal sealed record PorcineDecisionRemainingQuantities(int Rears, int Sows, int Males)
{
    internal int Total => Rears + Sows + Males;
}
