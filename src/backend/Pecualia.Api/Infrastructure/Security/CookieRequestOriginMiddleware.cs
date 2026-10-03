using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;

namespace Pecualia.Api.Infrastructure.Security;

public sealed class CookieRequestOriginMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IOptions<AuthCookieOptions> cookieOptions,
        IOptions<FrontendOptions> frontendOptions)
    {
        var request = context.Request;
        if (RequiresOriginCheck(request, cookieOptions.Value.Name) &&
            !HasTrustedOrigin(request, frontendOptions.Value.Origin))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new { error = "El origen de la solicitud no está permitido." },
                context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static bool RequiresOriginCheck(HttpRequest request, string cookieName) =>
        request.Path.StartsWithSegments("/api") &&
        !HttpMethods.IsGet(request.Method) &&
        !HttpMethods.IsHead(request.Method) &&
        !HttpMethods.IsOptions(request.Method) &&
        request.Cookies.ContainsKey(cookieName);

    private static bool HasTrustedOrigin(HttpRequest request, string frontendOrigin)
    {
        var origin = request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin))
        {
            return string.Equals(origin, frontendOrigin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        // Preserve non-browser API clients; browsers must not bypass this check by omitting Origin.
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        return string.IsNullOrEmpty(fetchSite) ||
               string.Equals(fetchSite, "same-origin", StringComparison.OrdinalIgnoreCase);
    }
}
