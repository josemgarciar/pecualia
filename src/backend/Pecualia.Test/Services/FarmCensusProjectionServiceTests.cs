using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class FarmCensusProjectionServiceTests
{
    [Theory]
    [InlineData(LivestockSpecies.Ovine)]
    [InlineData(LivestockSpecies.Caprine)]
    public async Task Snapshot_UnlinkedAutorreposition_DoesNotConsumeIdentifiedYoungAnimals(LivestockSpecies species)
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var date = new DateOnly(2026, 9, 17);
        var service = new FarmCensusProjectionService(dbContext,
            new TestClock(new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero)));
        var farm = ServiceTestData.CreateFarm(1, 1, species, "Synthetic farm", "ES410010000001");
        dbContext.Farms.Add(farm);
        for (var i = 0; i < 7; i++)
        {
            dbContext.Animals.Add(new Animal
            {
                LivestockFarmId = farm.Id, Identification = $"TEST-YOUNG-{i}", Sex = "Male",
                BirthDate = new DateOnly(2025, 12, 15), RegistrationDate = new DateOnly(2026, 3, 9),
                RegistrationCause = AnimalRegistrationCause.Entrada
            });
        }
        for (var i = 0; i < 44; i++)
        {
            dbContext.Animals.Add(new Animal
            {
                LivestockFarmId = farm.Id, Identification = $"TEST-REPLACEMENT-{i}", Sex = "Female",
                RegistrationDate = new DateOnly(2026, 3, 1),
                RegistrationCause = AnimalRegistrationCause.Autorreposicion
            });
        }
        await dbContext.SaveChangesAsync();

        var snapshot = await service.BuildSnapshotAsync(farm, date, default);
        snapshot.NonReproductiveBetween4And12Months.Should().Be(7);
        snapshot.ReproductiveFemales.Should().Be(44);
        snapshot.Total.Should().Be(51);
        var census = await service.BuildCensusResponseAsync(farm, 2026, date, default);
        census.Total.Should().Be(51);
        var book = await service.BuildBookCensusesAsync(farm, default);
        book.Single(c => c.CensusDate.Year == 2026).OvinoCaprino!
            .NonReproductiveBetween4And12Months.Should().Be(7);
    }

    [Theory]
    [InlineData(LivestockSpecies.Ovine, 0, 20, 0)]
    [InlineData(LivestockSpecies.Caprine, 0, 20, 0)]
    [InlineData(LivestockSpecies.Ovine, 25, 20, 5)]
    [InlineData(LivestockSpecies.Caprine, 25, 20, 5)]
    public async Task OvineCensus_WithHistoricalUnidentifiedMovements_NeverReturnsNegativeCounts(
        LivestockSpecies species, int entries, int exits, int expectedUnderFourMonths)
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero));
        var service = new FarmCensusProjectionService(dbContext, clock);
        var farm = ServiceTestData.CreateFarm(1, 1, species, "Prueba ficticia", "ES410010000001");
        dbContext.Farms.Add(farm);
        // Process the exit first to verify the floor is applied to the final net count.
        dbContext.MovementCertificates.Add(new MovementCertificate
        {
            OriginLivestockId = farm.Id,
            DepartureDate = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
            NumberOfAnimals = exits,
            Specie = species.ToString(),
            UnidentifiedCategory = MovementUnidentifiedCategory.Under4Months
        });
        if (entries > 0)
        {
            dbContext.MovementCertificates.Add(new MovementCertificate
            {
                DestinationLivestockId = farm.Id,
                DepartureDate = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
                NumberOfAnimals = entries,
                Specie = species.ToString(),
                UnidentifiedCategory = MovementUnidentifiedCategory.Under4Months
            });
        }
        await dbContext.SaveChangesAsync();

        var snapshot = await service.BuildSnapshotAsync(farm, new DateOnly(2026, 9, 17), default);
        var censusResponse = await service.BuildCensusResponseAsync(farm, 2026, new DateOnly(2026, 9, 17), default);
        var bookCensuses = await service.BuildBookCensusesAsync(farm, default);

        snapshot.NonReproductiveUnder4Months.Should().Be(expectedUnderFourMonths);
        snapshot.Total.Should().Be(expectedUnderFourMonths);
        censusResponse.NonReproductiveUnder4Months.Should().Be(expectedUnderFourMonths);
        bookCensuses.Should().ContainSingle().Which.OvinoCaprino!.NonReproductiveUnder4Months.Should().Be(expectedUnderFourMonths);
    }

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
