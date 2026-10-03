using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Models.Enums;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Infrastructure;

public sealed class JwtSessionServiceTests
{
    private static readonly JwtOptions JwtOptions = new()
    {
        Issuer = "test", Audience = "test", SigningKey = "test-signing-key-at-least-32-characters"
    };

    [Theory]
    [InlineData("password")]
    [InlineData("inactive")]
    [InlineData("role")]
    [InlineData("deleted")]
    public async Task IssuedSession_IsInvalidatedWhenAccountSecurityChanges(string change)
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var user = ServiceTestData.CreateUser(1, UserRole.Farmer, "Ana", "Test");
        user.PasswordHash = "first-password-hash";
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var tokens = new JwtTokenService(Options.Create(JwtOptions));
        var sessions = new JwtSessionService(db, Options.Create(JwtOptions), tokens);
        var principal = ReadPrincipal(tokens.CreateToken(user));
        (await sessions.IsValidAsync(principal, default)).Should().BeTrue();

        switch (change)
        {
            case "password": user.PasswordHash = "changed-password-hash"; break;
            case "inactive": user.IsActive = false; break;
            case "role": user.Role = UserRole.Manager; break;
            case "deleted": db.Users.Remove(user); break;
        }
        await db.SaveChangesAsync();

        (await sessions.IsValidAsync(principal, default)).Should().BeFalse();
    }

    [Fact]
    public async Task LegacySignedSession_RemainsCompatibleUntilJwtExpiration()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var tokens = new JwtTokenService(Options.Create(JwtOptions));
        var sessions = new JwtSessionService(db, Options.Create(JwtOptions), tokens);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(AuthClaimTypes.UserId, "1"), new Claim(AuthClaimTypes.Role, "Farmer")], "Bearer"));

        (await sessions.IsValidAsync(principal, default)).Should().BeTrue();
    }

    [Fact]
    public async Task RenewedSession_UsesCurrentPasswordWithoutExposingHash()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var user = ServiceTestData.CreateUser(1, UserRole.Farmer, "Ana", "Test");
        user.PasswordHash = "current-password-hash";
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var tokens = new JwtTokenService(Options.Create(JwtOptions));
        var sessions = new JwtSessionService(db, Options.Create(JwtOptions), tokens);

        var principal = ReadPrincipal(await sessions.CreateCurrentTokenAsync(user.Id, default));

        (await sessions.IsValidAsync(principal, default)).Should().BeTrue();
        principal.Claims.Should().NotContain(claim => claim.Value.Contains(user.PasswordHash));
        principal.FindFirstValue(AuthClaimTypes.SessionStamp).Should().HaveLength(64);
    }

    private static ClaimsPrincipal ReadPrincipal(string token) =>
        new(new ClaimsIdentity(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims, "Bearer"));
}
