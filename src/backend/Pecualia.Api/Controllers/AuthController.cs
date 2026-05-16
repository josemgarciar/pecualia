using System.Security.Claims;
using Pecualia.Api.Contracts.Auth;
using Pecualia.Api.Services;

namespace Pecualia.Api.Controllers;

public static class AuthController
{
    public static IEndpointRouteBuilder MapAuthController(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/register/manager", async (RegisterManagerRequest request, IAuthService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.RegisterManagerAsync(request, cancellationToken)));

        group.MapPost("/register/farmer", async (RegisterFarmerRequest request, IAuthService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.RegisterFarmerAsync(request, cancellationToken)));

        group.MapPost("/login", async (LoginRequest request, IAuthService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.LoginAsync(request, cancellationToken)));

        group.MapPost("/activate-account", async (ActivateAccountRequest request, IAuthService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.ActivateAccountAsync(request, cancellationToken)));

        group.MapPost("/resend-activation", async (ResendActivationRequest request, IAuthService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.ResendActivationAsync(request, cancellationToken)));

        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, IAuthService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.ForgotPasswordAsync(request, cancellationToken)));

        group.MapPost("/reset-password", async (ResetPasswordRequest request, IAuthService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.ResetPasswordAsync(request, cancellationToken)));

        group.MapGet("/me", async (ClaimsPrincipal user, IAuthService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(async () =>
            {
                var profile = await service.GetCurrentUserAsync(user.GetUserId(), cancellationToken);
                return profile ?? throw new DomainException("Usuario no encontrado.");
            }))
            .RequireAuthorization();

        group.MapPut("/settings", async (ClaimsPrincipal user, UpdateUserSettingsRequest request, IAuthService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.UpdateCurrentUserSettingsAsync(user.GetUserId(), request, cancellationToken)))
            .RequireAuthorization();

        group.MapGet("/task-reminder-settings", async (ClaimsPrincipal user, ITaskReminderSettingsService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetCurrentUserSettingsAsync(user.GetUserId(), cancellationToken)))
            .RequireAuthorization();

        group.MapPut("/task-reminder-settings", async (ClaimsPrincipal user, UpdateTaskReminderSettingsRequest request, ITaskReminderSettingsService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.UpdateCurrentUserSettingsAsync(user.GetUserId(), request, cancellationToken)))
            .RequireAuthorization();

        return endpoints;
    }
}
