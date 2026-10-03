using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;

namespace Pecualia.Api.Infrastructure.Security;

internal static class SessionStamp
{
    public static string Create(AppUser user, string signingKey) =>
        Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingKey),
            Encoding.UTF8.GetBytes($"{user.Id}:{user.PasswordHash}")));
}

public sealed class JwtSessionService(
    PecualiaDbContext dbContext,
    IOptions<JwtOptions> options,
    IJwtTokenService tokenService)
{
    public async Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var stamp = principal.FindFirstValue(AuthClaimTypes.SessionStamp);
        // Signed tokens issued before session stamps remain usable until their existing expiration.
        if (stamp is null)
        {
            return true;
        }

        if (!long.TryParse(principal.FindFirstValue(AuthClaimTypes.UserId), out var userId))
        {
            return false;
        }

        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);
        var role = principal.FindFirstValue(AuthClaimTypes.Role) ?? principal.FindFirstValue(ClaimTypes.Role);
        return user is { IsActive: true } && role == user.Role.ToString() &&
               CryptographicOperations.FixedTimeEquals(
                   Encoding.UTF8.GetBytes(stamp),
                   Encoding.UTF8.GetBytes(SessionStamp.Create(user, options.Value.SigningKey)));
    }

    public async Task<string> CreateCurrentTokenAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .SingleAsync(entity => entity.Id == userId, cancellationToken);
        return tokenService.CreateToken(user);
    }
}
