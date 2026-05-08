using System.Security.Claims;
using Pecualia.Api.Contracts.Farmers;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Services;

namespace Pecualia.Api.Controllers;

public static class FarmerController
{
    public static IEndpointRouteBuilder MapFarmerController(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/farmers").RequireAuthorization(AuthorizationPolicies.ManagerOnly);

        group.MapGet("/", async (ClaimsPrincipal user, IFarmerService service, string? search, string? province, string? status, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetManagedFarmersAsync(user.GetUserId(), search, province, status, cancellationToken)));

        group.MapGet("/{farmerId:long}", async (ClaimsPrincipal user, long farmerId, IFarmerService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetManagedFarmerDetailAsync(user.GetUserId(), farmerId, cancellationToken)));

        group.MapPost("/", async (ClaimsPrincipal user, CreateFarmerRequest request, IFarmerService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CreateManagedFarmerAsync(user.GetUserId(), request, cancellationToken)));

        group.MapPut("/{farmerId:long}", async (ClaimsPrincipal user, long farmerId, UpdateFarmerRequest request, IFarmerService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.UpdateManagedFarmerAsync(user.GetUserId(), farmerId, request, cancellationToken)));

        group.MapPost("/{farmerId:long}/send-activation", async (ClaimsPrincipal user, long farmerId, IFarmerService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(async () => new
            {
                resent = await service.ResendActivationAsync(user.GetUserId(), farmerId, cancellationToken)
            }));

        group.MapDelete("/{farmerId:long}/manager-link", async (ClaimsPrincipal user, long farmerId, IFarmerService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(async () =>
            {
                await service.UnlinkManagedFarmerAsync(user.GetUserId(), farmerId, cancellationToken);
                return new
                {
                    unlinked = true
                };
            }));

        return endpoints;
    }
}
