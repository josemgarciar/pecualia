using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Animals;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class AnimalServiceTests
{
    [Fact]
    public async Task GetAnimalsAsync_FiltersByMovementWithinFarm()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 16, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 10, LivestockSpecies.Ovine, "ES410010001010");
        var includedAnimal = ServiceTestData.CreateAnimal(100, farm.Id, "ES060000583100", new DateOnly(2026, 01, 10), birthYear: 2025, sex: "female");
        var excludedAnimal = ServiceTestData.CreateAnimal(101, farm.Id, "ES060000583101", new DateOnly(2026, 01, 11), birthYear: 2025, sex: "male");
        var movement = CreateMovementCertificate(200, originLivestockId: null, destinationLivestockId: farm.Id, serie: "G-ENTRY-1", departureDate: new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc));

        dbContext.Animals.AddRange(includedAnimal, excludedAnimal);
        dbContext.MovementCertificates.Add(movement);
        dbContext.MovementCertificateAnimals.Add(new MovementCertificateAnimal
        {
            Id = 300,
            MovementCertificateId = movement.Id,
            AnimalId = includedAnimal.Id
        });
        await dbContext.SaveChangesAsync();

        var animals = await service.GetAnimalsAsync(farm.FarmerId, UserRole.Farmer, farm.Id, movement.Id, null, null, null, null, CancellationToken.None);

        animals.Should().ContainSingle();
        animals[0].Id.Should().Be(includedAnimal.Id);
        animals[0].Identification.Should().Be("ES060000583100");
    }

    [Fact]
    public async Task GetFarmAnimalsPageAsync_ReturnsCountsAndGuideSeries()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 16, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 11, LivestockSpecies.Ovine, "ES410010001011");
        var animalWithGuides = ServiceTestData.CreateAnimal(110, farm.Id, "ES060000583110", new DateOnly(2026, 01, 10), birthYear: 2025, sex: "female");
        var dischargedAnimal = ServiceTestData.CreateAnimal(111, farm.Id, "ES060000583111", new DateOnly(2026, 01, 11), birthYear: 2025, sex: "male");
        dischargedAnimal.DischargeDate = new DateOnly(2026, 04, 01);
        dischargedAnimal.DischargeCause = AnimalDischargeCause.Salida;
        dischargedAnimal.DestinationCode = "ES410010009999";

        dbContext.Animals.AddRange(animalWithGuides, dischargedAnimal);
        dbContext.MovementCertificates.AddRange(
            CreateMovementCertificate(210, null, farm.Id, "G-ENTRADA", new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc)),
            CreateMovementCertificate(211, farm.Id, null, "G-SALIDA", new DateTime(2026, 03, 01, 0, 0, 0, DateTimeKind.Utc)));
        dbContext.MovementCertificateAnimals.AddRange(
            new MovementCertificateAnimal { Id = 310, MovementCertificateId = 210, AnimalId = animalWithGuides.Id },
            new MovementCertificateAnimal { Id = 311, MovementCertificateId = 211, AnimalId = animalWithGuides.Id });
        await dbContext.SaveChangesAsync();

        var page = await service.GetFarmAnimalsPageAsync(farm.FarmerId, UserRole.Farmer, farm.Id, null, null, null, null, null, 1, 10, CancellationToken.None);

        page.TotalCount.Should().Be(2);
        page.ActiveCount.Should().Be(1);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(10);
        page.Items.Should().HaveCount(2);
        page.Items[0].Identification.Should().Be("ES060000583110");
        page.Items[0].EntryGuideSerie.Should().Be("G-ENTRADA");
        page.Items[0].ExitGuideSerie.Should().Be("G-SALIDA");
        page.Items[1].Status.Should().Be("Discharged");
    }

    [Fact]
    public async Task GetAnimalAsync_ReturnsDetailWithEntryAndExitGuideSeries()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 16, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 12, LivestockSpecies.Porcine, "ES410010001012", authorisedCapacity: 20, porcineMothersCapacity: 10, porcineFatteningCapacity: 10);
        var animal = ServiceTestData.CreateAnimal(120, farm.Id, "GT1800001004", new DateOnly(2026, 01, 10), birthYear: 2025, sex: "female");

        dbContext.Animals.Add(animal);
        dbContext.PorcinoAnimals.Add(new PorcinoAnimal
        {
            AnimalId = animal.Id,
            AnimalType = "Lechones",
            IdentificationDate = new DateOnly(2026, 01, 10),
            PigRegistrationNumber = "PR-1",
            Tag = "TAG-1"
        });
        dbContext.MovementCertificates.AddRange(
            CreateMovementCertificate(220, null, farm.Id, "P-ENTRADA", new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc)),
            CreateMovementCertificate(221, farm.Id, null, "P-SALIDA", new DateTime(2026, 03, 01, 0, 0, 0, DateTimeKind.Utc)));
        dbContext.MovementCertificateAnimals.AddRange(
            new MovementCertificateAnimal { Id = 320, MovementCertificateId = 220, AnimalId = animal.Id },
            new MovementCertificateAnimal { Id = 321, MovementCertificateId = 221, AnimalId = animal.Id });
        await dbContext.SaveChangesAsync();

        var detail = await service.GetAnimalAsync(farm.FarmerId, UserRole.Farmer, animal.Id, CancellationToken.None);

        detail.Id.Should().Be(animal.Id);
        detail.FarmId.Should().Be(farm.Id);
        detail.EntryGuideSerie.Should().Be("P-ENTRADA");
        detail.ExitGuideSerie.Should().Be("P-SALIDA");
        detail.Porcino.Should().NotBeNull();
        detail.Porcino!.AnimalType.Should().Be("Lechones");
    }

    [Fact]
    public async Task CreateAnimalAsync_CreatesPorcineAnimalAndNormalizesFields()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 16, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 13, LivestockSpecies.Porcine, "ES410010001013", authorisedCapacity: 20, porcineMothersCapacity: 10, porcineFatteningCapacity: 10);

        var created = await service.CreateAnimalAsync(farm.FarmerId, UserRole.Farmer, new CreateAnimalRequest(
            farm.Id,
            " GT1800001005 ",
            2025,
            " Ibérico ",
            " female ",
            new DateOnly(2026, 05, 10),
            AnimalRegistrationCause.Entrada,
            " es410010001999 ",
            null,
            new PorcinoAnimalRequest(" Lechones ", new DateOnly(2026, 05, 10), " PR-55 ", " TAG-55 ")), CancellationToken.None);

        created.Identification.Should().Be("GT1800001005");
        created.Breed.Should().Be("Ibérico");
        created.Sex.Should().Be("female");
        created.OriginCode.Should().Be("ES410010001999");
        created.Porcino.Should().NotBeNull();
        created.Porcino!.AnimalType.Should().Be("Lechones");
        created.Porcino.Tag.Should().Be("TAG-55");

        var persisted = await dbContext.Animals.Include(entity => entity.Porcino).SingleAsync(entity => entity.Id == created.Id);
        persisted.Porcino.Should().NotBeNull();
        persisted.Porcino!.PigRegistrationNumber.Should().Be("PR-55");
    }

    [Fact]
    public async Task CreateAutorrepositionAnimalsAsync_CreatesAnimalsAndBalance()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 16, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 14, LivestockSpecies.Ovine, "ES410010001014");
        var birth = ServiceTestData.CreateBirth(400, farm.Id, new DateOnly(2025, 12, 1), 3);

        dbContext.AnimalBirths.Add(birth);
        await dbContext.SaveChangesAsync();

        var response = await service.CreateAutorrepositionAnimalsAsync(farm.FarmerId, UserRole.Farmer, farm.Id, new CreateAnimalsAutorrepositionRequest(
            "ES060000583120",
            2,
            "Merina",
            "female",
            new DateOnly(2026, 05, 16),
            new OvinoCaprinoAnimalRequest(LivestockSpecies.Ovine, " ARR/ARR ", " ARR ", " ARQ "),
            null), CancellationToken.None);

        response.CreatedAnimals.Should().Be(2);
        response.FirstIdentification.Should().Be("ES060000583120");
        response.LastIdentification.Should().Be("ES060000583121");

        var animals = await dbContext.Animals.Where(entity => entity.LivestockFarmId == farm.Id).OrderBy(entity => entity.Identification).ToListAsync();
        animals.Should().HaveCount(2);
        animals.All(entity => entity.SourceBirthId == birth.Id).Should().BeTrue();

        var balance = await dbContext.Balances.SingleAsync(entity => entity.LivestockFarmId == farm.Id);
        balance.ModificationCause.Should().Be("Autorreposicion");
        balance.NumberOfAnimals.Should().Be(2);
    }

    [Fact]
    public async Task UpdateAnimalAsync_UpdatesOvineAnimalAndSpecificData()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 16, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 15, LivestockSpecies.Ovine, "ES410010001015");
        var animal = ServiceTestData.CreateAnimal(150, farm.Id, "ES060000583130", new DateOnly(2026, 01, 10), birthYear: 2025, sex: "male");

        dbContext.Animals.Add(animal);
        dbContext.OvinoCaprinoAnimals.Add(new OvinoCaprinoAnimal
        {
            AnimalId = animal.Id,
            SpeciesType = LivestockSpecies.Ovine,
            Genotyping = "OLD",
            DominantAllele = "A",
            LowAllele = "B"
        });
        await dbContext.SaveChangesAsync();

        var updated = await service.UpdateAnimalAsync(farm.FarmerId, UserRole.Farmer, animal.Id, new UpdateAnimalRequest(
            "ES060000583131",
            2024,
            "Merina",
            "female",
            new DateOnly(2026, 02, 01),
            AnimalRegistrationCause.Entrada,
            "es410010001998",
            new OvinoCaprinoAnimalRequest(LivestockSpecies.Ovine, " ARR/ARR ", " ARR ", " ARQ "),
            null), CancellationToken.None);

        updated.Identification.Should().Be("ES060000583131");
        updated.BirthYear.Should().Be(2024);
        updated.Sex.Should().Be("female");
        updated.OriginCode.Should().Be("ES410010001998");
        updated.OvinoCaprino.Should().NotBeNull();
        updated.OvinoCaprino!.Genotyping.Should().Be("ARR/ARR");

        var persisted = await dbContext.OvinoCaprinoAnimals.SingleAsync(entity => entity.AnimalId == animal.Id);
        persisted.DominantAllele.Should().Be("ARR");
        persisted.LowAllele.Should().Be("ARQ");
    }

    [Fact]
    public async Task DischargeAnimalAsync_RegistersPorcineDeathWithNormalizedMerCode()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 16, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 16, LivestockSpecies.Porcine, "ES410010001016", authorisedCapacity: 20, porcineMothersCapacity: 10, porcineFatteningCapacity: 10);
        var animal = ServiceTestData.CreateAnimal(160, farm.Id, "GT1800001006", new DateOnly(2026, 01, 10), birthYear: 2025, sex: "female");

        dbContext.Animals.Add(animal);
        await dbContext.SaveChangesAsync();

        var discharged = await service.DischargeAnimalAsync(farm.FarmerId, UserRole.Farmer, animal.Id, new DischargeAnimalRequest(
            new DateOnly(2026, 05, 16),
            AnimalDischargeCause.Muerte,
            " ar26-1234567 "), CancellationToken.None);

        discharged.DischargeCauseValue.Should().Be(AnimalDischargeCause.Muerte.ToString());
        discharged.DestinationCode.Should().Be("AR26-1234567");
        discharged.Status.Should().Be("Discharged");
    }

    [Fact]
    public async Task DeleteAnimalAsync_Rejects_WhenAnimalIsLinkedToMovements()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 16, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 17, LivestockSpecies.Ovine, "ES410010001017");
        var animal = ServiceTestData.CreateAnimal(170, farm.Id, "ES060000583140", new DateOnly(2026, 01, 10), birthYear: 2025);
        var movement = CreateMovementCertificate(270, farm.Id, null, "G-270", new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc));

        dbContext.Animals.Add(animal);
        dbContext.MovementCertificates.Add(movement);
        dbContext.MovementCertificateAnimals.Add(new MovementCertificateAnimal
        {
            Id = 370,
            MovementCertificateId = movement.Id,
            AnimalId = animal.Id
        });
        await dbContext.SaveChangesAsync();

        var action = () => service.DeleteAnimalAsync(farm.FarmerId, UserRole.Farmer, animal.Id, CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("No se puede eliminar un animal vinculado a movimientos registrados.");
    }

    [Fact]
    public async Task DeleteAnimalAsync_RemovesAnimal_WhenNoMovementIsLinked()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 16, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 18, LivestockSpecies.Ovine, "ES410010001018");
        var animal = ServiceTestData.CreateAnimal(180, farm.Id, "ES060000583150", new DateOnly(2026, 01, 10), birthYear: 2025);

        dbContext.Animals.Add(animal);
        await dbContext.SaveChangesAsync();

        await service.DeleteAnimalAsync(farm.FarmerId, UserRole.Farmer, animal.Id, CancellationToken.None);

        (await dbContext.Animals.AnyAsync(entity => entity.Id == animal.Id)).Should().BeFalse();
    }

    [Fact]
    public void EnsureAutorrepositionSupportedSpecies_Throws_ForPorcine()
    {
        var action = () => AnimalService.EnsureAutorrepositionSupportedSpecies(LivestockSpecies.Porcine);

        action.Should().Throw<DomainException>()
            .WithMessage("La autoreposición no está disponible para explotaciones porcinas.");
    }

    [Theory]
    [InlineData(LivestockSpecies.Ovine)]
    [InlineData(LivestockSpecies.Caprine)]
    public void EnsureAutorrepositionSupportedSpecies_Allows_OvineAndCaprine(LivestockSpecies species)
    {
        var action = () => AnimalService.EnsureAutorrepositionSupportedSpecies(species);

        action.Should().NotThrow();
    }

    private static AnimalService CreateService(Pecualia.Api.Data.PecualiaDbContext dbContext, TestClock clock)
    {
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        return new AnimalService(dbContext, censusProjectionService, clock);
    }

    private static async Task<LivestockFarm> SeedFarmAsync(
        Pecualia.Api.Data.PecualiaDbContext dbContext,
        long userId,
        LivestockSpecies species,
        string regaCode,
        int? authorisedCapacity = null,
        int? porcineMothersCapacity = null,
        int? porcineFatteningCapacity = null)
    {
        var user = ServiceTestData.CreateUser(userId, UserRole.Farmer, "Titular", "Animal", email: $"animal-{userId}@test.local");
        var farmer = ServiceTestData.CreateFarmer(userId, user, nifCif: $"1234567{userId % 10}Z");
        var farm = ServiceTestData.CreateFarm(userId + 5000, farmer.UserId, species, $"Farm {userId}", regaCode, authorisedCapacity, porcineMothersCapacity, porcineFatteningCapacity);

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();

        return farm;
    }

    private static MovementCertificate CreateMovementCertificate(
        long id,
        long? originLivestockId,
        long? destinationLivestockId,
        string serie,
        DateTime departureDate)
    {
        return new MovementCertificate
        {
            Id = id,
            OriginLivestockId = originLivestockId,
            DestinationLivestockId = destinationLivestockId,
            DepartureDate = departureDate,
            ArrivalDate = departureDate,
            NumberOfAnimals = 1,
            Serie = serie,
            Specie = LivestockSpecies.Ovine.ToString(),
            Status = MovementStatus.Confirmed
        };
    }
}
