using System.Security.Claims;
using Pecualia.Api.Contracts.Animals;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Services;

namespace Pecualia.Api.Controllers;

public static class AnimalController
{
    public static IEndpointRouteBuilder MapAnimalController(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/animals").RequireAuthorization(AuthorizationPolicies.FarmerOrManager);

        group.MapGet("/", async (
            ClaimsPrincipal user,
            IAnimalService service,
            long? farmId,
            long? movementId,
            string? search,
            string? species,
            string? sex,
            string? status,
            CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetAnimalsAsync(
                user.GetUserId(),
                user.GetRole(),
                farmId,
                movementId,
                search,
                species,
                sex,
                status,
                cancellationToken)));

        group.MapGet("/{animalId:long}", async (ClaimsPrincipal user, long animalId, IAnimalService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetAnimalAsync(user.GetUserId(), user.GetRole(), animalId, cancellationToken)));

        group.MapPost("/", async (ClaimsPrincipal user, CreateAnimalRequest request, IAnimalService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CreateAnimalAsync(user.GetUserId(), user.GetRole(), request, cancellationToken)));

        group.MapPut("/{animalId:long}", async (ClaimsPrincipal user, long animalId, UpdateAnimalRequest request, IAnimalService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.UpdateAnimalAsync(user.GetUserId(), user.GetRole(), animalId, request, cancellationToken)));

        group.MapPost("/{animalId:long}/discharge", async (ClaimsPrincipal user, long animalId, DischargeAnimalRequest request, IAnimalService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.DischargeAnimalAsync(user.GetUserId(), user.GetRole(), animalId, request, cancellationToken)));

        group.MapDelete("/{animalId:long}", async (ClaimsPrincipal user, long animalId, IAnimalService service, CancellationToken cancellationToken) =>
        {
            try
            {
                await service.DeleteAnimalAsync(user.GetUserId(), user.GetRole(), animalId, cancellationToken);
                return Results.NoContent();
            }
            catch (DomainException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        return endpoints;
    }
}
