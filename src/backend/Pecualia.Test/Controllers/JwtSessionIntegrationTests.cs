using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Pecualia.Api.Contracts.Auth;
using Pecualia.Api.Data;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Models.Enums;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Controllers;

public sealed class JwtSessionIntegrationTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public JwtSessionIntegrationTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PasswordChange_RevokesOldJwtAndRenewsCurrentCookie()
    {
        using var application = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })));
        using var client = application.CreateClient();
        string originalToken;
        using (var scope = application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PecualiaDbContext>();
            var user = ServiceTestData.CreateUser(88701, UserRole.Manager, "Ana", "Gestora", email: "jwt-test@test.local");
            user.Username = "jwt-test";
            user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash("Original-password-123");
            db.Users.Add(user);
            db.Managers.Add(ServiceTestData.CreateManager(user.Id, user));
            await db.SaveChangesAsync();
            originalToken = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().CreateToken(user);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", originalToken);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await client.PutAsJsonAsync("/api/auth/settings", new UpdateUserSettingsRequest(
            "Ana", "Gestora", "jwt-test@test.local", "jwt-test", "Gestoría Test", "Original-password-123", "New-password-456"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var renewedCookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("pecualia.auth=", StringComparison.Ordinal)).Split(';')[0];

        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Add("Cookie", renewedCookie);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Explicit bearer credentials must not be replaced by an ambient browser cookie.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", originalToken);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
