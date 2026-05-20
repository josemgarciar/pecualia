using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;
using static Pecualia.Test.Services.PerformanceTestSupport;

namespace Pecualia.Test.Services;

[Trait("Category", "Performance")]
[Trait("PerformanceModule", "Transversal")]
public sealed class TransversalPerformanceTests
{
    [Fact]
    public async Task GetSummaryAsync_CompletesWithinBudget_ForManagerDashboard()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 20, 10, 0, 0, TimeSpan.Zero));
        var service = new DashboardService(dbContext, clock);

        var managerUser = ServiceTestData.CreateUser(200, UserRole.Manager, "Gestor", "Principal", email: "manager-performance@test.local");
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        dbContext.Users.Add(managerUser);
        dbContext.Managers.Add(manager);

        var farmers = new List<Farmer>(4);
        var farms = new List<LivestockFarm>(8);
        var animals = new List<Animal>(1600);
        var ovineAnimals = new List<OvinoCaprinoAnimal>(800);
        var porcineAnimals = new List<PorcinoAnimal>(800);
        var births = new List<AnimalBirth>(80);
        var vaccinations = new List<Vaccination>(200);
        var inspections = new List<Inspection>(80);
        var movements = new List<MovementCertificate>(160);

        for (var farmerIndex = 0; farmerIndex < 4; farmerIndex++)
        {
            var farmerUserId = 210 + farmerIndex;
            var farmerUser = ServiceTestData.CreateUser(
                farmerUserId,
                UserRole.Farmer,
                $"Farmer {farmerIndex}",
                "Performance",
                email: $"farmer-performance-{farmerIndex}@test.local");
            var farmer = ServiceTestData.CreateFarmer(
                farmerUserId,
                farmerUser,
                managerId: managerUser.Id,
                nifCif: $"0000000{farmerIndex}T",
                status: farmerIndex == 0 ? FarmerStatus.PendingActivation : FarmerStatus.Active);

            farmers.Add(farmer);
            dbContext.Users.Add(farmerUser);
            dbContext.Farmers.Add(farmer);

            for (var farmIndex = 0; farmIndex < 2; farmIndex++)
            {
                var sequence = (farmerIndex * 2) + farmIndex;
                var species = sequence % 2 == 0 ? LivestockSpecies.Ovine : LivestockSpecies.Porcine;
                var farm = ServiceTestData.CreateFarm(
                    500 + sequence,
                    farmer.UserId,
                    species,
                    $"Farm performance {sequence}",
                    BuildRegaCode(500 + sequence),
                    authorisedCapacity: species == LivestockSpecies.Porcine ? 800 : null,
                    porcineMothersCapacity: species == LivestockSpecies.Porcine ? 300 : null,
                    porcineFatteningCapacity: species == LivestockSpecies.Porcine ? 500 : null);

                farms.Add(farm);
                dbContext.Farms.Add(farm);

                for (var animalIndex = 0; animalIndex < 200; animalIndex++)
                {
                    var animalId = 50_000 + (sequence * 200) + animalIndex;
                    var registrationDate = new DateOnly(2025, 11, 01).AddDays((animalIndex + sequence) % 170);
                    var animal = ServiceTestData.CreateAnimal(
                        animalId,
                        farm.Id,
                        BuildAnimalIdentification(species, animalId),
                        registrationDate,
                        birthDate: registrationDate.AddDays(-180),
                        birthYear: registrationDate.Year - 1,
                        sex: animalIndex % 2 == 0 ? "female" : "male");

                    animals.Add(animal);
                    if (species == LivestockSpecies.Porcine)
                    {
                        porcineAnimals.Add(ServiceTestData.CreatePorcinoAnimal(animal.Id, animalIndex % 3 == 0 ? "Lechones" : "Cebo"));
                    }
                    else
                    {
                        ovineAnimals.Add(ServiceTestData.CreateOvinoCaprinoAnimal(animal.Id, LivestockSpecies.Ovine));
                    }

                    if (animalIndex < 25)
                    {
                        vaccinations.Add(new Vaccination
                        {
                            Id = 80_000 + (sequence * 25) + animalIndex,
                            AnimalId = animal.Id,
                            VaccinationDate = new DateOnly(2026, 05, 01).AddDays(-(animalIndex % 10)),
                            NextDose = new DateOnly(2026, 05, 20).AddDays(animalIndex % 14),
                            VaccinationType = "Lengua azul"
                        });
                    }
                }

                for (var birthIndex = 0; birthIndex < 10; birthIndex++)
                {
                    births.Add(ServiceTestData.CreateBirth(
                        90_000 + (sequence * 10) + birthIndex,
                        farm.Id,
                        new DateOnly(2026, 01, 15).AddDays(birthIndex * 7),
                        species == LivestockSpecies.Porcine ? 12 : 5));
                }

                for (var inspectionIndex = 0; inspectionIndex < 10; inspectionIndex++)
                {
                    inspections.Add(new Inspection
                    {
                        Id = 100_000 + (sequence * 10) + inspectionIndex,
                        LivestockFarmId = farm.Id,
                        InspectionDate = new DateOnly(2026, 05, 20).AddDays(inspectionIndex % 14),
                        Reason = "Control rutinario"
                    });
                }

                for (var movementIndex = 0; movementIndex < 20; movementIndex++)
                {
                    var movementId = 110_000 + (sequence * 20) + movementIndex;
                    var departureDate = new DateTime(2026, 05, 01, 0, 0, 0, DateTimeKind.Utc).AddDays(movementIndex % 20);
                    movements.Add(new MovementCertificate
                    {
                        Id = movementId,
                        OriginLivestockId = farm.Id,
                        DestinationExternalCode = $"EXT-{movementId}",
                        DestinationExternalName = "Destino externo",
                        DepartureDate = departureDate,
                        ArrivalDate = departureDate.AddDays(1),
                        NumberOfAnimals = 4 + (movementIndex % 3),
                        Specie = species.ToString(),
                        CodRemo = $"RM-{movementId}",
                        Status = MovementStatus.Confirmed
                    });
                }

                for (var pendingIndex = 0; pendingIndex < 5; pendingIndex++)
                {
                    var pendingId = 120_000 + (sequence * 5) + pendingIndex;
                    var arrivalDate = new DateTime(2026, 05, 18, 0, 0, 0, DateTimeKind.Utc).AddDays(-(pendingIndex % 3));
                    movements.Add(new MovementCertificate
                    {
                        Id = pendingId,
                        DestinationLivestockId = farm.Id,
                        OriginExternalCode = $"ORI-{pendingId}",
                        OriginExternalName = "Origen externo",
                        DepartureDate = arrivalDate.AddDays(-1),
                        ArrivalDate = arrivalDate,
                        NumberOfAnimals = 3,
                        Specie = species.ToString(),
                        CodRemo = $"PEND-{pendingId}",
                        Status = MovementStatus.Pending
                    });
                }
            }
        }

        dbContext.Animals.AddRange(animals);
        dbContext.OvinoCaprinoAnimals.AddRange(ovineAnimals);
        dbContext.PorcinoAnimals.AddRange(porcineAnimals);
        dbContext.AnimalBirths.AddRange(births);
        dbContext.Vaccinations.AddRange(vaccinations);
        dbContext.Inspections.AddRange(inspections);
        dbContext.MovementCertificates.AddRange(movements);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var measurement = await MeasureAsync(async () =>
        {
            _ = await service.GetSummaryAsync(managerUser.Id, UserRole.Manager, CancellationToken.None);
        });

        var summary = await service.GetSummaryAsync(managerUser.Id, UserRole.Manager, CancellationToken.None);

        summary.Farms.Should().Be(8);
        summary.ManagedFarmers.Should().Be(4);
        summary.PendingActivations.Should().Be(1);
        summary.TotalAnimals.Should().Be(1600);
        summary.MonthlyActivity.Should().HaveCount(7);
        summary.PendingTasks.Should().NotBeEmpty();
        AssertAverageBudget(measurement, 350);
    }

    [Fact]
    public async Task GetAccessibleFarmsAsync_CompletesWithinBudget_ForMixedPortfolio()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 20, 10, 0, 0, TimeSpan.Zero));
        var service = CreateFarmService(dbContext, clock);

        var managerUser = ServiceTestData.CreateUser(600, UserRole.Manager, "Gestor", "Portfolio", email: "manager-portfolio@test.local");
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        dbContext.Users.Add(managerUser);
        dbContext.Managers.Add(manager);

        var animals = new List<Animal>(2160);
        var births = new List<AnimalBirth>(90);

        for (var farmerIndex = 0; farmerIndex < 6; farmerIndex++)
        {
            var farmerUserId = 610 + farmerIndex;
            var farmerUser = ServiceTestData.CreateUser(
                farmerUserId,
                UserRole.Farmer,
                $"Portfolio {farmerIndex}",
                "Farmer",
                email: $"portfolio-{farmerIndex}@test.local");
            var farmer = ServiceTestData.CreateFarmer(farmerUserId, farmerUser, managerId: managerUser.Id, nifCif: $"1111111{farmerIndex}H");

            dbContext.Users.Add(farmerUser);
            dbContext.Farmers.Add(farmer);

            for (var farmIndex = 0; farmIndex < 3; farmIndex++)
            {
                var sequence = (farmerIndex * 3) + farmIndex;
                var species = (sequence % 3) switch
                {
                    0 => LivestockSpecies.Ovine,
                    1 => LivestockSpecies.Caprine,
                    _ => LivestockSpecies.Porcine
                };

                var farm = ServiceTestData.CreateFarm(
                    240_000 + sequence,
                    farmer.UserId,
                    species,
                    $"Farm portfolio {sequence}",
                    BuildRegaCode(700 + sequence),
                    authorisedCapacity: species == LivestockSpecies.Porcine ? 400 : null,
                    porcineMothersCapacity: species == LivestockSpecies.Porcine ? 150 : null,
                    porcineFatteningCapacity: species == LivestockSpecies.Porcine ? 250 : null);

                dbContext.Farms.Add(farm);

                for (var animalIndex = 0; animalIndex < 120; animalIndex++)
                {
                    var animalId = 250_000 + (sequence * 120) + animalIndex;
                    var birthDate = new DateOnly(2025, 03, 01).AddDays((animalIndex + sequence) % 280);
                    animals.Add(ServiceTestData.CreateAnimal(
                        animalId,
                        farm.Id,
                        BuildAnimalIdentification(species, animalId),
                        birthDate.AddDays(40),
                        birthDate: birthDate,
                        birthYear: birthDate.Year,
                        sex: animalIndex % 2 == 0 ? "female" : "male"));
                }

                for (var birthIndex = 0; birthIndex < 5; birthIndex++)
                {
                    births.Add(ServiceTestData.CreateBirth(
                        260_000 + (sequence * 5) + birthIndex,
                        farm.Id,
                        new DateOnly(2026, 01, 01).AddDays((birthIndex * 11) + sequence),
                        species == LivestockSpecies.Porcine ? 9 : 4));
                }
            }
        }

        dbContext.Animals.AddRange(animals);
        dbContext.AnimalBirths.AddRange(births);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var measurement = await MeasureAsync(async () =>
        {
            _ = await service.GetAccessibleFarmsAsync(managerUser.Id, UserRole.Manager, CancellationToken.None);
        });

        var accessibleFarms = await service.GetAccessibleFarmsAsync(managerUser.Id, UserRole.Manager, CancellationToken.None);

        accessibleFarms.Should().HaveCount(18);
        accessibleFarms.Should().Contain(entity => entity.LivestockSpecies == LivestockSpecies.Ovine.ToString());
        accessibleFarms.Should().Contain(entity => entity.LivestockSpecies == LivestockSpecies.Caprine.ToString());
        accessibleFarms.Should().Contain(entity => entity.LivestockSpecies == LivestockSpecies.Porcine.ToString());
        accessibleFarms.Should().OnlyContain(entity => entity.AnimalCount > 0);
        AssertAverageBudget(measurement, 450);
    }

    [Fact]
    public async Task GetManagedFarmersAsync_CompletesWithinBudget_ForLargeManagerPortfolio()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 20, 10, 0, 0, TimeSpan.Zero));
        var service = CreateFarmerService(dbContext, clock);

        var managerUser = ServiceTestData.CreateUser(700, UserRole.Manager, "Gestor", "Farmers", email: "manager-farmers@test.local");
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        dbContext.Users.Add(managerUser);
        dbContext.Managers.Add(manager);

        for (var farmerIndex = 0; farmerIndex < 180; farmerIndex++)
        {
            var farmerUserId = 710 + farmerIndex;
            var farmerUser = ServiceTestData.CreateUser(
                farmerUserId,
                UserRole.Farmer,
                $"Managed {farmerIndex}",
                "Farmer",
                email: $"managed-{farmerIndex}@test.local");
            var farmer = ServiceTestData.CreateFarmer(
                farmerUserId,
                farmerUser,
                managerId: managerUser.Id,
                nifCif: $"222222{farmerIndex % 10}{(char)('A' + (farmerIndex % 20))}",
                status: farmerIndex % 5 == 0 ? FarmerStatus.PendingActivation : FarmerStatus.Active);
            farmer.Province = farmerIndex % 2 == 0 ? "Sevilla" : "Córdoba";
            farmer.Town = $"Town {farmerIndex % 12}";

            dbContext.Users.Add(farmerUser);
            dbContext.Farmers.Add(farmer);

            for (var farmIndex = 0; farmIndex < 2; farmIndex++)
            {
                var farm = ServiceTestData.CreateFarm(
                    270_000 + (farmerIndex * 2) + farmIndex,
                    farmer.UserId,
                    farmIndex % 2 == 0 ? LivestockSpecies.Ovine : LivestockSpecies.Caprine,
                    $"Managed farm {farmerIndex}-{farmIndex}",
                    BuildRegaCode(900 + (farmerIndex * 2) + farmIndex));

                dbContext.Farms.Add(farm);
            }
        }

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var measurement = await MeasureAsync(async () =>
        {
            _ = await service.GetManagedFarmersAsync(managerUser.Id, "managed", "Sevilla", null, CancellationToken.None);
        });

        var farmers = await service.GetManagedFarmersAsync(managerUser.Id, "managed", "Sevilla", null, CancellationToken.None);

        farmers.Should().NotBeEmpty();
        farmers.Should().OnlyContain(entity => entity.Province == "Sevilla");
        farmers.Should().OnlyContain(entity => entity.FarmCount == 2);
        AssertAverageBudget(measurement, 200);
    }

    [Fact]
    public async Task GetManagedFarmerDetailAsync_CompletesWithinBudget_ForFarmerWithManyFarms()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 20, 10, 0, 0, TimeSpan.Zero));
        var service = CreateFarmerService(dbContext, clock);

        var managerUser = ServiceTestData.CreateUser(800, UserRole.Manager, "Gestor", "Detalle", email: "manager-detail@test.local");
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        var farmerUser = ServiceTestData.CreateUser(801, UserRole.Farmer, "Titular", "Detalle", email: "farmer-detail@test.local");
        var farmer = ServiceTestData.CreateFarmer(farmerUser.Id, farmerUser, managerId: managerUser.Id, nifCif: "33333333P");

        dbContext.Users.AddRange(managerUser, farmerUser);
        dbContext.Managers.Add(manager);
        dbContext.Farmers.Add(farmer);

        var animals = new List<Animal>(1920);
        var births = new List<AnimalBirth>(72);

        for (var farmIndex = 0; farmIndex < 12; farmIndex++)
        {
            var species = (farmIndex % 3) switch
            {
                0 => LivestockSpecies.Ovine,
                1 => LivestockSpecies.Caprine,
                _ => LivestockSpecies.Porcine
            };
            var farm = ServiceTestData.CreateFarm(
                280_000 + farmIndex,
                farmer.UserId,
                species,
                $"Detail farm {farmIndex}",
                BuildRegaCode(1_200 + farmIndex),
                authorisedCapacity: species == LivestockSpecies.Porcine ? 300 : null,
                porcineMothersCapacity: species == LivestockSpecies.Porcine ? 120 : null,
                porcineFatteningCapacity: species == LivestockSpecies.Porcine ? 180 : null);

            dbContext.Farms.Add(farm);

            for (var animalIndex = 0; animalIndex < 160; animalIndex++)
            {
                var animalId = 290_000 + (farmIndex * 160) + animalIndex;
                var birthDate = new DateOnly(2025, 02, 01).AddDays((animalIndex + farmIndex) % 300);
                animals.Add(ServiceTestData.CreateAnimal(
                    animalId,
                    farm.Id,
                    BuildAnimalIdentification(species, animalId),
                    birthDate.AddDays(45),
                    birthDate: birthDate,
                    birthYear: birthDate.Year,
                    sex: animalIndex % 2 == 0 ? "female" : "male"));
            }

            for (var birthIndex = 0; birthIndex < 6; birthIndex++)
            {
                births.Add(ServiceTestData.CreateBirth(
                    300_000 + (farmIndex * 6) + birthIndex,
                    farm.Id,
                    new DateOnly(2026, 01, 01).AddDays((birthIndex * 8) + farmIndex),
                    species == LivestockSpecies.Porcine ? 8 : 4));
            }
        }

        dbContext.Animals.AddRange(animals);
        dbContext.AnimalBirths.AddRange(births);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var measurement = await MeasureAsync(async () =>
        {
            _ = await service.GetManagedFarmerDetailAsync(managerUser.Id, farmerUser.Id, CancellationToken.None);
        });

        var detail = await service.GetManagedFarmerDetailAsync(managerUser.Id, farmerUser.Id, CancellationToken.None);

        detail.Farms.Should().HaveCount(12);
        detail.Farms.Should().OnlyContain(entity => entity.AnimalCount > 0);
        detail.Farms.Should().Contain(entity => entity.LivestockSpecies == LivestockSpecies.Porcine.ToString());
        AssertAverageBudget(measurement, 350);
    }
}
