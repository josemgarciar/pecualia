using System.Security.Claims;
using Pecualia.Api.Contracts.Auth;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Services;

namespace Pecualia.Api.Controllers;

public static class AuthController
{
    public static IEndpointRouteBuilder MapAuthController(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/register/manager", async (HttpContext httpContext, RegisterManagerRequest request, IAuthService service, IAuthCookieService authCookieService, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(async () =>
            {
                var response = await service.RegisterManagerAsync(request, cancellationToken);
                authCookieService.AppendAuthCookie(httpContext, response.Token);
                return new AuthResponse(response.User);
            }))
            .RequireRateLimiting("auth-register");

        group.MapPost("/register/farmer", async (HttpContext httpContext, RegisterFarmerRequest request, IAuthService service, IAuthCookieService authCookieService, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(async () =>
            {
                var response = await service.RegisterFarmerAsync(request, cancellationToken);
                authCookieService.AppendAuthCookie(httpContext, response.Token);
                return new AuthResponse(response.User);
            }))
            .RequireRateLimiting("auth-register");

        group.MapPost("/login", async (HttpContext httpContext, LoginRequest request, IAuthService service, IAuthCookieService authCookieService, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(async () =>
            {
                var response = await service.LoginAsync(request, cancellationToken);
                authCookieService.AppendAuthCookie(httpContext, response.Token);
                return new AuthResponse(response.User);
            }))
            .RequireRateLimiting("auth-login");

        group.MapPost("/logout", (HttpContext httpContext, IAuthCookieService authCookieService) =>
        {
            authCookieService.ClearAuthCookie(httpContext);
            return Results.Ok(new { message = "Sesión cerrada correctamente." });
        });

        group.MapPost("/activate-account", async (ActivateAccountRequest request, IAuthService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.ActivateAccountAsync(request, cancellationToken)))
            .RequireRateLimiting("auth-activation");

        group.MapPost("/resend-activation", async (ResendActivationRequest request, IAuthService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.ResendActivationAsync(request, cancellationToken)))
            .RequireRateLimiting("auth-recovery");

        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, IAuthService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.ForgotPasswordAsync(request, cancellationToken)))
            .RequireRateLimiting("auth-recovery");

        group.MapPost("/reset-password", async (ResetPasswordRequest request, IAuthService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.ResetPasswordAsync(request, cancellationToken)))
            .RequireRateLimiting("auth-recovery");

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

        group.MapDelete("/me", async (HttpContext httpContext, ClaimsPrincipal user, IAuthService service, IAuthCookieService authCookieService, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(async () =>
            {
                var response = await service.DeleteCurrentUserAsync(user.GetUserId(), cancellationToken);
                authCookieService.ClearAuthCookie(httpContext);
                return response;
            }))
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
