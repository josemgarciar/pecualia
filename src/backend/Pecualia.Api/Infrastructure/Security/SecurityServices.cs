using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using BCrypt.Net;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pecualia.Api.Configuration;
using Pecualia.Api.Models.Entities;

namespace Pecualia.Api.Infrastructure.Security;

public static class AuthClaimTypes
{
    public const string UserId = "userId";
    public const string Role = "role";
}

public static class AuthorizationPolicies
{
    public const string ManagerOnly = "manager-only";
    public const string FarmerOrManager = "farmer-or-manager";
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}

public interface IJwtTokenService
{
    string CreateToken(AppUser user);
}

public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public string CreateToken(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(AuthClaimTypes.UserId, user.Id.ToString()),
            new(AuthClaimTypes.Role, user.Role.ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public interface IAuthCookieService
{
    void AppendAuthCookie(HttpContext httpContext, string token);

    void ClearAuthCookie(HttpContext httpContext);
}

public sealed class AuthCookieService(IOptions<AuthCookieOptions> options) : IAuthCookieService
{
    private readonly AuthCookieOptions _options = options.Value;

    public void AppendAuthCookie(HttpContext httpContext, string token)
    {
        httpContext.Response.Cookies.Append(
            _options.Name,
            token,
            BuildCookieOptions(TimeSpan.FromMinutes(_options.ExpirationMinutes)));
    }

    public void ClearAuthCookie(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(_options.Name, BuildCookieOptions(null));
    }

    private CookieOptions BuildCookieOptions(TimeSpan? maxAge)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Secure = _options.Secure,
            SameSite = _options.SameSite,
            Domain = string.IsNullOrWhiteSpace(_options.Domain) ? null : _options.Domain,
            Path = "/",
            MaxAge = maxAge
        };
    }
}

public interface IAccountActivationService
{
    (string PlainToken, string Hash) GenerateTokenPair();
}

public sealed class AccountActivationService : IAccountActivationService
{
    public (string PlainToken, string Hash) GenerateTokenPair()
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var plainToken = Convert.ToBase64String(rawBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainToken)));
        return (plainToken, hash);
    }
}
