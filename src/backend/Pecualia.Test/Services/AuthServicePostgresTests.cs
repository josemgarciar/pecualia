using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pecualia.Api.Contracts.Auth;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed partial class AuthServiceTests
{
    [PostgresFact]
    public Task ResetPasswordAsync_ConsumesSameTokenOnlyOnce_WhenRequestsRace() =>
        VerifyConcurrentPasswordResetAsync(useSameToken: true);

    [PostgresFact]
    public Task ResetPasswordAsync_ConsumesDifferentTokensOnlyOnce_WhenRequestsRace() =>
        VerifyConcurrentPasswordResetAsync(useSameToken: false);

    private static async Task VerifyConcurrentPasswordResetAsync(bool useSameToken)
    {
        await using var database = new PostgresTestDatabase();
        await database.InitializeAsync(useCurrentModel: true);
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        long userId;
        await using (var setup = database.CreateContext())
        {
            var user = ServiceTestData.CreateUser(0, UserRole.Farmer, "Reset", "Concurrente", email: "reset@test.local");
            var otherUser = ServiceTestData.CreateUser(0, UserRole.Farmer, "Otro", "Usuario", email: "other@test.local");
            setup.Users.AddRange(user, otherUser);
            await setup.SaveChangesAsync();
            userId = user.Id;
            setup.PasswordResetTokens.AddRange(
                CreateToken(user, "first-token"),
                CreateToken(user, "second-token"),
                CreateToken(otherUser, "other-token"));
            await setup.SaveChangesAsync();
        }

        var barrier = new ResetReadBarrier();
        await using var firstContext = database.CreateContext(new SynchronizeFirstResetRead(barrier));
        await using var secondContext = database.CreateContext(new SynchronizeFirstResetRead(barrier));
        var firstService = CreateService(firstContext, clock, new CapturingEmailSender());
        var secondService = CreateService(secondContext, clock, new CapturingEmailSender());
        var results = await Task.WhenAll(
            CaptureResetAsync(firstService, "first-token", "first-password"),
            CaptureResetAsync(secondService, useSameToken ? "first-token" : "second-token", "second-password"));

        results.Should().ContainSingle(result => result.Error == null);
        results.Should().ContainSingle(result => result.Error is DomainException);
        var winner = results.Single(result => result.Error == null);
        await using var verify = database.CreateContext();
        var savedUser = await verify.Users.SingleAsync(user => user.Id == userId);
        savedUser.PasswordHash.Should().Be($"hash::{winner.Password}");
        var tokens = await verify.PasswordResetTokens.ToListAsync();
        tokens.Where(token => token.UserId == userId).Should().OnlyContain(token => token.UsedAt == clock.UtcNow);
        tokens.Single(token => token.UserId != userId).UsedAt.Should().BeNull();

        PasswordResetToken CreateToken(AppUser user, string value) => new()
        {
            User = user,
            TokenHash = ComputeTokenHash(value),
            CreatedAt = clock.UtcNow,
            ExpiresAt = clock.UtcNow.AddMinutes(30)
        };
    }

    private static async Task<(string Password, Exception? Error)> CaptureResetAsync(
        AuthService service, string token, string password)
    {
        var error = await Record.ExceptionAsync(() => service.ResetPasswordAsync(
            new ResetPasswordRequest(token, password), CancellationToken.None));
        return (password, error);
    }

    private sealed class ResetReadBarrier
    {
        private readonly TaskCompletionSource _bothRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readCount;

        public async Task WaitForBothReadsAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _readCount) == 2)
            {
                _bothRead.TrySetResult();
            }

            await _bothRead.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private sealed class SynchronizeFirstResetRead(ResetReadBarrier barrier) : DbCommandInterceptor
    {
        private bool _hasRead;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (!_hasRead && command.CommandText.Contains("password_reset_token", StringComparison.Ordinal))
            {
                _hasRead = true;
                await barrier.WaitForBothReadsAsync(cancellationToken);
            }

            return result;
        }
    }
}
