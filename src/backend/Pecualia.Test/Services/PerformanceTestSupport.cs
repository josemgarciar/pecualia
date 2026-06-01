using System.Diagnostics;
using Pecualia.Api.Contracts.Auth;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

internal static class PerformanceTestSupport
{
    internal static AnimalService CreateAnimalService(PecualiaDbContext dbContext, TestClock clock)
    {
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        return new AnimalService(dbContext, censusProjectionService, clock);
    }

    internal static FarmService CreateFarmService(PecualiaDbContext dbContext, TestClock clock)
    {
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        return new FarmService(dbContext, clock, censusProjectionService);
    }

    internal static FarmerService CreateFarmerService(PecualiaDbContext dbContext, TestClock clock)
    {
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        return new FarmerService(dbContext, new FakeAuthService(), clock, censusProjectionService);
    }

    internal static MovementService CreateMovementService(PecualiaDbContext dbContext, TestClock clock)
    {
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        return new MovementService(dbContext, censusProjectionService, clock);
    }

    internal static async Task<LivestockFarm> SeedFarmAsync(
        PecualiaDbContext dbContext,
        long userId,
        LivestockSpecies species,
        string regaCode,
        int? authorisedCapacity = null,
        int? porcineMothersCapacity = null,
        int? porcineFatteningCapacity = null)
    {
        var user = ServiceTestData.CreateUser(userId, UserRole.Farmer, "Titular", "Performance", email: $"performance-{userId}@test.local");
        var farmer = ServiceTestData.CreateFarmer(userId, user, nifCif: $"1234567{userId % 10}Z");
        var farm = ServiceTestData.CreateFarm(userId + 5000, farmer.UserId, species, $"Farm {userId}", regaCode, authorisedCapacity, porcineMothersCapacity, porcineFatteningCapacity);

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();

        return farm;
    }

    internal static async Task<PerformanceMeasurement> MeasureAsync(Func<Task> action, int iterations = 5)
    {
        await action();

        var samples = new List<TimeSpan>(iterations);
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var stopwatch = Stopwatch.StartNew();
            await action();
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed);
        }

        return new PerformanceMeasurement(
            TimeSpan.FromMilliseconds(samples.Average(entity => entity.TotalMilliseconds)),
            samples.Max());
    }

    internal static void AssertAverageBudget(PerformanceMeasurement measurement, double maxAverageMilliseconds)
    {
        measurement.Average.TotalMilliseconds.Should().BeLessThan(
            maxAverageMilliseconds,
            "media {0:F1} ms, máximo {1:F1} ms",
            measurement.Average.TotalMilliseconds,
            measurement.Maximum.TotalMilliseconds);
    }

    internal static string BuildRegaCode(int index) => $"ES41{index:010}";

    internal static string BuildOfficialIdentification(int index) => $"ES{index:D12}";

    internal static string BuildAnimalIdentification(LivestockSpecies species, long index)
    {
        return species == LivestockSpecies.Porcine
            ? $"GT{index}"
            : BuildOfficialIdentification((int)(index % 999_999_999_999L));
    }

    internal sealed class FakeAuthService : IAuthService
    {
        public Task<AuthSessionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AuthSessionResult> RegisterManagerAsync(RegisterManagerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AuthSessionResult> RegisterFarmerAsync(RegisterFarmerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ActivationResponse> ActivateAccountAsync(ActivateAccountRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ActivationResponse> ResendActivationAsync(ResendActivationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<UserProfileResponse?> GetCurrentUserAsync(long userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<UserProfileResponse> UpdateCurrentUserSettingsAsync(long userId, UpdateUserSettingsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    internal sealed record PerformanceMeasurement(TimeSpan Average, TimeSpan Maximum);
}
