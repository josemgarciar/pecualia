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

    [Fact]
    public async Task CreateFarmAsync_CreatesPorcineFarm_ForManagedFarmer()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var service = new FarmService(dbContext, clock, censusProjectionService);

        var managerUser = ServiceTestData.CreateUser(40, UserRole.Manager, "Marta", "Gestora", email: "farm-manager@test.local");
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        var farmerUser = ServiceTestData.CreateUser(41, UserRole.Farmer, "Paco", "Ganadero", email: "farm-farmer@test.local");
        var farmer = ServiceTestData.CreateFarmer(farmerUser.Id, farmerUser, managerId: managerUser.Id, nifCif: "00000011A");

        dbContext.Users.AddRange(managerUser, farmerUser);
        dbContext.Managers.Add(manager);
        dbContext.Farmers.Add(farmer);
        await dbContext.SaveChangesAsync();

        var created = await service.CreateFarmAsync(managerUser.Id, UserRole.Manager, new CreateFarmRequest(
            farmer.UserId,
            "Porcina nueva",
            "ES410010000040",
            LivestockSpecies.Porcine,
            FarmRegime.Intensive,
            "Sevilla",
            "Sevilla",
            "Camino 1",
            "41001",
            null,
            "PR-40",
            "Producción",
            12,
            18,
            "Responsable",
            "Multiplicación",
            30,
            123.45,
            678.9), CancellationToken.None);

        created.LivestockSpecies.Should().Be(LivestockSpecies.Porcine.ToString());
        created.AuthorisedCapacity.Should().Be(30);
        dbContext.Farms.Should().ContainSingle(entity => entity.Name == "Porcina nueva" && entity.PorcineRegistryNumber == "PR-40");
    }

    [Fact]
    public async Task UpdateFarmAsync_UpdatesPorcineCapacitiesAndMetadata()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var service = new FarmService(dbContext, clock, censusProjectionService);

        var farmerUser = ServiceTestData.CreateUser(50, UserRole.Farmer, "Rosa", "Titular", email: "farm-update@test.local");
        var farmer = ServiceTestData.CreateFarmer(50, farmerUser, nifCif: "00000012B");
        var farm = ServiceTestData.CreateFarm(150, farmer.UserId, LivestockSpecies.Porcine, "Porcina base", "ES410010000050", authorisedCapacity: 20, porcineMothersCapacity: 8, porcineFatteningCapacity: 12);

        dbContext.Users.Add(farmerUser);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();

        var updated = await service.UpdateFarmAsync(farmerUser.Id, UserRole.Farmer, farm.Id, new UpdateFarmRequest(
            "Porcina actualizada",
            "ES410010000051",
            FarmRegime.SemiExtensive,
            "Huelva",
            "Huelva",
            "Carretera 2",
            "21001",
            null,
            "PR-UPDATED",
            "Cebo",
            10,
            15,
            "Nueva responsable",
            "Producción",
            29,
            222.2,
            333.3), CancellationToken.None);

        updated.Name.Should().Be("Porcina actualizada");
        updated.AuthorisedCapacity.Should().Be(25);
        updated.PorcineRegistryNumber.Should().Be("PR-UPDATED");
        updated.Town.Should().Be("Huelva");
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsFarmerAndCapacityData()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var service = new FarmService(dbContext, clock, censusProjectionService);

        var farmerUser = ServiceTestData.CreateUser(60, UserRole.Farmer, "Clara", "Titular", email: "farm-summary@test.local");
        var farmer = ServiceTestData.CreateFarmer(60, farmerUser, nifCif: "00000013C");
        var farm = ServiceTestData.CreateFarm(160, farmer.UserId, LivestockSpecies.Porcine, "Porcina resumen", "ES410010000060", authorisedCapacity: 12, porcineMothersCapacity: 5, porcineFatteningCapacity: 7);
        var birth = ServiceTestData.CreateBirth(3001, farm.Id, new DateOnly(2026, 05, 1), 2);

        dbContext.Users.Add(farmerUser);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        dbContext.AnimalBirths.Add(birth);
        await dbContext.SaveChangesAsync();

        var summary = await service.GetSummaryAsync(farmerUser.Id, UserRole.Farmer, farm.Id, CancellationToken.None);

        summary.FarmerName.Should().Be("Clara Titular");
        summary.AuthorisedCapacity.Should().Be(12);
        summary.AnimalCount.Should().Be(2);
    }
}
