using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class FarmOperationServiceTests
{
    [Fact]
    public async Task CreateBirthAsync_CreatesOvineBirth_AndGetBirthsReturnsIt()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedOvineFarmAsync(dbContext, 90);

        var created = await service.CreateBirthAsync(90, UserRole.Farmer, farm.Id, new CreateFarmBirthRequest(
            new DateOnly(2026, 05, 10),
            4,
            3.5m,
            "Lote de primavera"), CancellationToken.None);
        var births = await service.GetBirthsAsync(90, UserRole.Farmer, farm.Id, CancellationToken.None);

        created.OffspringNumber.Should().Be(4);
        births.Should().ContainSingle(entry => entry.Id == created.Id && entry.Observations == "Lote de primavera");
        dbContext.BalanceOvinoCaprino.Should().ContainSingle(detail => detail.NonReproductiveUnder4Months == 4);
    }

    [Fact]
    public async Task CreateBirthAsync_RejectsPorcineBirthOlderThanThreeMonths()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 100);

        var action = () => service.CreateBirthAsync(100, UserRole.Farmer, farm.Id, new CreateFarmBirthRequest(
            new DateOnly(2026, 02, 14),
            5,
            null,
            null), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("En porcino no puedes registrar nacimientos con más de 3 meses de antigüedad.");
    }

    [Fact]
    public async Task UpdateBirthAsync_RejectsWhenPorcineBirthLeavesAllowedWindow()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 101);
        var balance = new Pecualia.Api.Models.Entities.Balance
        {
            Id = 6001,
            LivestockFarmId = farm.Id,
            BalanceDate = new DateOnly(2026, 04, 01),
            ModificationCause = "Nacimiento",
            NumberOfAnimals = 4
        };
        var birth = ServiceTestData.CreateBirth(5001, farm.Id, new DateOnly(2026, 04, 01), 4, balance.Id);

        dbContext.Balances.Add(balance);
        dbContext.AnimalBirths.Add(birth);
        dbContext.BalancePorcino.Add(new Pecualia.Api.Models.Entities.BalancePorcino
        {
            BalanceId = balance.Id,
            Piglets = 4
        });
        await dbContext.SaveChangesAsync();

        var action = () => service.UpdateBirthAsync(101, UserRole.Farmer, farm.Id, birth.Id, new UpdateFarmBirthRequest(
            new DateOnly(2026, 02, 14),
            4,
            null,
            null), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("En porcino no puedes registrar nacimientos con más de 3 meses de antigüedad.");
    }

    [Fact]
    public async Task GetPendingPorcineTransitionsAsync_ReturnsBirthWhenThreeMonthsAreReached()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 102);
        var birth = ServiceTestData.CreateBirth(5002, farm.Id, new DateOnly(2026, 02, 15), 6);

        dbContext.AnimalBirths.Add(birth);
        await dbContext.SaveChangesAsync();

        var pending = await service.GetPendingPorcineTransitionsAsync(102, UserRole.Farmer, farm.Id, CancellationToken.None);

        pending.Should().ContainSingle();
        pending[0].BirthId.Should().Be(birth.Id);
        pending[0].PendingQuantity.Should().Be(6);
        pending[0].DueDate.Should().Be(new DateOnly(2026, 05, 15));
        pending[0].FinalTransitionDate.Should().Be(new DateOnly(2026, 08, 15));
        pending[0].IsOverdue.Should().BeFalse();
    }

    [Fact]
    public async Task GetPendingPorcineTransitionsAsync_MarksTaskAsOverdue_AfterSixMonths()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 09, 16, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 105);
        var birth = ServiceTestData.CreateBirth(5005, farm.Id, new DateOnly(2026, 02, 15), 6);

        dbContext.AnimalBirths.Add(birth);
        await dbContext.SaveChangesAsync();

        var pending = await service.GetPendingPorcineTransitionsAsync(105, UserRole.Farmer, farm.Id, CancellationToken.None);

        pending.Should().ContainSingle();
        pending[0].IsOverdue.Should().BeTrue();
    }

    [Fact]
    public async Task GetAutorrepositionAvailabilityAsync_ReturnsEligibleOvineAnimals()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedOvineFarmAsync(dbContext, 1060);
        var eligible = ServiceTestData.CreateAnimal(8601, farm.Id, "ES123456789601", new DateOnly(2026, 01, 10), birthDate: new DateOnly(2025, 01, 10), birthYear: 2025, registrationCause: AnimalRegistrationCause.Autorreposicion, sex: "H");
        var young = ServiceTestData.CreateAnimal(8602, farm.Id, "ES123456789602", new DateOnly(2026, 04, 10), birthDate: new DateOnly(2026, 03, 10), birthYear: 2026, registrationCause: AnimalRegistrationCause.Entrada, sex: "H");

        dbContext.Animals.AddRange(eligible, young);
        await dbContext.SaveChangesAsync();

        var availability = await service.GetAutorrepositionAvailabilityAsync(1060, UserRole.Farmer, farm.Id, CancellationToken.None);

        availability.AvailableAnimals.Should().BeGreaterThan(0);
        availability.EligibleAnimals.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task ResolvePorcineTransitionAsync_RejectsWhenBreakdownDoesNotMatchPendingAnimals()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 103);
        var birth = ServiceTestData.CreateBirth(5003, farm.Id, new DateOnly(2026, 02, 15), 6);

        dbContext.AnimalBirths.Add(birth);
        await dbContext.SaveChangesAsync();

        var action = () => service.ResolvePorcineTransitionAsync(103, UserRole.Farmer, farm.Id, birth.Id, new ResolvePorcineTransitionRequest(
            2,
            2,
            1), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("La suma del reparto debe coincidir exactamente con los animales pendientes de reclasificación.");
    }

    [Fact]
    public async Task ResolvePorcineTransitionAsync_CreatesDecisionAndPorcineBalance()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 104);
        var birth = ServiceTestData.CreateBirth(5004, farm.Id, new DateOnly(2026, 02, 15), 6);

        dbContext.AnimalBirths.Add(birth);
        await dbContext.SaveChangesAsync();

        var pendingAfterResolve = await service.ResolvePorcineTransitionAsync(104, UserRole.Farmer, farm.Id, birth.Id, new ResolvePorcineTransitionRequest(
            2,
            3,
            1), CancellationToken.None);

        pendingAfterResolve.Should().BeEmpty();

        var decision = await dbContext.PorcineBirthTransitionDecisions.SingleAsync(entity => entity.BirthId == birth.Id);
        decision.ToRears.Should().Be(2);
        decision.ToSowsReposition.Should().Be(3);
        decision.ToMalesReposition.Should().Be(1);
        decision.EffectiveDate.Should().Be(new DateOnly(2026, 05, 15));

        var balance = await dbContext.Balances.SingleAsync(entity => entity.Id == decision.BalanceId);
        balance.ModificationCause.Should().Be(AnimalRegistrationCause.Autorreposicion.ToString());
        balance.NumberOfAnimals.Should().Be(6);

        var balanceDetail = await dbContext.BalancePorcino.SingleAsync(entity => entity.BalanceId == balance.Id);
        balanceDetail.Piglets.Should().Be(6);
        balanceDetail.Rear.Should().Be(2);
        balanceDetail.SowsReposition.Should().Be(3);
        balanceDetail.PigsReposition.Should().Be(1);
        balanceDetail.Type.Should().Be("Reclasificación porcina");
    }

    [Fact]
    public async Task CreateDeathAsync_AllowsAggregatePorcineDeath_WithoutIdentification_AndUpdatesCensus()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 106);
        var firstAnimal = ServiceTestData.CreateAnimal(5106, farm.Id, "GT1800001006", new DateOnly(2026, 03, 10), birthDate: new DateOnly(2026, 02, 10));
        var secondAnimal = ServiceTestData.CreateAnimal(5107, farm.Id, "GT1800001007", new DateOnly(2026, 03, 10), birthDate: new DateOnly(2026, 02, 10));
        var firstPorcineAnimal = ServiceTestData.CreatePorcinoAnimal(firstAnimal.Id, "Cebo");
        var secondPorcineAnimal = ServiceTestData.CreatePorcinoAnimal(secondAnimal.Id, "Cebo");

        dbContext.Animals.AddRange(firstAnimal, secondAnimal);
        dbContext.PorcinoAnimals.AddRange(firstPorcineAnimal, secondPorcineAnimal);
        await dbContext.SaveChangesAsync();

        var death = await service.CreateDeathAsync(106, UserRole.Farmer, farm.Id, new CreateFarmDeathRequest(
            null,
            "Cebo",
            2,
            new DateOnly(2026, 05, 15),
            "AR26-1234567"), CancellationToken.None);

        death.Identification.Should().BeNull();
        death.AnimalType.Should().Be("Cebo");
        death.NumberOfAnimals.Should().Be(2);

        var census = await service.GetCensusAsync(106, UserRole.Farmer, farm.Id, 2026, CancellationToken.None);
        census.Baits.Should().Be(0);

        var balance = await dbContext.Balances.SingleAsync(entity => entity.OriginLivestockCode == BalanceMarkers.PorcineAggregateDeath);
        balance.NumberOfAnimals.Should().Be(2);
        balance.ModificationCause.Should().Be(AnimalDischargeCause.Muerte.ToString());
    }

    [Fact]
    public async Task CreateDeathAsync_RequiresAnimalType_ForPorcineDeaths()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 107);

        var action = () => service.CreateDeathAsync(107, UserRole.Farmer, farm.Id, new CreateFarmDeathRequest(
            null,
            null,
            1,
            new DateOnly(2026, 05, 15),
            "AR26-1234567"), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Debes indicar el tipo de animal para registrar la baja en porcino.");
    }

    [Fact]
    public async Task CreateDeathAsync_RejectsQuantityGreaterThanOne_WhenIdentificationIsProvided()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedPorcineFarmAsync(dbContext, 108);
        var animal = ServiceTestData.CreateAnimal(5108, farm.Id, "GT1800001008", new DateOnly(2026, 03, 10), birthDate: new DateOnly(2026, 02, 10));
        var porcineAnimal = ServiceTestData.CreatePorcinoAnimal(animal.Id, "Cebo");

        dbContext.Animals.Add(animal);
        dbContext.PorcinoAnimals.Add(porcineAnimal);
        await dbContext.SaveChangesAsync();

        var action = () => service.CreateDeathAsync(108, UserRole.Farmer, farm.Id, new CreateFarmDeathRequest(
            "GT1800001008",
            "Cebo",
            2,
            new DateOnly(2026, 05, 15),
            "AR26-1234567"), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Si indicas un crotal individual, el número de animales debe ser 1.");
    }

    [Fact]
    public async Task DeleteBirthAsync_RemovesBirthAndBalance_WhenBirthHasNoConsumedAnimals()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedOvineFarmAsync(dbContext, 109);
        var created = await service.CreateBirthAsync(109, UserRole.Farmer, farm.Id, new CreateFarmBirthRequest(
            new DateOnly(2026, 05, 1),
            3,
            null,
            null), CancellationToken.None);

        await service.DeleteBirthAsync(109, UserRole.Farmer, farm.Id, created.Id, CancellationToken.None);

        dbContext.AnimalBirths.Should().BeEmpty();
        dbContext.Balances.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateBirthAsync_UpdatesOvineBalanceDetails()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedOvineFarmAsync(dbContext, 113);
        var created = await service.CreateBirthAsync(113, UserRole.Farmer, farm.Id, new CreateFarmBirthRequest(
            new DateOnly(2026, 05, 1),
            2,
            2.3m,
            "Inicial"), CancellationToken.None);

        var updated = await service.UpdateBirthAsync(113, UserRole.Farmer, farm.Id, created.Id, new UpdateFarmBirthRequest(
            new DateOnly(2026, 05, 2),
            5,
            2.8m,
            "Actualizado"), CancellationToken.None);

        updated.OffspringNumber.Should().Be(5);
        var detail = dbContext.BalanceOvinoCaprino.Single();
        detail.NonReproductiveUnder4Months.Should().Be(5);
    }

    [Fact]
    public async Task VaccinationCrud_WorksForFarmAnimal()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedOvineFarmAsync(dbContext, 110);
        var animal = ServiceTestData.CreateAnimal(9001, farm.Id, "ES123456789210", new DateOnly(2026, 01, 10), birthYear: 2025);

        dbContext.Animals.Add(animal);
        await dbContext.SaveChangesAsync();

        var created = await service.CreateVaccinationAsync(110, UserRole.Farmer, farm.Id, new CreateFarmVaccinationRequest(
            animal.Identification,
            new DateOnly(2026, 05, 10),
            new DateOnly(2026, 06, 10),
            "Brucelosis",
            "Primera dosis"), CancellationToken.None);
        var updated = await service.UpdateVaccinationAsync(110, UserRole.Farmer, farm.Id, created.Id, new UpdateFarmVaccinationRequest(
            animal.Identification,
            new DateOnly(2026, 05, 11),
            new DateOnly(2026, 06, 11),
            "Brucelosis",
            "Recordatorio"), CancellationToken.None);
        var listed = await service.GetVaccinationsAsync(110, UserRole.Farmer, farm.Id, CancellationToken.None);

        updated.VaccinationDate.Should().Be(new DateOnly(2026, 05, 11));
        listed.Should().ContainSingle(entry => entry.Id == created.Id && entry.Observations == "Recordatorio");

        await service.DeleteVaccinationAsync(110, UserRole.Farmer, farm.Id, created.Id, CancellationToken.None);
        dbContext.Vaccinations.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateIncidentAndInspectionAsync_ReturnDataInQueries()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedOvineFarmAsync(dbContext, 111);
        var animal = ServiceTestData.CreateAnimal(9002, farm.Id, "ES123456789211", new DateOnly(2026, 01, 10), birthYear: 2025);

        dbContext.Animals.Add(animal);
        await dbContext.SaveChangesAsync();

        var incident = await service.CreateIncidentAsync(111, UserRole.Farmer, farm.Id, new CreateFarmIncidentRequest(
            animal.Identification,
            new DateOnly(2026, 05, 5),
            "Pérdida",
            "Sustitución del crotal",
            null,
            "ES123456789299"), CancellationToken.None);
        var inspection = await service.CreateInspectionAsync(111, UserRole.Farmer, farm.Id, new CreateFarmInspectionRequest(
            new DateOnly(2026, 05, 12),
            "CC",
            "Sin incidencias",
            "Dr. Test",
            12), CancellationToken.None);

        var incidents = await service.GetIncidentsAsync(111, UserRole.Farmer, farm.Id, CancellationToken.None);
        var inspections = await service.GetInspectionsAsync(111, UserRole.Farmer, farm.Id, CancellationToken.None);

        incident.AnimalIdentification.Should().Be(animal.Identification);
        incidents.Should().ContainSingle(entry => entry.Id == incident.Id && entry.NewIdentification == "ES123456789299");
        inspections.Should().ContainSingle(entry => entry.Id == inspection.Id && entry.Veterinary == "Dr. Test");
    }

    [Fact]
    public async Task GetBalanceAsync_SummarizesYear_AndUpdateCensusRemainsReadOnly()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedOvineFarmAsync(dbContext, 112);

        dbContext.Balances.AddRange(
            new Pecualia.Api.Models.Entities.Balance
            {
                Id = 9101,
                LivestockFarmId = farm.Id,
                BalanceDate = new DateOnly(2026, 01, 10),
                ModificationCause = AnimalRegistrationCause.Entrada.ToString(),
                NumberOfAnimals = 3
            },
            new Pecualia.Api.Models.Entities.Balance
            {
                Id = 9102,
                LivestockFarmId = farm.Id,
                BalanceDate = new DateOnly(2026, 02, 11),
                ModificationCause = "Nacimiento",
                NumberOfAnimals = 2
            },
            new Pecualia.Api.Models.Entities.Balance
            {
                Id = 9103,
                LivestockFarmId = farm.Id,
                BalanceDate = new DateOnly(2026, 03, 12),
                ModificationCause = AnimalDischargeCause.Muerte.ToString(),
                NumberOfAnimals = 1
            });
        await dbContext.SaveChangesAsync();

        var balance = await service.GetBalanceAsync(112, UserRole.Farmer, farm.Id, 2026, CancellationToken.None);
        var updateAction = () => service.UpdateCensusAsync(112, UserRole.Farmer, farm.Id, 2026, new UpdateFarmCensusRequest(
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1), CancellationToken.None);

        balance.Registrations.Should().Be(3);
        balance.Births.Should().Be(2);
        balance.Deaths.Should().Be(1);
        balance.Months.Should().HaveCount(12);

        await updateAction.Should().ThrowAsync<DomainException>()
            .WithMessage("El censo se calcula automáticamente y no admite edición manual.");
    }

    [Fact]
    public async Task CreateDeathAsync_RegistersIndividualOvineDeath_AndGetDeathsReturnsIt()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var farm = await SeedOvineFarmAsync(dbContext, 114);
        var animal = ServiceTestData.CreateAnimal(9003, farm.Id, "ES123456789212", new DateOnly(2026, 01, 10), birthDate: new DateOnly(2025, 02, 10), birthYear: 2025, registrationCause: AnimalRegistrationCause.Entrada, sex: "M");

        dbContext.Animals.Add(animal);
        await dbContext.SaveChangesAsync();

        var death = await service.CreateDeathAsync(114, UserRole.Farmer, farm.Id, new CreateFarmDeathRequest(
            animal.Identification,
            null,
            1,
            new DateOnly(2026, 05, 15),
            "SANDACH"), CancellationToken.None);
        var deaths = await service.GetDeathsAsync(114, UserRole.Farmer, farm.Id, CancellationToken.None);

        death.Identification.Should().Be(animal.Identification);
        deaths.Should().ContainSingle(entry => entry.AnimalId == animal.Id && entry.DischargeCause == AnimalDischargeCause.Muerte.ToString());
    }

    private static FarmOperationService CreateService(Pecualia.Api.Data.PecualiaDbContext dbContext, TestClock clock)
    {
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        return new FarmOperationService(dbContext, clock, censusProjectionService);
    }

    private static async Task<Pecualia.Api.Models.Entities.LivestockFarm> SeedOvineFarmAsync(Pecualia.Api.Data.PecualiaDbContext dbContext, long userId)
    {
        var user = ServiceTestData.CreateUser(userId, UserRole.Farmer, "Ovi", "No", email: $"ovine-{userId}@test.local");
        var farmer = ServiceTestData.CreateFarmer(userId, user, nifCif: $"111111{userId % 100:00}T");
        var farm = ServiceTestData.CreateFarm(userId + 8000, farmer.UserId, LivestockSpecies.Ovine, $"Ovina {userId}", $"ES4100101{userId:00000}");

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();

        return farm;
    }

    private static async Task<Pecualia.Api.Models.Entities.LivestockFarm> SeedPorcineFarmAsync(Pecualia.Api.Data.PecualiaDbContext dbContext, long userId)
    {
        var user = ServiceTestData.CreateUser(userId, UserRole.Farmer, "Por", "Cino", email: $"porcine-{userId}@test.local");
        var farmer = ServiceTestData.CreateFarmer(userId, user, nifCif: $"000000{userId % 100:00}T");
        var farm = ServiceTestData.CreateFarm(userId + 1000, farmer.UserId, LivestockSpecies.Porcine, $"Porcina {userId}", $"ES4100100{userId:00000}", authorisedCapacity: 50, porcineMothersCapacity: 20, porcineFatteningCapacity: 30);

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();

        return farm;
    }
}
