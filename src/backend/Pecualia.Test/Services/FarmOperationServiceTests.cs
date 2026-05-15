using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class FarmOperationServiceTests
{
    [Fact]
    public async Task CreateBirthAsync_RejectsPorcineBirthOlderThanThreeMonths()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 100);

        var action = () => service.CreateBirthAsync(100, UserRole.Farmer, farm.Id, new CreateFarmBirthRequest(
            new DateOnly(2026, 02, 14),
            5,
            null,
            null), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("En porcino no puedes registrar nacimientos con más de 3 meses de antigüedad.");
    }

    [Fact]
    public async Task UpdateBirthAsync_RejectsWhenPorcineBirthLeavesAllowedWindow()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 101);
        var balance = new Pecualia.Api.Models.Entities.Balance
        {
            Id = 6001,
            LivestockFarmId = farm.Id,
            BalanceDate = new DateOnly(2026, 04, 01),
            ModificationCause = "Nacimiento",
            NumberOfAnimals = 4
        };
        var birth = ServiceTestData.CreateBirth(5001, farm.Id, new DateOnly(2026, 04, 01), 4, balance.Id);

        dbContext.Balances.Add(balance);
        dbContext.AnimalBirths.Add(birth);
        dbContext.BalancePorcino.Add(new Pecualia.Api.Models.Entities.BalancePorcino
        {
            BalanceId = balance.Id,
            Piglets = 4
        });
        await dbContext.SaveChangesAsync();

        var action = () => service.UpdateBirthAsync(101, UserRole.Farmer, farm.Id, birth.Id, new UpdateFarmBirthRequest(
            new DateOnly(2026, 02, 14),
            4,
            null,
            null), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("En porcino no puedes registrar nacimientos con más de 3 meses de antigüedad.");
    }

    [Fact]
    public async Task GetPendingPorcineTransitionsAsync_ReturnsBirthWhenThreeMonthsAreReached()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 102);
        var birth = ServiceTestData.CreateBirth(5002, farm.Id, new DateOnly(2026, 02, 15), 6);

        dbContext.AnimalBirths.Add(birth);
        await dbContext.SaveChangesAsync();

        var pending = await service.GetPendingPorcineTransitionsAsync(102, UserRole.Farmer, farm.Id, CancellationToken.None);

        pending.Should().ContainSingle();
        pending[0].BirthId.Should().Be(birth.Id);
        pending[0].PendingQuantity.Should().Be(6);
        pending[0].DueDate.Should().Be(new DateOnly(2026, 05, 15));
        pending[0].FinalTransitionDate.Should().Be(new DateOnly(2026, 08, 15));
        pending[0].IsOverdue.Should().BeFalse();
    }

    [Fact]
    public async Task GetPendingPorcineTransitionsAsync_MarksTaskAsOverdue_AfterSixMonths()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 09, 16, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 105);
        var birth = ServiceTestData.CreateBirth(5005, farm.Id, new DateOnly(2026, 02, 15), 6);

        dbContext.AnimalBirths.Add(birth);
        await dbContext.SaveChangesAsync();

        var pending = await service.GetPendingPorcineTransitionsAsync(105, UserRole.Farmer, farm.Id, CancellationToken.None);

        pending.Should().ContainSingle();
        pending[0].IsOverdue.Should().BeTrue();
    }

    [Fact]
    public async Task ResolvePorcineTransitionAsync_RejectsWhenBreakdownDoesNotMatchPendingAnimals()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 103);
        var birth = ServiceTestData.CreateBirth(5003, farm.Id, new DateOnly(2026, 02, 15), 6);

        dbContext.AnimalBirths.Add(birth);
        await dbContext.SaveChangesAsync();

        var action = () => service.ResolvePorcineTransitionAsync(103, UserRole.Farmer, farm.Id, birth.Id, new ResolvePorcineTransitionRequest(
            2,
            2,
            1), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("La suma del reparto debe coincidir exactamente con los animales pendientes de reclasificación.");
    }

    [Fact]
    public async Task ResolvePorcineTransitionAsync_CreatesDecisionAndPorcineBalance()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 104);
        var birth = ServiceTestData.CreateBirth(5004, farm.Id, new DateOnly(2026, 02, 15), 6);

        dbContext.AnimalBirths.Add(birth);
        await dbContext.SaveChangesAsync();

        var pendingAfterResolve = await service.ResolvePorcineTransitionAsync(104, UserRole.Farmer, farm.Id, birth.Id, new ResolvePorcineTransitionRequest(
            2,
            3,
            1), CancellationToken.None);

        pendingAfterResolve.Should().BeEmpty();

        var decision = await dbContext.PorcineBirthTransitionDecisions.SingleAsync(entity => entity.BirthId == birth.Id);
        decision.ToRears.Should().Be(2);
        decision.ToSowsReposition.Should().Be(3);
        decision.ToMalesReposition.Should().Be(1);
        decision.EffectiveDate.Should().Be(new DateOnly(2026, 05, 15));

        var balance = await dbContext.Balances.SingleAsync(entity => entity.Id == decision.BalanceId);
        balance.ModificationCause.Should().Be(AnimalRegistrationCause.Autorreposicion.ToString());
        balance.NumberOfAnimals.Should().Be(6);

        var balanceDetail = await dbContext.BalancePorcino.SingleAsync(entity => entity.BalanceId == balance.Id);
        balanceDetail.Piglets.Should().Be(6);
        balanceDetail.Rear.Should().Be(2);
        balanceDetail.SowsReposition.Should().Be(3);
        balanceDetail.PigsReposition.Should().Be(1);
        balanceDetail.Type.Should().Be("Reclasificación porcina");
    }

    private static FarmOperationService CreateService(Pecualia.Api.Data.PecualiaDbContext dbContext, TestClock clock)
    {
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        return new FarmOperationService(dbContext, clock, censusProjectionService);
    }

    private static async Task<Pecualia.Api.Models.Entities.LivestockFarm> SeedPorcineFarmAsync(Pecualia.Api.Data.PecualiaDbContext dbContext, long userId)
    {
        var user = ServiceTestData.CreateUser(userId, UserRole.Farmer, "Por", "Cino", email: $"porcine-{userId}@test.local");
        var farmer = ServiceTestData.CreateFarmer(userId, user, nifCif: $"000000{userId % 100:00}T");
        var farm = ServiceTestData.CreateFarm(userId + 1000, farmer.UserId, LivestockSpecies.Porcine, $"Porcina {userId}", $"ES4100100{userId:00000}", authorisedCapacity: 50, porcineMothersCapacity: 20, porcineFatteningCapacity: 30);

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();

        return farm;
    }
}
