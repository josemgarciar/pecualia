using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Farms;
using Pecualia.Api.Models.Entities;
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
    public async Task CreateFarmAsync_KeepsMaxCapacity_WhenActiveAutorenewExpirationIsStale()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 07, 29, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var service = new FarmService(dbContext, clock, censusProjectionService);

        var managerUser = ServiceTestData.CreateUser(70, UserRole.Manager, "Max", "Gestora", email: "max-manager@test.local");
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        var farmerUser = ServiceTestData.CreateUser(71, UserRole.Farmer, "Eva", "Ganadera", email: "max-farmer@test.local");
        var farmer = ServiceTestData.CreateFarmer(farmerUser.Id, farmerUser, managerId: managerUser.Id, nifCif: "00000014D");
        var farm1 = ServiceTestData.CreateFarm(170, farmer.UserId, LivestockSpecies.Ovine, "Max 1", "ES410010000070");
        var farm2 = ServiceTestData.CreateFarm(171, farmer.UserId, LivestockSpecies.Caprine, "Max 2", "ES410010000071");
        var subscription = new Subscription
        {
            Id = 70,
            UserId = managerUser.Id,
            User = managerUser,
            PlanType = PlanType.Enterprise,
            State = SubscriptionState.Active,
            Autorenew = true,
            InitialDate = new DateOnly(2026, 05, 09),
            ExpirationDate = new DateOnly(2026, 06, 09)
        };

        dbContext.Users.AddRange(managerUser, farmerUser);
        dbContext.Managers.Add(manager);
        dbContext.Farmers.Add(farmer);
        dbContext.Subscriptions.Add(subscription);
        dbContext.Farms.AddRange(farm1, farm2);
        await dbContext.SaveChangesAsync();

        var created = await service.CreateFarmAsync(managerUser.Id, UserRole.Manager, CreateOvineRequest(
            farmer.UserId,
            "Max 3",
            "ES410010000072"), CancellationToken.None);

        created.Name.Should().Be("Max 3");
        dbContext.Farms.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateFarmAsync_ReturnsSameFarm_WhenIdenticalRequestIsRetriedAtPlanLimit()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 07, 29, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var service = new FarmService(dbContext, clock, censusProjectionService);

        var user = ServiceTestData.CreateUser(72, UserRole.Farmer, "Ida", "Ganadera", email: "idempotent-farmer@test.local");
        var farmer = ServiceTestData.CreateFarmer(user.Id, user, nifCif: "00000015E");
        var existingFarm = ServiceTestData.CreateFarm(172, farmer.UserId, LivestockSpecies.Caprine, "Previa", "ES410010000073");
        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(existingFarm);
        await dbContext.SaveChangesAsync();
        var request = CreateOvineRequest(farmer.UserId, "Reintentable", "ES410010000074");

        var first = await service.CreateFarmAsync(user.Id, UserRole.Farmer, request, CancellationToken.None);
        var retry = await service.CreateFarmAsync(user.Id, UserRole.Farmer, request, CancellationToken.None);

        retry.Id.Should().Be(first.Id);
        dbContext.Farms.Should().HaveCount(2);
        dbContext.Farms.Should().ContainSingle(entity => entity.RegaCode == request.RegaCode);
    }

    [Fact]
    public async Task CreateFarmAsync_RejectsSameRega_WhenRetryPayloadDoesNotMatch()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 07, 29, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var service = new FarmService(dbContext, clock, censusProjectionService);

        var user = ServiceTestData.CreateUser(73, UserRole.Farmer, "Única", "Ganadera", email: "unique-farmer@test.local");
        var farmer = ServiceTestData.CreateFarmer(user.Id, user, nifCif: "00000016F");
        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        await dbContext.SaveChangesAsync();
        const string regaCode = "ES410010000075";

        await service.CreateFarmAsync(
            user.Id,
            UserRole.Farmer,
            CreateOvineRequest(farmer.UserId, "Nombre original", regaCode),
            CancellationToken.None);

        var action = () => service.CreateFarmAsync(
            user.Id,
            UserRole.Farmer,
            CreateOvineRequest(farmer.UserId, "Nombre diferente", regaCode),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("*sus datos no coinciden*");
        dbContext.Farms.Should().ContainSingle(entity => entity.RegaCode == regaCode);
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

    [Fact]
    public async Task DeleteFarmAsync_DeletesAnimalsAndOwnMovements_ButPreservesSharedMovementHistory()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 08, 11, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var service = new FarmService(dbContext, clock, censusProjectionService);

        var user = ServiceTestData.CreateUser(80, UserRole.Farmer, "Elena", "Titular", email: "farm-delete@test.local");
        var farmer = ServiceTestData.CreateFarmer(user.Id, user, nifCif: "00000017G");
        var deletedFarm = ServiceTestData.CreateFarm(180, farmer.UserId, LivestockSpecies.Ovine, "Ovina eliminable", "ES410010000080");
        var survivingFarm = ServiceTestData.CreateFarm(181, farmer.UserId, LivestockSpecies.Ovine, "Ovina destino", "ES410010000081");
        var deletedAnimal = ServiceTestData.CreateAnimal(1800, deletedFarm.Id, "ES060000581800", new DateOnly(2026, 1, 1));
        var survivingAnimal = ServiceTestData.CreateAnimal(1801, survivingFarm.Id, "ES060000581801", new DateOnly(2026, 1, 1));
        var subtype = ServiceTestData.CreateOvinoCaprinoAnimal(deletedAnimal.Id, LivestockSpecies.Ovine);
        var vaccination = new Vaccination
        {
            Id = 1800,
            AnimalId = deletedAnimal.Id,
            VaccinationDate = new DateOnly(2026, 2, 1),
            VaccinationType = "Lengua azul"
        };
        var sharedMovement = new MovementCertificate
        {
            Id = 1800,
            OriginLivestockId = deletedFarm.Id,
            DestinationLivestockId = survivingFarm.Id,
            DepartureDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = MovementStatus.Confirmed,
            NumberOfAnimals = 2,
            Specie = LivestockSpecies.Ovine.ToString()
        };
        var ownMovement = new MovementCertificate
        {
            Id = 1801,
            OriginLivestockId = deletedFarm.Id,
            DestinationExternalCode = "ES410010999999",
            DepartureDate = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
            Status = MovementStatus.Confirmed,
            NumberOfAnimals = 1,
            Specie = LivestockSpecies.Ovine.ToString()
        };

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.AddRange(deletedFarm, survivingFarm);
        dbContext.Animals.AddRange(deletedAnimal, survivingAnimal);
        dbContext.OvinoCaprinoAnimals.Add(subtype);
        dbContext.Vaccinations.Add(vaccination);
        dbContext.MovementCertificates.AddRange(sharedMovement, ownMovement);
        dbContext.MovementCertificateAnimals.AddRange(
            new MovementCertificateAnimal { Id = 1800, MovementCertificateId = sharedMovement.Id, AnimalId = deletedAnimal.Id },
            new MovementCertificateAnimal { Id = 1801, MovementCertificateId = sharedMovement.Id, AnimalId = survivingAnimal.Id },
            new MovementCertificateAnimal { Id = 1802, MovementCertificateId = ownMovement.Id, AnimalId = deletedAnimal.Id });
        await dbContext.SaveChangesAsync();

        var result = await service.DeleteFarmAsync(user.Id, UserRole.Farmer, deletedFarm.Id, CancellationToken.None);

        result.Should().Be(new DeleteFarmResponse(deletedFarm.Id, 1, false));
        dbContext.Farms.Should().ContainSingle(entity => entity.Id == survivingFarm.Id);
        dbContext.Animals.Should().ContainSingle(entity => entity.Id == survivingAnimal.Id);
        dbContext.OvinoCaprinoAnimals.Should().BeEmpty();
        dbContext.Vaccinations.Should().BeEmpty();
        dbContext.MovementCertificates.Should().ContainSingle(entity => entity.Id == sharedMovement.Id);
        dbContext.MovementCertificateAnimals.Should().ContainSingle(entity => entity.AnimalId == survivingAnimal.Id);

        var preservedMovement = await dbContext.MovementCertificates.SingleAsync();
        preservedMovement.OriginLivestockId.Should().BeNull();
        preservedMovement.OriginExternalCode.Should().Be(deletedFarm.RegaCode);
        preservedMovement.OriginExternalName.Should().Be(deletedFarm.Name);
        preservedMovement.DestinationLivestockId.Should().Be(survivingFarm.Id);

        var retry = await service.DeleteFarmAsync(user.Id, UserRole.Farmer, deletedFarm.Id, CancellationToken.None);
        retry.Should().Be(new DeleteFarmResponse(deletedFarm.Id, 0, true));
        dbContext.Farms.Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteFarmAsync_DoesNotDeleteFarmOwnedByAnotherFarmer()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 08, 11, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var service = new FarmService(dbContext, clock, censusProjectionService);

        var ownerUser = ServiceTestData.CreateUser(82, UserRole.Farmer, "Sara", "Dueña", email: "farm-owner@test.local");
        var owner = ServiceTestData.CreateFarmer(ownerUser.Id, ownerUser, nifCif: "00000018H");
        var otherUser = ServiceTestData.CreateUser(83, UserRole.Farmer, "Raúl", "Ajeno", email: "farm-other@test.local");
        var other = ServiceTestData.CreateFarmer(otherUser.Id, otherUser, nifCif: "00000019J");
        var farm = ServiceTestData.CreateFarm(182, owner.UserId, LivestockSpecies.Caprine, "Caprina privada", "ES410010000082");
        var animal = ServiceTestData.CreateAnimal(1820, farm.Id, "ES060000581820", new DateOnly(2026, 1, 1));

        dbContext.Users.AddRange(ownerUser, otherUser);
        dbContext.Farmers.AddRange(owner, other);
        dbContext.Farms.Add(farm);
        dbContext.Animals.Add(animal);
        await dbContext.SaveChangesAsync();

        var action = () => service.DeleteFarmAsync(otherUser.Id, UserRole.Farmer, farm.Id, CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>().WithMessage("Explotación no encontrada.");
        dbContext.Farms.Should().ContainSingle(entity => entity.Id == farm.Id);
        dbContext.Animals.Should().ContainSingle(entity => entity.Id == animal.Id);
    }

    private static CreateFarmRequest CreateOvineRequest(long farmerId, string name, string regaCode) =>
        new(
            farmerId,
            name,
            regaCode,
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
            null);
}
