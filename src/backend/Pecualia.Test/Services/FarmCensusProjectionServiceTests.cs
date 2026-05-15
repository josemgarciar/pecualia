using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class FarmCensusProjectionServiceTests
{
    [Fact]
    public async Task BuildSnapshotAsync_MapsResolvedPorcineBirth_ToIntermediateBuckets_BetweenThreeAndSixMonths()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = new FarmCensusProjectionService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 300);
        var birth = ServiceTestData.CreateBirth(7001, farm.Id, new DateOnly(2026, 02, 15), 6);
        var decision = new PorcineBirthTransitionDecision
        {
            BirthId = birth.Id,
            EffectiveDate = new DateOnly(2026, 05, 15),
            ToRears = 2,
            ToSowsReposition = 3,
            ToMalesReposition = 1,
            ResolvedAt = clock.UtcNow.UtcDateTime
        };
        birth.PorcineTransitionDecision = decision;

        dbContext.AnimalBirths.Add(birth);
        dbContext.PorcineBirthTransitionDecisions.Add(decision);
        await dbContext.SaveChangesAsync();

        var snapshot = await service.BuildSnapshotAsync(farm, new DateOnly(2026, 05, 15), CancellationToken.None);

        snapshot.Rears.Should().Be(2);
        snapshot.SowsReposition.Should().Be(3);
        snapshot.MalesReposition.Should().Be(1);
        snapshot.Baits.Should().Be(0);
        snapshot.SowsForLive.Should().Be(0);
        snapshot.Boars.Should().Be(0);
    }

    [Fact]
    public async Task BuildSnapshotAsync_EvolvesResolvedPorcineBirth_ToFinalBuckets_FromSixMonthsOnward()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 08, 15, 10, 0, 0, TimeSpan.Zero));
        var service = new FarmCensusProjectionService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 301);
        var birth = ServiceTestData.CreateBirth(7002, farm.Id, new DateOnly(2026, 02, 15), 6);
        var decision = new PorcineBirthTransitionDecision
        {
            BirthId = birth.Id,
            EffectiveDate = new DateOnly(2026, 05, 15),
            ToRears = 2,
            ToSowsReposition = 3,
            ToMalesReposition = 1,
            ResolvedAt = new DateTime(2026, 05, 15, 10, 0, 0, DateTimeKind.Utc)
        };
        birth.PorcineTransitionDecision = decision;

        dbContext.AnimalBirths.Add(birth);
        dbContext.PorcineBirthTransitionDecisions.Add(decision);
        await dbContext.SaveChangesAsync();

        var snapshot = await service.BuildSnapshotAsync(farm, new DateOnly(2026, 08, 15), CancellationToken.None);

        snapshot.Rears.Should().Be(0);
        snapshot.SowsReposition.Should().Be(0);
        snapshot.MalesReposition.Should().Be(0);
        snapshot.Baits.Should().Be(2);
        snapshot.SowsForLive.Should().Be(3);
        snapshot.Boars.Should().Be(1);
    }

    private static async Task<LivestockFarm> SeedPorcineFarmAsync(Pecualia.Api.Data.PecualiaDbContext dbContext, long userId)
    {
        var user = ServiceTestData.CreateUser(userId, UserRole.Farmer, "Por", "Cino", email: $"projection-{userId}@test.local");
        var farmer = ServiceTestData.CreateFarmer(userId, user, nifCif: $"000001{userId % 100:00}L");
        var farm = ServiceTestData.CreateFarm(userId + 2000, farmer.UserId, LivestockSpecies.Porcine, $"Porcina Proyección {userId}", $"ES4200100{userId:00000}", authorisedCapacity: 40, porcineMothersCapacity: 15, porcineFatteningCapacity: 25);

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();

        return farm;
    }
}
