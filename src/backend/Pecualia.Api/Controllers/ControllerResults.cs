using System.Security.Claims;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;

namespace Pecualia.Api.Controllers;

internal static class ControllerResults
{
    public static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (DomainException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    public static long GetUserId(this ClaimsPrincipal user)
    {
        return long.Parse(user.FindFirstValue(AuthClaimTypes.UserId)!);
    }

    public static UserRole GetRole(this ClaimsPrincipal user)
    {
        var role = user.FindFirstValue(AuthClaimTypes.Role) ?? user.FindFirstValue(ClaimTypes.Role);
        return Enum.Parse<UserRole>(role!);
    }
}
