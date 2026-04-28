using System.Security.Claims;
using Pecualia.Api.Contracts.Farms;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Services;

namespace Pecualia.Api.Controllers;

public static class FarmController
{
    public static IEndpointRouteBuilder MapFarmController(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/farms").RequireAuthorization(AuthorizationPolicies.FarmerOrManager);

        group.MapGet("/", async (ClaimsPrincipal user, IFarmService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetAccessibleFarmsAsync(user.GetUserId(), user.GetRole(), cancellationToken)));

        group.MapPost("/", async (ClaimsPrincipal user, CreateFarmRequest request, IFarmService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CreateFarmAsync(user.GetUserId(), user.GetRole(), request, cancellationToken)));

        group.MapGet("/{farmId:long}/summary", async (ClaimsPrincipal user, long farmId, IFarmService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetSummaryAsync(user.GetUserId(), user.GetRole(), farmId, cancellationToken)));

        group.MapGet("/{farmId:long}/animals", async (
            ClaimsPrincipal user,
            long farmId,
            IAnimalService service,
            string? search,
            string? species,
            string? sex,
            string? status,
            CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetAnimalsAsync(
                user.GetUserId(),
                user.GetRole(),
                farmId,
                search,
                species,
                sex,
                status,
                cancellationToken)));

        group.MapGet("/{farmId:long}", async (ClaimsPrincipal user, long farmId, IFarmService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetDetailAsync(user.GetUserId(), user.GetRole(), farmId, cancellationToken)));

        return endpoints;
    }
}
