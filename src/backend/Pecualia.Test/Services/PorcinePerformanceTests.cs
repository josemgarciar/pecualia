using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;
using static Pecualia.Test.Services.PerformanceTestSupport;

namespace Pecualia.Test.Services;

[Trait("Category", "Performance")]
[Trait("PerformanceModule", "Porcine")]
public sealed class PorcinePerformanceTests
{
    [Fact]
    public async Task BuildSnapshotAsync_CompletesWithinBudget_ForDensePorcineFarm()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 20, 10, 0, 0, TimeSpan.Zero));
        var service = new FarmCensusProjectionService(dbContext, clock);
        var farm = await SeedFarmAsync(
            dbContext,
            300,
            LivestockSpecies.Porcine,
            BuildRegaCode(300),
            authorisedCapacity: 2000,
            porcineMothersCapacity: 800,
            porcineFatteningCapacity: 1200);

        var animals = new List<Animal>(1200);
        var porcineAnimals = new List<PorcinoAnimal>(1200);
        var births = new List<AnimalBirth>(150);
        var balances = new List<Balance>(40);
        var balancePorcino = new List<BalancePorcino>(40);
        var movements = new List<MovementCertificate>(60);

        for (var index = 0; index < 1200; index++)
        {
            var animalId = 130_000 + index;
            var birthDate = new DateOnly(2025, 07, 01).AddDays(index % 240);
            animals.Add(ServiceTestData.CreateAnimal(
                animalId,
                farm.Id,
                $"GT{animalId}",
                birthDate.AddDays(30),
                birthDate: birthDate,
                birthYear: birthDate.Year,
                sex: index % 2 == 0 ? "female" : "male"));
            porcineAnimals.Add(ServiceTestData.CreatePorcinoAnimal(
                animalId,
                (index % 6) switch
                {
                    0 => "Lechones",
                    1 => "Recría",
                    2 => "Cebo",
                    3 => "Cerdas vida",
                    4 => "Hembras reposición",
                    _ => "Machos reposición"
                }));
        }

        for (var index = 0; index < 150; index++)
        {
            var birth = ServiceTestData.CreateBirth(
                140_000 + index,
                farm.Id,
                new DateOnly(2026, 01, 01).AddDays(index),
                10 + (index % 4));

            if (index % 3 == 0)
            {
                birth.PorcineTransitionDecision = new PorcineBirthTransitionDecision
                {
                    BirthId = birth.Id,
                    EffectiveDate = birth.BirthDate.AddMonths(3),
                    ToRears = 3,
                    ToSowsReposition = 4,
                    ToMalesReposition = 2,
                    ResolvedAt = new DateTime(2026, 04, 01, 0, 0, 0, DateTimeKind.Utc)
                };
            }

            births.Add(birth);
        }

        for (var index = 0; index < 60; index++)
        {
            movements.Add(new MovementCertificate
            {
                Id = 150_000 + index,
                OriginLivestockId = index % 2 == 0 ? farm.Id : null,
                DestinationLivestockId = index % 2 == 0 ? null : farm.Id,
                DepartureDate = new DateTime(2026, 03, 01, 0, 0, 0, DateTimeKind.Utc).AddDays(index),
                ArrivalDate = new DateTime(2026, 03, 02, 0, 0, 0, DateTimeKind.Utc).AddDays(index),
                NumberOfAnimals = 6 + (index % 5),
                AnimalType = index % 2 == 0 ? "Cebo" : "Lechones",
                Specie = LivestockSpecies.Porcine.ToString(),
                Status = MovementStatus.Confirmed
            });
        }

        for (var index = 0; index < 40; index++)
        {
            var balance = new Balance
            {
                Id = 160_000 + index,
                LivestockFarmId = farm.Id,
                BalanceDate = new DateOnly(2026, 02, 01).AddDays(index),
                ModificationCause = AnimalDischargeCause.Muerte.ToString(),
                OriginLivestockCode = BalanceMarkers.PorcineAggregateDeath,
                NumberOfAnimals = 2 + (index % 3)
            };

            balances.Add(balance);
            balancePorcino.Add(new BalancePorcino
            {
                BalanceId = balance.Id,
                Piglets = index % 2 == 0 ? balance.NumberOfAnimals : 0,
                Rear = index % 2 == 0 ? 0 : balance.NumberOfAnimals,
                Breed = "Ibérico",
                Type = index % 2 == 0 ? "Lechones" : "Recría"
            });
        }

        dbContext.Animals.AddRange(animals);
        dbContext.PorcinoAnimals.AddRange(porcineAnimals);
        dbContext.AnimalBirths.AddRange(births);
        dbContext.PorcineBirthTransitionDecisions.AddRange(
            births.Where(entity => entity.PorcineTransitionDecision is not null).Select(entity => entity.PorcineTransitionDecision!));
        dbContext.MovementCertificates.AddRange(movements);
        dbContext.Balances.AddRange(balances);
        dbContext.BalancePorcino.AddRange(balancePorcino);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var measurement = await MeasureAsync(async () =>
        {
            _ = await service.BuildSnapshotAsync(farm, new DateOnly(2026, 05, 20), CancellationToken.None);
        });

        var snapshot = await service.BuildSnapshotAsync(farm, new DateOnly(2026, 05, 20), CancellationToken.None);

        snapshot.Total.Should().BeGreaterThan(0);
        snapshot.LivestockSpecies.Should().Be(LivestockSpecies.Porcine.ToString());
        AssertAverageBudget(measurement, 225);
    }

    [Fact]
    public async Task GetFarmMovementsAsync_CompletesWithinBudget_ForDensePorcineHistory()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 20, 10, 0, 0, TimeSpan.Zero));
        var service = CreateMovementService(dbContext, clock);
        var farm = await SeedFarmAsync(
            dbContext,
            900,
            LivestockSpecies.Porcine,
            BuildRegaCode(1_400),
            authorisedCapacity: 600,
            porcineMothersCapacity: 250,
            porcineFatteningCapacity: 350);

        var movements = new List<MovementCertificate>(1600);
        for (var index = 0; index < 1600; index++)
        {
            var isExit = index % 2 == 0;
            var movementDate = new DateTime(2026, 05, 20, 0, 0, 0, DateTimeKind.Utc).AddDays(-index);
            movements.Add(new MovementCertificate
            {
                Id = 310_000 + index,
                OriginLivestockId = isExit ? farm.Id : null,
                DestinationLivestockId = isExit ? null : farm.Id,
                OriginExternalCode = isExit ? null : $"ORI-{index:0000}",
                OriginExternalName = isExit ? null : $"Origen {index:0000}",
                DestinationExternalCode = isExit ? $"DST-{index:0000}" : null,
                DestinationExternalName = isExit ? $"Destino {index:0000}" : null,
                DepartureDate = movementDate,
                ArrivalDate = movementDate.AddDays(1),
                NumberOfAnimals = 2 + (index % 7),
                Specie = LivestockSpecies.Porcine.ToString(),
                CodRemo = $"MOV-{index:0000}",
                Serie = $"SER-{index:0000}",
                Status = index % 5 == 0 ? MovementStatus.Pending : MovementStatus.Confirmed,
                AnimalType = index % 3 == 0 ? "Cebo" : "Lechones"
            });
        }

        dbContext.MovementCertificates.AddRange(movements);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var measurement = await MeasureAsync(async () =>
        {
            _ = await service.GetFarmMovementsAsync(farm.FarmerId, UserRole.Farmer, farm.Id, CancellationToken.None);
        });

        var history = await service.GetFarmMovementsAsync(farm.FarmerId, UserRole.Farmer, farm.Id, CancellationToken.None);

        history.Should().HaveCount(1600);
        history.Should().Contain(entity => entity.Direction == "Entry");
        history.Should().Contain(entity => entity.Direction == "Exit");
        AssertAverageBudget(measurement, 275);
    }

    [Fact]
    public async Task GetMovementAsync_CompletesWithinBudget_ForLargePorcineMovementDetail()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 20, 10, 0, 0, TimeSpan.Zero));
        var service = CreateMovementService(dbContext, clock);
        var farm = await SeedFarmAsync(
            dbContext,
            950,
            LivestockSpecies.Porcine,
            BuildRegaCode(1_500),
            authorisedCapacity: 800,
            porcineMothersCapacity: 300,
            porcineFatteningCapacity: 500);

        var animals = Enumerable.Range(0, 500)
            .Select(index =>
            {
                var animalId = 330_000 + index;
                var animal = ServiceTestData.CreateAnimal(
                    animalId,
                    farm.Id,
                    $"GT{animalId}",
                    new DateOnly(2026, 01, 10),
                    birthDate: new DateOnly(2025, 09, 01).AddDays(index % 120),
                    birthYear: 2025,
                    sex: index % 2 == 0 ? "female" : "male");
                animal.Breed = index % 2 == 0 ? "Ibérico" : "Duroc";
                return animal;
            })
            .ToList();

        var movement = new MovementCertificate
        {
            Id = 340_000,
            OriginLivestockId = farm.Id,
            DepartureDate = new DateTime(2026, 05, 10, 0, 0, 0, DateTimeKind.Utc),
            ArrivalDate = new DateTime(2026, 05, 11, 0, 0, 0, DateTimeKind.Utc),
            NumberOfAnimals = 500,
            Specie = LivestockSpecies.Porcine.ToString(),
            CodRemo = "PORCINE-DETAIL",
            Serie = "POR-DET-1",
            Status = MovementStatus.Confirmed,
            DestinationExternalCode = "ES410010009993",
            DestinationExternalName = "Destino detalle"
        };
        var links = animals.Select((animal, index) => new MovementCertificateAnimal
        {
            Id = 350_000 + index,
            MovementCertificateId = movement.Id,
            AnimalId = animal.Id
        }).ToList();

        dbContext.Animals.AddRange(animals);
        dbContext.MovementCertificates.Add(movement);
        dbContext.MovementCertificateAnimals.AddRange(links);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var measurement = await MeasureAsync(async () =>
        {
            _ = await service.GetMovementAsync(farm.FarmerId, UserRole.Farmer, movement.Id, CancellationToken.None);
        });

        var detail = await service.GetMovementAsync(farm.FarmerId, UserRole.Farmer, movement.Id, CancellationToken.None);

        detail.NumberOfAnimals.Should().Be(500);
        detail.Animals.Should().HaveCount(500);
        detail.LivestockSpecies.Should().Be(LivestockSpecies.Porcine.ToString());
        AssertAverageBudget(measurement, 225);
    }
}
