using Pecualia.Api.Contracts.Dashboard;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class PendingTaskQueryServiceTests
{
    [Fact]
    public async Task GetPendingTasksAsync_ReturnsCombinedTasks_ForManagerAccessibleFarms()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var managerUser = ServiceTestData.CreateUser(1, UserRole.Manager, "Marta", "Gestora", email: "manager@test.local");
        var farmerUser = ServiceTestData.CreateUser(2, UserRole.Farmer, "Pablo", "Ganadero", email: "farmer@test.local");
        var farmer = ServiceTestData.CreateFarmer(farmerUser.Id, farmerUser, managerId: managerUser.Id, nifCif: "12345678Z");
        var ovineFarm = ServiceTestData.CreateFarm(100, farmer.UserId, LivestockSpecies.Ovine, "Finca ovina", "ES410010000100");
        var porcineFarm = ServiceTestData.CreateFarm(101, farmer.UserId, LivestockSpecies.Porcine, "Finca porcina", "ES410010000101");
        var ovineAnimal = ServiceTestData.CreateAnimal(1000, ovineFarm.Id, "ES123456789012", new DateOnly(2026, 1, 10), birthYear: 2025);
        var porcineBirth = ServiceTestData.CreateBirth(2000, porcineFarm.Id, new DateOnly(2026, 2, 15), 6);
        var vaccination = new Vaccination
        {
            Id = 3000,
            AnimalId = ovineAnimal.Id,
            VaccinationDate = new DateOnly(2026, 5, 1),
            NextDose = new DateOnly(2026, 5, 25),
            VaccinationType = "Brucelosis"
        };
        var inspection = new Inspection
        {
            Id = 4000,
            LivestockFarmId = ovineFarm.Id,
            InspectionDate = new DateOnly(2026, 5, 30),
            Reason = "CI",
            LivestockFarm = ovineFarm
        };
        var movement = new MovementCertificate
        {
            Id = 5000,
            DestinationLivestockId = ovineFarm.Id,
            Status = MovementStatus.Pending,
            ArrivalDate = new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Utc),
            DepartureDate = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc),
            CodRemo = "REMO-PEND",
            Specie = LivestockSpecies.Ovine.ToString(),
            NumberOfAnimals = 1
        };
        var consumedAnimal = ServiceTestData.CreateAnimal(
            1001,
            porcineFarm.Id,
            "GT1800001001",
            new DateOnly(2026, 5, 1),
            birthDate: new DateOnly(2026, 2, 15),
            birthYear: 2026,
            registrationCause: AnimalRegistrationCause.Autorreposicion,
            sourceBirthId: porcineBirth.Id);

        dbContext.Users.AddRange(managerUser, farmerUser);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.AddRange(ovineFarm, porcineFarm);
        dbContext.Animals.AddRange(ovineAnimal, consumedAnimal);
        dbContext.AnimalBirths.Add(porcineBirth);
        dbContext.Vaccinations.Add(vaccination);
        dbContext.Inspections.Add(inspection);
        dbContext.MovementCertificates.Add(movement);
        await dbContext.SaveChangesAsync();

        var service = new PendingTaskQueryService(dbContext);

        var tasks = await service.GetPendingTasksAsync(
            managerUser.Id,
            UserRole.Manager,
            new DateOnly(2026, 5, 20),
            new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        tasks.Should().HaveCount(4);
        tasks.Select(task => task.Kind).Should().BeEquivalentTo(
            [ "Vaccination", "MovementConfirmation", "Inspection", "PorcineTransition" ]);
        tasks.Should().Contain(task => task.Detail.Contains("Finca ovina"));
        tasks.Should().Contain(task => task.Detail.Contains("Finca porcina"));
    }

    [Fact]
    public async Task GetPendingTasksAsync_ReturnsEmpty_WhenFarmerHasNoAccessibleFarms()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var user = ServiceTestData.CreateUser(8, UserRole.Farmer, "Sin", "Fincas", email: "nofarms@test.local");

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new PendingTaskQueryService(dbContext);

        var tasks = await service.GetPendingTasksAsync(
            user.Id,
            UserRole.Farmer,
            new DateOnly(2026, 5, 20),
            new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        tasks.Should().BeEmpty();
    }
}
