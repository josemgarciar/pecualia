using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;
using Pecualia.Api.Contracts.Billing;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Stripe;
using BillingPortalSessionCreateOptions = Stripe.BillingPortal.SessionCreateOptions;
using BillingPortalSessionService = Stripe.BillingPortal.SessionService;
using CheckoutSession = Stripe.Checkout.Session;
using CheckoutSessionCreateOptions = Stripe.Checkout.SessionCreateOptions;
using CheckoutSessionGetOptions = Stripe.Checkout.SessionGetOptions;
using CheckoutSessionLineItemOptions = Stripe.Checkout.SessionLineItemOptions;
using CheckoutSessionService = Stripe.Checkout.SessionService;
using CheckoutSessionSubscriptionDataOptions = Stripe.Checkout.SessionSubscriptionDataOptions;
using LocalSubscription = Pecualia.Api.Models.Entities.Subscription;
using StripeSubscription = Stripe.Subscription;

namespace Pecualia.Api.Services;

public interface IBillingService
{
    Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(long userId, UserRole role, CreateCheckoutSessionRequest request, CancellationToken cancellationToken);

    Task<PortalSessionResponse> CreatePortalSessionAsync(long userId, CancellationToken cancellationToken);

    Task<CheckoutSessionStatusResponse> GetCheckoutSessionStatusAsync(long userId, string sessionId, CancellationToken cancellationToken);

    Task HandleWebhookAsync(string payload, string? stripeSignature, CancellationToken cancellationToken);
}

