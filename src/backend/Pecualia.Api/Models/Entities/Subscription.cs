using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Models.Entities;

public sealed class Subscription
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public bool Autorenew { get; set; }

    public DateOnly ExpirationDate { get; set; }

    public DateOnly InitialDate { get; set; }

    public PlanType PlanType { get; set; }

    public SubscriptionState State { get; set; }

    public string? StripeCustomerId { get; set; }

    public string? StripeSubscriptionId { get; set; }

    public string? StripePriceId { get; set; }

    public AppUser User { get; set; } = null!;
}
