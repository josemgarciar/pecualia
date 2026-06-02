using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;

namespace Pecualia.Api.Services;

internal static class UserDeletionSupport
{
    public static async Task DeleteFarmerAccountsAsync(PecualiaDbContext dbContext, IReadOnlyCollection<Farmer> farmers, CancellationToken cancellationToken)
    {
        if (farmers.Count == 0)
        {
            return;
        }

        foreach (var farmer in farmers)
        {
            await DeleteUserArtifactsAsync(dbContext, farmer.UserId, cancellationToken);
        }

        dbContext.Farmers.RemoveRange(farmers);
        dbContext.Users.RemoveRange(farmers.Select(entity => entity.User));
    }

    public static async Task DeleteFarmerAccountAsync(PecualiaDbContext dbContext, Farmer farmer, CancellationToken cancellationToken)
    {
        await DeleteUserArtifactsAsync(dbContext, farmer.UserId, cancellationToken);
        dbContext.Farmers.Remove(farmer);
        dbContext.Users.Remove(farmer.User);
    }

    public static async Task DeleteUserArtifactsAsync(PecualiaDbContext dbContext, long userId, CancellationToken cancellationToken)
    {
        var activationTokens = await dbContext.AccountActivationTokens
            .Where(entity => entity.UserId == userId)
            .ToListAsync(cancellationToken);
        var passwordResetTokens = await dbContext.PasswordResetTokens
            .Where(entity => entity.UserId == userId)
            .ToListAsync(cancellationToken);
        var createdActivationTokens = await dbContext.AccountActivationTokens
            .Where(entity => entity.CreatedByUserId == userId)
            .ToListAsync(cancellationToken);

        if (activationTokens.Count != 0)
        {
            dbContext.AccountActivationTokens.RemoveRange(activationTokens);
        }

        if (passwordResetTokens.Count != 0)
        {
            dbContext.PasswordResetTokens.RemoveRange(passwordResetTokens);
        }

        foreach (var token in createdActivationTokens)
        {
            token.CreatedByUserId = null;
        }
    }
}
