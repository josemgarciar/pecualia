using System.Net;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Controllers;

public sealed class CookieOriginIntegrationTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public CookieOriginIntegrationTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("https://attacker.test", HttpStatusCode.Forbidden)]
    [InlineData("http://127.0.0.1:4173", HttpStatusCode.OK)]
    public async Task Logout_ValidatesCookieRequestOrigin(string origin, HttpStatusCode expectedStatus)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("Cookie", "pecualia.auth=test-session");
        request.Headers.Add("Origin", origin);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(expectedStatus);
        response.Headers.Contains("Set-Cookie").Should().Be(expectedStatus == HttpStatusCode.OK);
    }
}
