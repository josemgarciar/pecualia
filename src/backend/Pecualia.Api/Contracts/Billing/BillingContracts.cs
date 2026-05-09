using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Contracts.Billing;

public sealed record CreateCheckoutSessionRequest(PlanType PlanType);

public sealed record CheckoutSessionResponse(string CheckoutUrl);

public sealed record PortalSessionResponse(string PortalUrl);

public sealed record CheckoutSessionStatusResponse(
    string SessionStatus,
    string PaymentStatus,
    string? CustomerEmail);
