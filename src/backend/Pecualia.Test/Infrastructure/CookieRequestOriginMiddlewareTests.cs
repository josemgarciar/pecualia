using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;
using Pecualia.Api.Infrastructure.Security;

namespace Pecualia.Test.Infrastructure;

public sealed class CookieRequestOriginMiddlewareTests
{
    [Theory]
    [InlineData("POST", "https://untrusted.test")]
    [InlineData("PUT", "https://pecualia.test.untrusted.test")]
    [InlineData("DELETE", "null")]
    [InlineData("PATCH", "http://pecualia.test")]
    [InlineData("POST", "https://pecualia.test:8443")]
    public async Task CookieMutation_RejectsUntrustedOrigins(string method, string origin)
    {
        var context = CreateContext(method);
        context.Request.Headers.Origin = origin;

        var wasCalled = await InvokeAsync(context);

        wasCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Theory]
    [InlineData("same-site")]
    [InlineData("cross-site")]
    [InlineData("none")]
    public async Task CookieMutation_RejectsBrowserRequestWithoutTrustedOrigin(string fetchSite)
    {
        var context = CreateContext("POST");
        context.Request.Headers["Sec-Fetch-Site"] = fetchSite;

        (await InvokeAsync(context)).Should().BeFalse();
    }

    [Fact]
    public async Task CookieMutation_AllowsConfiguredFrontendEvenAcrossSites()
    {
        var context = CreateContext("POST");
        context.Request.Headers.Origin = "https://pecualia.test";
        context.Request.Headers["Sec-Fetch-Site"] = "cross-site";

        (await InvokeAsync(context)).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("same-origin")]
    public async Task CookieMutation_PreservesNonBrowserAndSameOriginClients(string? fetchSite)
    {
        var context = CreateContext("POST");
        context.Request.Headers["Sec-Fetch-Site"] = fetchSite;

        (await InvokeAsync(context)).Should().BeTrue();
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task SafeRequests_AreNotBlocked(string method)
    {
        var context = CreateContext(method);
        context.Request.Headers.Origin = "https://untrusted.test";

        (await InvokeAsync(context)).Should().BeTrue();
    }

    [Fact]
    public async Task RequestWithoutCookie_PreservesWebhookAndBearerClients()
    {
        var context = CreateContext("POST");
        context.Request.Headers.Remove("Cookie");
        context.Request.Headers.Origin = "https://untrusted.test";

        (await InvokeAsync(context)).Should().BeTrue();
    }

    private static DefaultHttpContext CreateContext(string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/auth/logout";
        context.Request.Method = method;
        context.Request.Headers.Cookie = "pecualia.auth=test-session";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<bool> InvokeAsync(HttpContext context)
    {
        var wasCalled = false;
        var middleware = new CookieRequestOriginMiddleware(_ =>
        {
            wasCalled = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context,
            Options.Create(new AuthCookieOptions()),
            Options.Create(new FrontendOptions { Origin = "https://pecualia.test/" }));
        return wasCalled;
    }
}