public sealed class BillingService(
    PecualiaDbContext dbContext,
    IOptions<StripeOptions> stripeOptions,
    IOptions<FrontendOptions> frontendOptions,
    IClock clock)
    : IBillingService
{
    private readonly StripeOptions _stripeOptions = stripeOptions.Value;
    private readonly FrontendOptions _frontendOptions = frontendOptions.Value;

    public async Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(
        long userId,
        UserRole role,
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureStripeSecretKeyConfigured();

        var user = await dbContext.Users
            .Include(entity => entity.Subscription)
            .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new DomainException("Usuario no encontrado.");
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new DomainException("La cuenta debe tener un correo electrónico para iniciar el cobro con Stripe.");
        }

        var targetPlanType = NormalizeCheckoutPlan(role, request.PlanType);
        var priceId = ResolvePriceId(role, targetPlanType);

        if (!string.IsNullOrWhiteSpace(user.Subscription?.StripeSubscriptionId))
        {
            throw new DomainException("Ya existe una suscripción conectada con Stripe. Usa \"Gestionar facturación\" para cambiar o cancelar el plan.");
        }

        var frontendBaseUrl = GetFrontendBaseUrl();
        var metadata = BuildMetadata(user, targetPlanType);
        var stripeClient = BuildStripeClient();
        var checkoutService = new CheckoutSessionService(stripeClient);

        var session = await checkoutService.CreateAsync(
            new CheckoutSessionCreateOptions
            {
                Mode = "subscription",
                SuccessUrl = $"{frontendBaseUrl}/app/profile/subscription?checkout=success&session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{frontendBaseUrl}/app/profile/subscription?checkout=cancelled",
                ClientReferenceId = user.Id.ToString(),
                Customer = string.IsNullOrWhiteSpace(user.Subscription?.StripeCustomerId) ? null : user.Subscription.StripeCustomerId,
                CustomerEmail = string.IsNullOrWhiteSpace(user.Subscription?.StripeCustomerId) ? user.Email : null,
                LineItems =
                [
                    new CheckoutSessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1
                    }
                ],
                Metadata = metadata,
                SubscriptionData = new CheckoutSessionSubscriptionDataOptions
                {
                    Metadata = metadata
                },
                AllowPromotionCodes = true
            },
            requestOptions: null,
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(session.Url))
        {
            throw new DomainException("Stripe no devolvió una URL de checkout válida.");
        }

        return new CheckoutSessionResponse(session.Url);
    }

    public async Task<PortalSessionResponse> CreatePortalSessionAsync(long userId, CancellationToken cancellationToken)
    {
        EnsureStripeSecretKeyConfigured();

        var subscription = await dbContext.Subscriptions
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.UserId == userId, cancellationToken);

        if (subscription is null || string.IsNullOrWhiteSpace(subscription.StripeCustomerId))
        {
            throw new DomainException("Esta cuenta todavía no tiene un cliente de Stripe asociado. Contrata un plan de pago primero.");
        }

        var portalService = new BillingPortalSessionService(BuildStripeClient());
        var portalSession = await portalService.CreateAsync(
            new BillingPortalSessionCreateOptions
            {
                Customer = subscription.StripeCustomerId,
                ReturnUrl = $"{GetFrontendBaseUrl()}/app/profile/subscription"
            },
            requestOptions: null,
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(portalSession.Url))
        {
            throw new DomainException("Stripe no devolvió una URL válida para el portal de facturación.");
        }

        return new PortalSessionResponse(portalSession.Url);
    }

    public async Task<CheckoutSessionStatusResponse> GetCheckoutSessionStatusAsync(long userId, string sessionId, CancellationToken cancellationToken)
    {
        EnsureStripeSecretKeyConfigured();

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new DomainException("Debes indicar el identificador de sesión de Stripe.");
        }

        var sessionService = new CheckoutSessionService(BuildStripeClient());
        var session = await sessionService.GetAsync(
            sessionId,
            new CheckoutSessionGetOptions(),
            requestOptions: null,
            cancellationToken: cancellationToken);

        if (session.ClientReferenceId != userId.ToString())
        {
            throw new DomainException("La sesión de Stripe no pertenece al usuario autenticado.");
        }

        if (!string.IsNullOrWhiteSpace(session.SubscriptionId))
        {
            var localUser = await dbContext.Users
                .Include(entity => entity.Subscription)
                .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

            if (localUser is not null)
            {
                var stripeSubscription = await GetStripeSubscriptionAsync(session.SubscriptionId, cancellationToken);
                await UpsertSubscriptionFromStripeAsync(
                    localUser,
                    localUser.Subscription,
                    stripeSubscription,
                    session.CustomerId,
                    cancellationToken);
            }
        }

        return new CheckoutSessionStatusResponse(
            session.Status ?? "unknown",
            session.PaymentStatus ?? "unknown",
            session.CustomerDetails?.Email);
    }

    public async Task HandleWebhookAsync(string payload, string? stripeSignature, CancellationToken cancellationToken)
    {
        EnsureStripeSecretKeyConfigured();

        if (string.IsNullOrWhiteSpace(_stripeOptions.WebhookSecret))
        {
            throw new InvalidOperationException("Falta la configuración obligatoria 'Stripe:WebhookSecret'.");
        }

        if (string.IsNullOrWhiteSpace(stripeSignature))
        {
            throw new DomainException("Falta la cabecera Stripe-Signature.");
        }

        var stripeEvent = EventUtility.ConstructEvent(payload, stripeSignature, _stripeOptions.WebhookSecret);
        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
            {
                var session = stripeEvent.Data.Object as CheckoutSession;
                if (session is not null)
                {
                    await HandleCheckoutSessionCompletedAsync(session, cancellationToken);
                }
                break;
            }
            case EventTypes.CustomerSubscriptionCreated:
            case EventTypes.CustomerSubscriptionUpdated:
            case EventTypes.CustomerSubscriptionDeleted:
            {
                var stripeSubscription = stripeEvent.Data.Object as StripeSubscription;
                if (stripeSubscription is not null)
                {
                    await HandleStripeSubscriptionUpdatedAsync(stripeSubscription, cancellationToken);
                }
                break;
            }
            case EventTypes.InvoicePaid:
            case EventTypes.InvoicePaymentFailed:
            {
                var invoice = stripeEvent.Data.Object as Invoice;
                var stripeSubscriptionId = invoice?.Parent?.SubscriptionDetails?.SubscriptionId;
                if (!string.IsNullOrWhiteSpace(stripeSubscriptionId))
                {
                    var stripeSubscription = await GetStripeSubscriptionAsync(stripeSubscriptionId, cancellationToken);
                    await HandleStripeSubscriptionUpdatedAsync(stripeSubscription, cancellationToken);
                }
                break;
            }
        }
    }

    private async Task HandleCheckoutSessionCompletedAsync(CheckoutSession session, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(session.ClientReferenceId) || !long.TryParse(session.ClientReferenceId, out var userId))
        {
            return;
        }

        var user = await dbContext.Users
            .Include(entity => entity.Subscription)
            .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        if (user is null || string.IsNullOrWhiteSpace(session.SubscriptionId))
        {
            return;
        }

        var stripeSubscription = await GetStripeSubscriptionAsync(session.SubscriptionId, cancellationToken);
        await UpsertSubscriptionFromStripeAsync(
            user,
            user.Subscription,
            stripeSubscription,
            session.CustomerId,
            cancellationToken);
    }

    private async Task HandleStripeSubscriptionUpdatedAsync(StripeSubscription stripeSubscription, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.Subscriptions
            .Include(entity => entity.User)
            .SingleOrDefaultAsync(entity => entity.StripeSubscriptionId == stripeSubscription.Id, cancellationToken);

        if (subscription is not null)
        {
            await UpsertSubscriptionFromStripeAsync(
                subscription.User,
                subscription,
                stripeSubscription,
                stripeSubscription.CustomerId,
                cancellationToken);
            return;
        }

        if (!TryGetUserIdFromMetadata(stripeSubscription.Metadata, out var userId))
        {
            return;
        }

        var user = await dbContext.Users
            .Include(entity => entity.Subscription)
            .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        if (user is null)
        {
            return;
        }

        await UpsertSubscriptionFromStripeAsync(
            user,
            user.Subscription,
            stripeSubscription,
            stripeSubscription.CustomerId,
            cancellationToken);
    }

    private async Task UpsertSubscriptionFromStripeAsync(
        AppUser user,
        LocalSubscription? localSubscription,
        StripeSubscription stripeSubscription,
        string? customerId,
        CancellationToken cancellationToken)
    {
        var effectiveSubscription = localSubscription ?? new LocalSubscription
        {
            UserId = user.Id,
            User = user
        };

        var priceId = stripeSubscription.Items.Data.FirstOrDefault()?.Price?.Id;
        var planType = ResolvePlanTypeFromStripe(user.Role, stripeSubscription.Metadata, priceId, effectiveSubscription.PlanType);
        var initialDate = ToDateOnly(stripeSubscription.StartDate) ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var expirationDate = ResolveExpirationDate(stripeSubscription, initialDate);

        effectiveSubscription.PlanType = planType;
        effectiveSubscription.InitialDate = initialDate;
        effectiveSubscription.ExpirationDate = expirationDate >= initialDate ? expirationDate : initialDate;
        effectiveSubscription.State = MapStripeStatus(stripeSubscription.Status);
        effectiveSubscription.Autorenew = !stripeSubscription.CancelAtPeriodEnd && effectiveSubscription.State != SubscriptionState.Cancelled;
        effectiveSubscription.StripeCustomerId = customerId ?? stripeSubscription.CustomerId;
        effectiveSubscription.StripeSubscriptionId = stripeSubscription.Id;
        effectiveSubscription.StripePriceId = priceId;

        if (localSubscription is null)
        {
            dbContext.Subscriptions.Add(effectiveSubscription);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private PlanType NormalizeCheckoutPlan(UserRole role, PlanType requestedPlanType)
    {
        if (requestedPlanType == PlanType.Basic)
        {
            throw new DomainException("El plan Free no se contrata con Stripe. Para volver a Free, cancela o cambia la suscripción desde el portal de facturación.");
        }

        if (role == UserRole.Farmer && requestedPlanType != PlanType.Professional)
        {
            throw new DomainException("Las cuentas Ganader@ solo pueden contratar el plan Pro.");
        }

        return requestedPlanType;
    }

    private string ResolvePriceId(UserRole role, PlanType planType)
    {
        var priceId = (role, planType) switch
        {
            (UserRole.Manager, PlanType.Professional) => _stripeOptions.ManagerProfessionalMonthlyPriceId,
            (UserRole.Manager, PlanType.Enterprise) => _stripeOptions.ManagerEnterpriseMonthlyPriceId,
            (UserRole.Farmer, PlanType.Professional) => _stripeOptions.FarmerProfessionalMonthlyPriceId,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(priceId))
        {
            throw new DomainException($"Falta el Price ID de Stripe para el plan {planType}.");
        }

        return priceId;
    }

    private PlanType ResolvePlanTypeFromStripe(
        UserRole role,
        IDictionary<string, string> metadata,
        string? priceId,
        PlanType fallbackPlanType)
    {
        if (metadata.TryGetValue("planType", out var rawPlanType) &&
            Enum.TryParse<PlanType>(rawPlanType, ignoreCase: true, out var planTypeFromMetadata))
        {
            return planTypeFromMetadata;
        }

        if (!string.IsNullOrWhiteSpace(priceId))
        {
            if (priceId == _stripeOptions.ManagerProfessionalMonthlyPriceId)
            {
                return PlanType.Professional;
            }

            if (priceId == _stripeOptions.ManagerEnterpriseMonthlyPriceId)
            {
                return PlanType.Enterprise;
            }

            if (priceId == _stripeOptions.FarmerProfessionalMonthlyPriceId)
            {
                return PlanType.Professional;
            }
        }

        return role == UserRole.Farmer && fallbackPlanType == 0
            ? PlanType.Basic
            : fallbackPlanType;
    }

    private static SubscriptionState MapStripeStatus(string? stripeStatus)
    {
        return stripeStatus switch
        {
            "active" => SubscriptionState.Active,
            "trialing" => SubscriptionState.Active,
            "past_due" => SubscriptionState.PastDue,
            "unpaid" => SubscriptionState.PastDue,
            "canceled" => SubscriptionState.Cancelled,
            "incomplete_expired" => SubscriptionState.Cancelled,
            _ => SubscriptionState.Cancelled
        };
    }

    private Dictionary<string, string> BuildMetadata(AppUser user, PlanType targetPlanType)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["userId"] = user.Id.ToString(),
            ["role"] = user.Role.ToString(),
            ["planType"] = targetPlanType.ToString()
        };
    }

    private async Task<StripeSubscription> GetStripeSubscriptionAsync(string stripeSubscriptionId, CancellationToken cancellationToken)
    {
        var subscriptionService = new SubscriptionService(BuildStripeClient());
        return await subscriptionService.GetAsync(
            stripeSubscriptionId,
            new SubscriptionGetOptions(),
            requestOptions: null,
            cancellationToken: cancellationToken);
    }

    private bool TryGetUserIdFromMetadata(IDictionary<string, string> metadata, out long userId)
    {
        userId = default;
        return metadata.TryGetValue("userId", out var rawUserId) && long.TryParse(rawUserId, out userId);
    }

    private static DateOnly? ToDateOnly(DateTime? value)
    {
        return value.HasValue ? DateOnly.FromDateTime(value.Value.ToUniversalTime()) : null;
    }

    private static DateOnly ResolveExpirationDate(StripeSubscription stripeSubscription, DateOnly fallbackDate)
    {
        var currentPeriodEnd = stripeSubscription.Items.Data
            .Select(entity => ToDateOnly(entity.CurrentPeriodEnd))
            .Where(entity => entity.HasValue)
            .Select(entity => entity!.Value)
            .DefaultIfEmpty(fallbackDate.AddMonths(1))
            .Max();

        return currentPeriodEnd >= fallbackDate ? currentPeriodEnd : fallbackDate;
    }

    private string GetFrontendBaseUrl()
    {
        var value = _frontendOptions.Origin.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Falta la configuración obligatoria 'Frontend:Origin'.");
        }

        return value;
    }

    private StripeClient BuildStripeClient()
    {
        return new StripeClient(_stripeOptions.SecretKey);
    }

    private void EnsureStripeSecretKeyConfigured()
    {
        if (string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
        {
            throw new DomainException("Stripe no está configurado todavía en este entorno. Falta Stripe__SecretKey en la API.");
        }
    }
}
