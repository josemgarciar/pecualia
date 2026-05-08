using System.Security.Claims;
using Pecualia.Api.Contracts.Movements;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;

namespace Pecualia.Api.Controllers;

public static class MovementController
{
    public static IEndpointRouteBuilder MapMovementController(this IEndpointRouteBuilder endpoints)
    {
        var movementGroup = endpoints.MapGroup("/api/movements").RequireAuthorization(AuthorizationPolicies.FarmerOrManager);

        movementGroup.MapGet("/breeds/{species}", (LivestockSpecies species, IMovementService service) =>
            service.GetBreedOptions(species));

        movementGroup.MapGet("/{movementId:long}", async (ClaimsPrincipal user, long movementId, IMovementService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetMovementAsync(user.GetUserId(), user.GetRole(), movementId, cancellationToken)));

        movementGroup.MapPost("/manual", async (ClaimsPrincipal user, CreateManualMovementRequest request, IMovementService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CreateManualMovementAsync(user.GetUserId(), user.GetRole(), request, cancellationToken)));

        movementGroup.MapPost("/imports/preview", async (ClaimsPrincipal user, PreviewMovementImportRequest request, IMovementService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.PreviewImportAsync(user.GetUserId(), user.GetRole(), request, cancellationToken)));

        movementGroup.MapPost("/imports/commit", async (ClaimsPrincipal user, CommitMovementImportRequest request, IMovementService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CommitImportAsync(user.GetUserId(), user.GetRole(), request, cancellationToken)));

        var farmGroup = endpoints.MapGroup("/api/farms").RequireAuthorization(AuthorizationPolicies.FarmerOrManager);
        farmGroup.MapGet("/{farmId:long}/movements", async (ClaimsPrincipal user, long farmId, IMovementService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetFarmMovementsAsync(user.GetUserId(), user.GetRole(), farmId, cancellationToken)));

        return endpoints;
    }
}
