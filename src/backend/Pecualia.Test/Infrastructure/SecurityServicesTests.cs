using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Test.Infrastructure;

public sealed class SecurityServicesTests
{
    [Fact]
    public void BcryptPasswordHasher_HashesAndVerifiesPasswords()
    {
        var hasher = new BcryptPasswordHasher();

        var hash = hasher.Hash("Pecualia-1234");

        hash.Should().NotBeNullOrWhiteSpace();
        hasher.Verify("Pecualia-1234", hash).Should().BeTrue();
        hasher.Verify("otra-clave", hash).Should().BeFalse();
    }

    [Fact]
    public void AccountActivationService_GeneratesHashMatchingPlainToken()
    {
        var service = new AccountActivationService();

        var (plainToken, hash) = service.GenerateTokenPair();

        plainToken.Should().NotBeNullOrWhiteSpace();
        plainToken.Should().NotContain("+");
        plainToken.Should().NotContain("/");
        hash.Should().Be(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainToken))));
    }

    [Fact]
    public void JwtTokenService_CreateToken_EmbedsUserClaimsAndEmail()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "pecualia-tests",
            Audience = "pecualia-clients",
            SigningKey = "pecualia-tests-signing-key-0123456789012345",
            ExpirationMinutes = 60
        });
        var service = new JwtTokenService(options);

        var token = service.CreateToken(new AppUser
        {
            Id = 15,
            Role = UserRole.Manager,
            Email = "manager@pecualia.test"
        });

        var parsedToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
        parsedToken.Issuer.Should().Be("pecualia-tests");
        parsedToken.Audiences.Should().Contain("pecualia-clients");
        parsedToken.Claims.Should().Contain(claim => claim.Type == AuthClaimTypes.UserId && claim.Value == "15");
        parsedToken.Claims.Should().Contain(claim => claim.Type == AuthClaimTypes.Role && claim.Value == nameof(UserRole.Manager));
        parsedToken.Claims.Should().Contain(claim => claim.Type == JwtRegisteredClaimNames.Email && claim.Value == "manager@pecualia.test");
    }
}
