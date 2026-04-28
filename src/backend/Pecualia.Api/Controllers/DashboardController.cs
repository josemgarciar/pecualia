using System.Security.Claims;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Services;

namespace Pecualia.Api.Controllers;

public static class DashboardController
{
    public static IEndpointRouteBuilder MapDashboardController(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/dashboard/summary", async (ClaimsPrincipal user, IDashboardService dashboardService, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => dashboardService.GetSummaryAsync(user.GetUserId(), user.GetRole(), cancellationToken)))
            .RequireAuthorization(AuthorizationPolicies.FarmerOrManager);

        return endpoints;
    }
}
