using System.Security.Claims;
using Pecualia.Api.Contracts.Billing;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Services;

namespace Pecualia.Api.Controllers;

public static class BillingController
{
    public static IEndpointRouteBuilder MapBillingController(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/billing");

        group.MapPost("/checkout-session", async (
            ClaimsPrincipal user,
            CreateCheckoutSessionRequest request,
            IBillingService service,
            CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CreateCheckoutSessionAsync(
                user.GetUserId(),
                user.GetRole(),
                request,
                cancellationToken)))
            .RequireAuthorization(AuthorizationPolicies.FarmerOrManager);

        group.MapPost("/portal-session", async (
            ClaimsPrincipal user,
            IBillingService service,
            CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CreatePortalSessionAsync(
                user.GetUserId(),
                cancellationToken)))
            .RequireAuthorization(AuthorizationPolicies.FarmerOrManager);

        group.MapGet("/checkout-session-status/{sessionId}", async (
            ClaimsPrincipal user,
            string sessionId,
            IBillingService service,
            CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetCheckoutSessionStatusAsync(
                user.GetUserId(),
                sessionId,
                cancellationToken)))
            .RequireAuthorization(AuthorizationPolicies.FarmerOrManager);

        group.MapPost("/webhook", async (
            HttpRequest request,
            IBillingService service,
            CancellationToken cancellationToken) =>
        {
            var payload = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);
            var stripeSignature = request.Headers["Stripe-Signature"].ToString();

            try
            {
                await service.HandleWebhookAsync(payload, stripeSignature, cancellationToken);
                return Results.Ok();
            }
            catch (DomainException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        return endpoints;
    }
}
