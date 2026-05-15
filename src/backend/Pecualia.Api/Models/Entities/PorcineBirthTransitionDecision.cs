namespace Pecualia.Api.Models.Entities;

public sealed class PorcineBirthTransitionDecision
{
    public long BirthId { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public int ToRears { get; set; }

    public int ToSowsReposition { get; set; }

    public int ToMalesReposition { get; set; }

    public int BaselineRearsConsumed { get; set; }

    public int BaselineSowsRepositionConsumed { get; set; }

    public int BaselineMalesRepositionConsumed { get; set; }

    public DateTime ResolvedAt { get; set; }

    public long? BalanceId { get; set; }

    public AnimalBirth Birth { get; set; } = null!;

    public Balance? Balance { get; set; }
}
