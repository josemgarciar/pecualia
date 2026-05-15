using Pecualia.Api.Contracts.Movements;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class MovementServiceTests
{
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
