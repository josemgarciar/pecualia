using Pecualia.Api.Contracts.Farms;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class FarmServiceTests
{
    [Fact]
    public async Task GetDetailAsync_UsesCurrentCensusTotal_ForPorcineFarm()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var service = new FarmService(dbContext, clock, censusProjectionService);

        var farmerUser = ServiceTestData.CreateUser(10, UserRole.Farmer, "Ana", "Ganadera", email: "ana@test.local");
        var farmer = ServiceTestData.CreateFarmer(10, farmerUser, nifCif: "12345678Z");
        var farm = ServiceTestData.CreateFarm(100, farmer.UserId, LivestockSpecies.Porcine, "Porcina 1", "ES410010000001", authorisedCapacity: 20, porcineMothersCapacity: 10, porcineFatteningCapacity: 10);
        var birth = ServiceTestData.CreateBirth(1000, farm.Id, new DateOnly(2026, 04, 01), 7);

        dbContext.Users.Add(farmerUser);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        dbContext.AnimalBirths.Add(birth);
        await dbContext.SaveChangesAsync();

        var detail = await service.GetDetailAsync(farmerUser.Id, UserRole.Farmer, farm.Id, CancellationToken.None);

        detail.Should().BeOfType<FarmDetailResponse>();
        detail.AnimalCount.Should().Be(7);
    }

    [Fact]
    public async Task GetAccessibleFarmsAsync_UsesCurrentCensusTotal_InFarmList()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var service = new FarmService(dbContext, clock, censusProjectionService);

        var farmerUser = ServiceTestData.CreateUser(11, UserRole.Farmer, "Luis", "Titular", email: "luis@test.local");
        var farmer = ServiceTestData.CreateFarmer(11, farmerUser, nifCif: "87654321X");
        var farm = ServiceTestData.CreateFarm(101, farmer.UserId, LivestockSpecies.Ovine, "Ovina 1", "ES410010000002");
        var birth = ServiceTestData.CreateBirth(1001, farm.Id, new DateOnly(2026, 03, 01), 4);

        dbContext.Users.Add(farmerUser);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        dbContext.AnimalBirths.Add(birth);
        await dbContext.SaveChangesAsync();

        var farms = await service.GetAccessibleFarmsAsync(farmerUser.Id, UserRole.Farmer, CancellationToken.None);

        farms.Should().ContainSingle();
        farms[0].AnimalCount.Should().Be(4);
    }

    [Fact]
    public async Task GetDetailAsync_Throws_WhenFarmIsNotAccessibleForFarmer()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var service = new FarmService(dbContext, clock, censusProjectionService);

        var ownerUser = ServiceTestData.CreateUser(20, UserRole.Farmer, "Olga", "Dueña", email: "olga@test.local");
        var owner = ServiceTestData.CreateFarmer(20, ownerUser, nifCif: "00000000T");
        var otherUser = ServiceTestData.CreateUser(21, UserRole.Farmer, "Pepe", "Ajeno", email: "pepe@test.local");
        var otherFarmer = ServiceTestData.CreateFarmer(21, otherUser, nifCif: "00000001R");
        var farm = ServiceTestData.CreateFarm(120, owner.UserId, LivestockSpecies.Ovine, "Ovina Privada", "ES410010000020");

        dbContext.Users.AddRange(ownerUser, otherUser);
        dbContext.Farmers.AddRange(owner, otherFarmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();

        var action = () => service.GetDetailAsync(otherUser.Id, UserRole.Farmer, farm.Id, CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Explotación no encontrada.");
    }

    [Fact]
    public async Task CreateFarmAsync_Rejects_WhenFarmerExceedsBasicPlanLimit()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var service = new FarmService(dbContext, clock, censusProjectionService);

        var farmerUser = ServiceTestData.CreateUser(30, UserRole.Farmer, "Rosa", "Titular", email: "rosa@test.local");
        var farmer = ServiceTestData.CreateFarmer(30, farmerUser, nifCif: "00000002W");
        var farm1 = ServiceTestData.CreateFarm(130, farmer.UserId, LivestockSpecies.Ovine, "Finca 1", "ES410010000030");
        var farm2 = ServiceTestData.CreateFarm(131, farmer.UserId, LivestockSpecies.Caprine, "Finca 2", "ES410010000031");

        dbContext.Users.Add(farmerUser);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.AddRange(farm1, farm2);
        await dbContext.SaveChangesAsync();

        var action = () => service.CreateFarmAsync(farmerUser.Id, UserRole.Farmer, new CreateFarmRequest(
            farmer.UserId,
            "Finca 3",
            "ES410010000032",
            LivestockSpecies.Ovine,
            FarmRegime.Intensive,
            "Sevilla",
            "Sevilla",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            30,
            null,
            null), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("El plan Free permite hasta 2 explotaciones. Cambia de plan para crear más.");
    }
}
