using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Animals;
using Pecualia.Api.Contracts.Movements;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class MovementServiceTests
{
    [Fact]
    public void GetBreedOptions_ReturnsOfficialBreedCodes()
    {
        using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);

        var options = service.GetBreedOptions(LivestockSpecies.Ovine);

        options.Should().Contain(option => option.Name == "Merina" && option.Code == "M");
        options.Should().Contain(option => option.Name == "Assaf");
    }

    [Fact]
    public async Task PreviewImportAsync_RejectsExternalCounterpartyWithInvalidRega()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 100, LivestockSpecies.Ovine, "ES410010000100");

        var action = () => service.PreviewImportAsync(farm.FarmerId, UserRole.Farmer, new PreviewMovementImportRequest(
            farm.Id,
            MovementImportOperation.Alta,
            "INVALID",
            "Origen externo",
            "REMO-1",
            null,
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            null,
            null,
            MovementImportCause.Entrada,
            null,
            null,
            null,
            "ES123456789012",
            null,
            null,
            null), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("El código REGA no es válido. Debe seguir el formato ES seguido de 12 dígitos.");
    }

    [Fact]
    public async Task PreviewImportAsync_RejectsUnidentifiedAnimals_ForPorcine()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 101, LivestockSpecies.Porcine, "ES410010000101", authorisedCapacity: 30, porcineMothersCapacity: 10, porcineFatteningCapacity: 20);

        var action = () => service.PreviewImportAsync(farm.FarmerId, UserRole.Farmer, new PreviewMovementImportRequest(
            farm.Id,
            MovementImportOperation.Alta,
            "ES410010009999",
            "Origen externo",
            "REMO-2",
            null,
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            null,
            null,
            MovementImportCause.Entrada,
            null,
            null,
            null,
            null,
            null,
            5,
            MovementUnidentifiedCategory.Under4Months), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Solo se pueden registrar movimientos sin identificación para ganado ovino o caprino.");
    }

    [Fact]
    public async Task PreviewImportAsync_RejectsInvalidCauseForAlta()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 102, LivestockSpecies.Ovine, "ES410010000102");

        var action = () => service.PreviewImportAsync(farm.FarmerId, UserRole.Farmer, new PreviewMovementImportRequest(
            farm.Id,
            MovementImportOperation.Alta,
            "ES410010009998",
            "Origen externo",
            "REMO-3",
            null,
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            null,
            null,
            MovementImportCause.Muerte,
            null,
            null,
            null,
            "ES123456789012",
            null,
            null,
            null), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("La causa de alta debe ser Entrada (E) o Autorreposición (A).");
    }

    [Fact]
    public async Task CommitImportAsync_CreatesExternalOvineEntry_AndRegistersAnimals()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 1021, LivestockSpecies.Ovine, "ES410010000121");

        var result = await service.CommitImportAsync(farm.FarmerId, UserRole.Farmer, new CommitMovementImportRequest(
            farm.Id,
            MovementImportOperation.Alta,
            "ES410010009121",
            "Origen externo",
            "REMO-OV-1",
            "SER-OV-1",
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            null,
            "Camión",
            "Transportes Test",
            "1234BCD",
            MovementImportCause.Entrada,
            null,
            null,
            null,
            "ES123456789012\nES123456789013",
            new SharedAnimalDataRequest(
                new DateOnly(2025, 12, 1),
                2025,
                "Merina",
                "H",
                AnimalRegistrationCause.Entrada,
                new OvinoCaprinoAnimalRequest(LivestockSpecies.Ovine, "ARR/ARQ", "ARR", "ARQ"),
                null),
            null,
            null), CancellationToken.None);

        result.ProcessedRows.Should().Be(2);
        result.RejectedRows.Should().Be(0);
        result.SharedAnimalDataUsed.Should().BeTrue();

        var animals = dbContext.Animals.Where(entity => entity.LivestockFarmId == farm.Id).OrderBy(entity => entity.Identification).ToList();
        animals.Should().HaveCount(2);
        animals.All(entity => entity.OriginCode == "ES410010009121").Should().BeTrue();
        animals.All(entity => entity.RegistrationCause == AnimalRegistrationCause.Entrada).Should().BeTrue();
        dbContext.OvinoCaprinoAnimals.Should().HaveCount(2);
        dbContext.MovementCertificates.Should().ContainSingle(entity => entity.CodRemo == "REMO-OV-1");
        dbContext.Census.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateManualMovementAsync_MovesAnimalBetweenInternalFarms()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var originFarm = await SeedFarmAsync(dbContext, 1032, LivestockSpecies.Ovine, "ES410010000132");
        var destinationFarm = ServiceTestData.CreateFarm(9032, originFarm.FarmerId, LivestockSpecies.Ovine, "Destino interno", "ES410010000133");
        var animal = ServiceTestData.CreateAnimal(7001, originFarm.Id, "ES123456789120", new DateOnly(2026, 02, 1), birthYear: 2025, registrationCause: AnimalRegistrationCause.Entrada, sex: "H");

        dbContext.Farms.Add(destinationFarm);
        dbContext.Animals.Add(animal);
        await dbContext.SaveChangesAsync();

        var movement = await service.CreateManualMovementAsync(originFarm.FarmerId, UserRole.Farmer, new CreateManualMovementRequest(
            destinationFarm.Id,
            MovementDirection.Entry,
            MovementCounterpartyType.Internal,
            originFarm.Id,
            null,
            null,
            "REMO-INT-1",
            "SER-INT-1",
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            "Transportes Sierra",
            "4321BCD",
            AnimalRegistrationCause.Entrada.ToString(),
            null,
            null,
            null,
            [animal.Id],
            [],
            null), CancellationToken.None);

        movement.OriginFarmId.Should().Be(originFarm.Id);
        movement.DestinationFarmId.Should().Be(destinationFarm.Id);
        movement.NumberOfAnimals.Should().Be(1);

        var updatedAnimal = await dbContext.Animals.SingleAsync(entity => entity.Id == animal.Id);
        updatedAnimal.LivestockFarmId.Should().Be(destinationFarm.Id);
        updatedAnimal.OriginCode.Should().Be(originFarm.RegaCode);
        dbContext.Balances.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConfirmMovementAsync_UpdatesStatus_AndMovementCanBeRetrieved()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 1033, LivestockSpecies.Ovine, "ES410010000134");
        var movement = new MovementCertificate
        {
            Id = 8001,
            OriginLivestockId = farm.Id,
            CodRemo = "REMO-GET-1",
            Serie = "SER-GET-1",
            Specie = LivestockSpecies.Ovine.ToString(),
            NumberOfAnimals = 2,
            DepartureDate = new DateTime(2026, 05, 10, 0, 0, 0, DateTimeKind.Utc),
            ArrivalDate = new DateTime(2026, 05, 11, 0, 0, 0, DateTimeKind.Utc),
            Status = MovementStatus.Pending,
            OriginFarm = farm
        };

        dbContext.MovementCertificates.Add(movement);
        await dbContext.SaveChangesAsync();

        var confirmation = await service.ConfirmMovementAsync(farm.FarmerId, UserRole.Farmer, movement.Id, CancellationToken.None);
        var detail = await service.GetMovementAsync(farm.FarmerId, UserRole.Farmer, movement.Id, CancellationToken.None);
        var farmMovements = await service.GetFarmMovementsAsync(farm.FarmerId, UserRole.Farmer, farm.Id, CancellationToken.None);

        confirmation.Status.Should().Be(MovementStatus.Confirmed.ToString());
        detail.Status.Should().Be(MovementStatus.Confirmed.ToString());
        farmMovements.Should().ContainSingle(item => item.Id == movement.Id && item.Status == MovementStatus.Confirmed.ToString());
    }

    [Fact]
    public async Task CommitImportAsync_CreatesAggregatePorcineExit()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 1034, LivestockSpecies.Porcine, "ES410010000135", authorisedCapacity: 50, porcineMothersCapacity: 20, porcineFatteningCapacity: 30);
        var firstAnimal = ServiceTestData.CreateAnimal(7101, farm.Id, "GT1800002101", new DateOnly(2026, 02, 1), birthDate: new DateOnly(2026, 01, 1), birthYear: 2026);
        var secondAnimal = ServiceTestData.CreateAnimal(7102, farm.Id, "GT1800002102", new DateOnly(2026, 02, 1), birthDate: new DateOnly(2026, 01, 1), birthYear: 2026);

        dbContext.Animals.AddRange(firstAnimal, secondAnimal);
        dbContext.PorcinoAnimals.AddRange(
            ServiceTestData.CreatePorcinoAnimal(firstAnimal.Id, "Cebo"),
            ServiceTestData.CreatePorcinoAnimal(secondAnimal.Id, "Cebo"));
        await dbContext.SaveChangesAsync();

        var result = await service.CommitImportAsync(farm.FarmerId, UserRole.Farmer, new CommitMovementImportRequest(
            farm.Id,
            MovementImportOperation.Baja,
            "ES410010009135",
            "Destino externo",
            "REMO-POR-1",
            "SER-POR-1",
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            null,
            null,
            MovementImportCause.Salida,
            2,
            "Ibérico",
            "Cebo",
            null,
            null,
            null,
            null), CancellationToken.None);

        result.ProcessedRows.Should().Be(2);
        result.RejectedRows.Should().Be(0);
        dbContext.MovementCertificates.Should().ContainSingle(entity => entity.CodRemo == "REMO-POR-1");
        dbContext.BalancePorcino.Should().Contain(entity => entity.Baits == 2);
        dbContext.Census.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PreviewImportAsync_ReturnsMixedStatuses_ForOvineBulkExit()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 1035, LivestockSpecies.Ovine, "ES410010000136");
        var activeAnimal = ServiceTestData.CreateAnimal(7201, farm.Id, "ES123456789401", new DateOnly(2026, 01, 1));
        var dischargedAnimal = ServiceTestData.CreateAnimal(7202, farm.Id, "ES123456789402", new DateOnly(2026, 01, 1));
        dischargedAnimal.DischargeDate = new DateOnly(2026, 03, 1);
        dischargedAnimal.DischargeCause = AnimalDischargeCause.Salida;

        dbContext.Animals.AddRange(activeAnimal, dischargedAnimal);
        await dbContext.SaveChangesAsync();

        var preview = await service.PreviewImportAsync(farm.FarmerId, UserRole.Farmer, new PreviewMovementImportRequest(
            farm.Id,
            MovementImportOperation.Baja,
            "ES410010009136",
            "Destino externo",
            "REMO-PREV-1",
            null,
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            null,
            null,
            MovementImportCause.Salida,
            null,
            null,
            null,
            "ES123456789401\nES123456789402\nES123456789401\nINVALID\nES123456789499",
            null,
            null,
            null), CancellationToken.None);

        preview.Summary.TotalLines.Should().Be(5);
        preview.Summary.ValidRows.Should().Be(1);
        preview.Summary.ConflictRows.Should().Be(2);
        preview.Summary.DuplicateRows.Should().Be(1);
        preview.Summary.InvalidFormatRows.Should().Be(1);
    }

    [Fact]
    public async Task CommitImportAsync_RegistersUnidentifiedCaprineMovement()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 1036, LivestockSpecies.Caprine, "ES410010000137");

        var result = await service.CommitImportAsync(farm.FarmerId, UserRole.Farmer, new CommitMovementImportRequest(
            farm.Id,
            MovementImportOperation.Alta,
            "ES410010009137",
            "Origen sin identificar",
            "REMO-UNID-1",
            null,
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            "Transporte Caprino",
            "1111AAA",
            MovementImportCause.Entrada,
            null,
            null,
            null,
            null,
            null,
            5,
            MovementUnidentifiedCategory.Under4Months), CancellationToken.None);

        result.ProcessedRows.Should().Be(5);
        dbContext.MovementCertificates.Should().ContainSingle(entity => entity.CodRemo == "REMO-UNID-1" && entity.UnidentifiedCategory == MovementUnidentifiedCategory.Under4Months);
        dbContext.BalanceOvinoCaprino.Should().Contain(entity => entity.NonReproductiveUnder4Months == 5);
        dbContext.Census.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateManualMovementAsync_CreatesExternalOvineEntry_FromIdentificationLines()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 1037, LivestockSpecies.Ovine, "ES410010000138");

        var movement = await service.CreateManualMovementAsync(farm.FarmerId, UserRole.Farmer, new CreateManualMovementRequest(
            farm.Id,
            MovementDirection.Entry,
            MovementCounterpartyType.External,
            null,
            "ES410010009138",
            "Origen lector",
            "REMO-MAN-1",
            "SER-MAN-1",
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            null,
            "Camión",
            "Transporte Dos",
            "2222BBB",
            AnimalRegistrationCause.Entrada.ToString(),
            null,
            null,
            null,
            [],
            ["ES123456789501", "ES123456789502"],
            new SharedAnimalDataRequest(
                new DateOnly(2025, 10, 1),
                2025,
                "Merina",
                "H",
                AnimalRegistrationCause.Entrada,
                new OvinoCaprinoAnimalRequest(LivestockSpecies.Ovine, "ARR/ARQ", "ARR", "ARQ"),
                null)), CancellationToken.None);

        movement.NumberOfAnimals.Should().Be(2);
        dbContext.Animals.Count(entity => entity.LivestockFarmId == farm.Id).Should().Be(2);
        dbContext.OvinoCaprinoAnimals.Should().HaveCount(2);
    }

    [Fact]
    public async Task PreviewImportAsync_AcceptsAggregatePorcineMovement_WithOfficialBreedAndAnimalType()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 103, LivestockSpecies.Porcine, "ES410010000103", authorisedCapacity: 50, porcineMothersCapacity: 20, porcineFatteningCapacity: 30);

        var preview = await service.PreviewImportAsync(farm.FarmerId, UserRole.Farmer, new PreviewMovementImportRequest(
            farm.Id,
            MovementImportOperation.Alta,
            "ES410010009997",
            "Origen externo",
            "REMO-4",
            null,
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            null,
            null,
            MovementImportCause.Entrada,
            7,
            "Ibérico",
            "Hembras reposición",
            null,
            null,
            null,
            null), CancellationToken.None);

        preview.LivestockSpecies.Should().Be(LivestockSpecies.Porcine.ToString());
        preview.RequiresSharedAnimalData.Should().BeFalse();
        preview.Rows.Should().BeEmpty();
        preview.Summary.ValidRows.Should().Be(7);
    }

    [Fact]
    public async Task PreviewImportAsync_NormalizesReaderExportLine_ForOvineImport()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 1031, LivestockSpecies.Ovine, "ES410010000131");

        var preview = await service.PreviewImportAsync(farm.FarmerId, UserRole.Farmer, new PreviewMovementImportRequest(
            farm.Id,
            MovementImportOperation.Alta,
            "ES410010009991",
            "Origen externo",
            "REMO-LECTOR-1",
            null,
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            null,
            null,
            MovementImportCause.Entrada,
            null,
            null,
            null,
            "[A0040000724100007879164|109BA0275|2|016||H|11111999|23022026|02]",
            null,
            null,
            null), CancellationToken.None);

        preview.Rows.Should().ContainSingle();
        preview.Rows[0].Identification.Should().Be("ES100007879164");
        preview.Rows[0].Status.Should().Be("not_found");
        preview.Summary.InvalidFormatRows.Should().Be(0);
        preview.Summary.NotFoundRows.Should().Be(1);
    }

    [Fact]
    public async Task CreateManualMovementAsync_RejectsExternalCounterpartyWithoutName()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 104, LivestockSpecies.Ovine, "ES410010000104");

        var action = () => service.CreateManualMovementAsync(farm.FarmerId, UserRole.Farmer, new CreateManualMovementRequest(
            farm.Id,
            MovementDirection.Entry,
            MovementCounterpartyType.External,
            null,
            "ES410010009996",
            null,
            "REMO-5",
            null,
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            null,
            null,
            AnimalRegistrationCause.Entrada.ToString(),
            null,
            null,
            null,
            [],
            [],
            null), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Debes indicar el nombre de la contraparte externa.");
    }

    [Fact]
    public async Task CreateManualMovementAsync_RejectsInvalidCauseForEntry()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedFarmAsync(dbContext, 105, LivestockSpecies.Ovine, "ES410010000105");

        var action = () => service.CreateManualMovementAsync(farm.FarmerId, UserRole.Farmer, new CreateManualMovementRequest(
            farm.Id,
            MovementDirection.Entry,
            MovementCounterpartyType.External,
            null,
            "ES410010009995",
            "Origen externo",
            "REMO-6",
            null,
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 15, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            null,
            null,
            AnimalDischargeCause.Salida.ToString(),
            null,
            null,
            null,
            [],
            [],
            null), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("La causa de alta debe ser Entrada (E) o Autorreposición (A).");
    }

    private static MovementService CreateService(Pecualia.Api.Data.PecualiaDbContext dbContext, TestClock clock)
    {
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        return new MovementService(dbContext, censusProjectionService, clock);
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
        var user = ServiceTestData.CreateUser(userId, UserRole.Farmer, "Titular", "Test", email: $"movement-{userId}@test.local");
        var farmer = ServiceTestData.CreateFarmer(userId, user, nifCif: $"1234567{userId % 10}Z");
        var farm = ServiceTestData.CreateFarm(userId + 5000, farmer.UserId, species, $"Farm {userId}", regaCode, authorisedCapacity, porcineMothersCapacity, porcineFatteningCapacity);

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();

        return farm;
    }
}
