using Pecualia.Api.Contracts.Movements;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;
using static Pecualia.Test.Services.PerformanceTestSupport;

namespace Pecualia.Test.Services;

[Trait("Category", "Performance")]
[Trait("PerformanceModule", "OvineCaprine")]
public sealed class OvineCaprinePerformanceTests
{
    [Fact]
    public async Task GetFarmAnimalsPageAsync_CompletesWithinBudget_ForLargeFarm()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 20, 10, 0, 0, TimeSpan.Zero));
        var service = CreateAnimalService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 100, LivestockSpecies.Ovine, BuildRegaCode(100));

        const int animalCount = 2500;
        const int animalsWithGuides = 80;
        var animals = new List<Animal>(animalCount);
        var ovineAnimals = new List<OvinoCaprinoAnimal>(animalCount);
        var movements = new List<MovementCertificate>(animalsWithGuides * 2);
        var links = new List<MovementCertificateAnimal>(animalsWithGuides * 2);

        for (var index = 0; index < animalCount; index++)
        {
            var animal = ServiceTestData.CreateAnimal(
                10_000 + index,
                farm.Id,
                BuildOfficialIdentification(index + 1),
                new DateOnly(2026, 01, 01).AddDays(index % 120),
                birthYear: 2025,
                sex: index % 2 == 0 ? "female" : "male");

            animals.Add(animal);
            ovineAnimals.Add(ServiceTestData.CreateOvinoCaprinoAnimal(animal.Id, LivestockSpecies.Ovine));

            if (index >= animalsWithGuides)
            {
                continue;
            }

            var entryMovement = new MovementCertificate
            {
                Id = 20_000 + index,
                DestinationLivestockId = farm.Id,
                DepartureDate = new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc).AddDays(index),
                ArrivalDate = new DateTime(2026, 02, 02, 0, 0, 0, DateTimeKind.Utc).AddDays(index),
                NumberOfAnimals = 1,
                Serie = $"IN-{index:000}",
                CodRemo = $"REMO-IN-{index:000}",
                Specie = LivestockSpecies.Ovine.ToString(),
                Status = MovementStatus.Confirmed
            };
            var exitMovement = new MovementCertificate
            {
                Id = 30_000 + index,
                OriginLivestockId = farm.Id,
                DepartureDate = new DateTime(2026, 03, 01, 0, 0, 0, DateTimeKind.Utc).AddDays(index),
                ArrivalDate = new DateTime(2026, 03, 02, 0, 0, 0, DateTimeKind.Utc).AddDays(index),
                NumberOfAnimals = 1,
                Serie = $"OUT-{index:000}",
                CodRemo = $"REMO-OUT-{index:000}",
                Specie = LivestockSpecies.Ovine.ToString(),
                Status = MovementStatus.Confirmed
            };

            movements.Add(entryMovement);
            movements.Add(exitMovement);
            links.Add(new MovementCertificateAnimal
            {
                Id = 40_000 + (index * 2),
                MovementCertificateId = entryMovement.Id,
                AnimalId = animal.Id
            });
            links.Add(new MovementCertificateAnimal
            {
                Id = 40_001 + (index * 2),
                MovementCertificateId = exitMovement.Id,
                AnimalId = animal.Id
            });
        }

        dbContext.Animals.AddRange(animals);
        dbContext.OvinoCaprinoAnimals.AddRange(ovineAnimals);
        dbContext.MovementCertificates.AddRange(movements);
        dbContext.MovementCertificateAnimals.AddRange(links);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var measurement = await MeasureAsync(async () =>
        {
            _ = await service.GetFarmAnimalsPageAsync(
                farm.FarmerId,
                UserRole.Farmer,
                farm.Id,
                null,
                null,
                null,
                null,
                null,
                1,
                50,
                CancellationToken.None);
        });

        var page = await service.GetFarmAnimalsPageAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            null,
            null,
            null,
            null,
            null,
            1,
            50,
            CancellationToken.None);

        page.TotalCount.Should().Be(animalCount);
        page.ActiveCount.Should().Be(animalCount);
        page.Items.Should().HaveCount(50);
        page.Items.Should().Contain(entity => entity.EntryGuideSerie != null && entity.ExitGuideSerie != null);
        AssertAverageBudget(measurement, 250);
    }

    [Fact]
    public async Task BuildSnapshotAsync_CompletesWithinBudget_ForDenseCaprineFarm()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 20, 10, 0, 0, TimeSpan.Zero));
        var service = new FarmCensusProjectionService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 350, LivestockSpecies.Caprine, BuildRegaCode(350));

        var animals = new List<Animal>(1400);
        var births = new List<AnimalBirth>(180);
        var movements = new List<MovementCertificate>(80);

        for (var index = 0; index < 1400; index++)
        {
            var birthDate = new DateOnly(2025, 04, 01).AddDays(index % 320);
            AnimalRegistrationCause? registrationCause = index % 11 == 0 ? AnimalRegistrationCause.Autorreposicion : null;

            animals.Add(ServiceTestData.CreateAnimal(
                180_000 + index,
                farm.Id,
                BuildOfficialIdentification(20_000 + index),
                birthDate.AddDays(35),
                birthDate: birthDate,
                birthYear: birthDate.Year,
                registrationCause: registrationCause,
                sex: index % 2 == 0 ? "female" : "male"));
        }

        for (var index = 0; index < 180; index++)
        {
            births.Add(ServiceTestData.CreateBirth(
                190_000 + index,
                farm.Id,
                new DateOnly(2026, 01, 01).AddDays(index % 90),
                4 + (index % 3)));
        }

        for (var index = 0; index < 80; index++)
        {
            var isEntry = index % 2 == 0;
            movements.Add(new MovementCertificate
            {
                Id = 195_000 + index,
                OriginLivestockId = isEntry ? null : farm.Id,
                DestinationLivestockId = isEntry ? farm.Id : null,
                DepartureDate = new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc).AddDays(index),
                ArrivalDate = new DateTime(2026, 02, 02, 0, 0, 0, DateTimeKind.Utc).AddDays(index),
                NumberOfAnimals = 3 + (index % 4),
                UnidentifiedCategory = index % 3 == 0
                    ? MovementUnidentifiedCategory.Under4Months
                    : MovementUnidentifiedCategory.Between4And12Months,
                Specie = LivestockSpecies.Caprine.ToString(),
                Status = MovementStatus.Confirmed
            });
        }

        dbContext.Animals.AddRange(animals);
        dbContext.AnimalBirths.AddRange(births);
        dbContext.MovementCertificates.AddRange(movements);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var measurement = await MeasureAsync(async () =>
        {
            _ = await service.BuildSnapshotAsync(farm, new DateOnly(2026, 05, 20), CancellationToken.None);
        });

        var snapshot = await service.BuildSnapshotAsync(farm, new DateOnly(2026, 05, 20), CancellationToken.None);

        snapshot.LivestockSpecies.Should().Be(LivestockSpecies.Caprine.ToString());
        snapshot.Total.Should().BeGreaterThan(0);
        snapshot.NonReproductiveUnder4Months.Should().BeGreaterThan(0);
        snapshot.NonReproductiveBetween4And12Months.Should().BeGreaterThan(0);
        AssertAverageBudget(measurement, 250);
    }

    [Fact]
    public async Task PreviewImportAsync_CompletesWithinBudget_ForLargeBulkImport()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 20, 10, 0, 0, TimeSpan.Zero));
        var service = CreateMovementService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 400, LivestockSpecies.Ovine, BuildRegaCode(400));

        var animals = Enumerable.Range(0, 800)
            .Select(index => ServiceTestData.CreateAnimal(
                170_000 + index,
                farm.Id,
                BuildOfficialIdentification(index + 1),
                new DateOnly(2026, 01, 10),
                birthYear: 2025,
                sex: index % 2 == 0 ? "female" : "male"))
            .ToList();
        var ovineAnimals = animals
            .Select(entity => ServiceTestData.CreateOvinoCaprinoAnimal(entity.Id, LivestockSpecies.Ovine))
            .ToList();

        dbContext.Animals.AddRange(animals);
        dbContext.OvinoCaprinoAnimals.AddRange(ovineAnimals);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var lines = new List<string>(1450);
        lines.AddRange(Enumerable.Range(1, 800).Select(BuildOfficialIdentification));
        lines.AddRange(Enumerable.Range(801, 500).Select(BuildOfficialIdentification));
        lines.AddRange(Enumerable.Range(1, 100).Select(BuildOfficialIdentification));
        lines.AddRange(Enumerable.Range(1, 50).Select(index => $"INVALID-{index:000}"));
        var rawText = string.Join(Environment.NewLine, lines);

        var request = new PreviewMovementImportRequest(
            farm.Id,
            MovementImportOperation.Alta,
            "ES410010009991",
            "Origen externo performance",
            "REMO-PERF-1",
            null,
            new DateTime(2026, 05, 20, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 20, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            null,
            null,
            MovementImportCause.Entrada,
            null,
            null,
            null,
            rawText,
            null,
            null,
            null);

        var measurement = await MeasureAsync(async () =>
        {
            _ = await service.PreviewImportAsync(farm.FarmerId, UserRole.Farmer, request, CancellationToken.None);
        });

        var preview = await service.PreviewImportAsync(farm.FarmerId, UserRole.Farmer, request, CancellationToken.None);

        preview.Summary.TotalLines.Should().Be(1450);
        preview.Rows.Should().HaveCount(1450);
        preview.LivestockSpecies.Should().Be(LivestockSpecies.Ovine.ToString());
        AssertAverageBudget(measurement, 225);
    }

    [Fact]
    public async Task PreviewImportAsync_CompletesWithinBudget_ForLargeCaprineBulkExit()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 20, 10, 0, 0, TimeSpan.Zero));
        var service = CreateMovementService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 450, LivestockSpecies.Caprine, BuildRegaCode(450));
        var otherFarm = await SeedFarmAsync(dbContext, 451, LivestockSpecies.Caprine, BuildRegaCode(451));

        var activeAnimals = Enumerable.Range(0, 700)
            .Select(index => ServiceTestData.CreateAnimal(
                210_000 + index,
                farm.Id,
                BuildOfficialIdentification(40_000 + index),
                new DateOnly(2026, 01, 10),
                birthYear: 2025,
                sex: index % 2 == 0 ? "female" : "male"))
            .ToList();
        var dischargedAnimals = Enumerable.Range(0, 150)
            .Select(index =>
            {
                var animal = ServiceTestData.CreateAnimal(
                    220_000 + index,
                    farm.Id,
                    BuildOfficialIdentification(50_000 + index),
                    new DateOnly(2026, 01, 10),
                    birthYear: 2025,
                    sex: index % 2 == 0 ? "female" : "male");
                animal.DischargeDate = new DateOnly(2026, 04, 01);
                animal.DischargeCause = AnimalDischargeCause.Salida;
                return animal;
            })
            .ToList();
        var foreignAnimals = Enumerable.Range(0, 200)
            .Select(index => ServiceTestData.CreateAnimal(
                230_000 + index,
                otherFarm.Id,
                BuildOfficialIdentification(60_000 + index),
                new DateOnly(2026, 01, 10),
                birthYear: 2025,
                sex: index % 2 == 0 ? "female" : "male"))
            .ToList();

        dbContext.Animals.AddRange(activeAnimals);
        dbContext.Animals.AddRange(dischargedAnimals);
        dbContext.Animals.AddRange(foreignAnimals);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var lines = new List<string>(1200);
        lines.AddRange(activeAnimals.Select(entity => entity.Identification));
        lines.AddRange(foreignAnimals.Select(entity => entity.Identification));
        lines.AddRange(dischargedAnimals.Select(entity => entity.Identification));
        lines.AddRange(activeAnimals.Take(100).Select(entity => entity.Identification));
        lines.AddRange(Enumerable.Range(1, 50).Select(index => $"INVALID-CAP-{index:000}"));
        var rawText = string.Join(Environment.NewLine, lines);

        var request = new PreviewMovementImportRequest(
            farm.Id,
            MovementImportOperation.Baja,
            "ES410010009992",
            "Destino externo performance",
            "REMO-PERF-EXIT-1",
            null,
            new DateTime(2026, 05, 20, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 20, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            null,
            null,
            MovementImportCause.Salida,
            null,
            null,
            null,
            rawText,
            null,
            null,
            null);

        var measurement = await MeasureAsync(async () =>
        {
            _ = await service.PreviewImportAsync(farm.FarmerId, UserRole.Farmer, request, CancellationToken.None);
        });

        var preview = await service.PreviewImportAsync(farm.FarmerId, UserRole.Farmer, request, CancellationToken.None);

        preview.Summary.TotalLines.Should().Be(1200);
        preview.Rows.Should().HaveCount(1200);
        preview.LivestockSpecies.Should().Be(LivestockSpecies.Caprine.ToString());
        preview.Rows.Should().Contain(entity => entity.Status == "valid");
        preview.Rows.Should().Contain(entity => entity.Status == "conflict");
        AssertAverageBudget(measurement, 225);
    }
}
