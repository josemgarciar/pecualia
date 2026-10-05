using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Movements;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class MovementBirthLotTests
{
    private static readonly TestClock Clock = new(new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task PartialExit_ConsumesSelectedLots_AndPreservesTheAgesOfRemainingAnimals()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var (farm, first, second) = await SeedAsync(db);
        var service = CreateService(db);
        var request = Exit(farm.Id, new DateOnly(2026, 3, 1),
            [new(first.Id, 3), new(second.Id, 2)]);
        var response = await service.CommitImportAsync(farm.FarmerId, UserRole.Farmer, request, default);
        db.ChangeTracker.Clear();
        var detail = await service.GetMovementAsync(farm.FarmerId, UserRole.Farmer, response.MovementId, default);
        detail.BirthLots.Should().BeEquivalentTo(request.BirthLots);
        var projection = new FarmCensusProjectionService(db, Clock);
        var snapshot = await projection.BuildSnapshotAsync(farm, new DateOnly(2026, 5, 1), default);
        snapshot.NonReproductiveUnder4Months.Should().Be(8);
        snapshot.NonReproductiveBetween4And12Months.Should().Be(7);
        snapshot.Total.Should().Be(15);
        var options = await service.GetBirthLotsAsync(farm.FarmerId, UserRole.Farmer, farm.Id, new DateOnly(2026, 3, 1), null, default);
        options.Lots.Select(lot => lot.Available).Should().Equal(7, 8);
    }

    [Fact]
    public async Task MixedAgeLots_AreClassifiedAutomatically_WithoutARequestedCategory()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var (farm, first, second) = await SeedAsync(db);
        var response = await CreateService(db).CommitImportAsync(farm.FarmerId, UserRole.Farmer,
            Exit(farm.Id, new DateOnly(2026, 5, 1), [new(first.Id, 3), new(second.Id, 2)]), default);
        var movement = await db.MovementCertificates.SingleAsync(item => item.Id == response.MovementId);
        movement.UnidentifiedCategory.Should().Be(MovementUnidentifiedCategory.BirthLots);
        var delta = await db.BalanceOvinoCaprino.SingleAsync();
        delta.NonReproductiveUnder4Months.Should().Be(2);
        delta.NonReproductiveBetween4And12Months.Should().Be(3);
        var snapshot = await new FarmCensusProjectionService(db, Clock).BuildSnapshotAsync(farm, new DateOnly(2026, 5, 1), default);
        snapshot.NonReproductiveUnder4Months.Should().Be(8);
        snapshot.NonReproductiveBetween4And12Months.Should().Be(7);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("overdraw")]
    [InlineData("foreign")]
    [InlineData("older")]
    public async Task InvalidSelections_DoNotWriteAMovement(string scenario)
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var (farm, first, _) = await SeedAsync(db);
        var request = Exit(farm.Id, new DateOnly(2026, 3, 1), [new(first.Id, 5)]);
        request = scenario switch
        {
            "missing" => request with { BirthLots = null },
            "duplicate" => request with { BirthLots = [new(first.Id, 2), new(first.Id, 3)] },
            "overdraw" => request with { BirthLots = [new(first.Id, 11)], UnidentifiedAnimalCount = 11 },
            "foreign" => request with { BirthLots = [new(9999, 5)] },
            "older" => request with { DepartureDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), ArrivalDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            _ => request
        };
        var action = () => CreateService(db).CommitImportAsync(farm.FarmerId, UserRole.Farmer, request, default);
        await action.Should().ThrowAsync<DomainException>();
        db.MovementCertificates.Should().BeEmpty();
        db.MovementBirthLots.Should().BeEmpty();
    }

    [Fact]
    public async Task HistoricalGuide_CanBeExplicitlyLinked_ButOnlyByTheOwner()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var (farm, first, _) = await SeedAsync(db);
        var movement = new MovementCertificate
        {
            OriginLivestockId = farm.Id, Specie = "Ovine", NumberOfAnimals = 5,
            DepartureDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            UnidentifiedCategory = MovementUnidentifiedCategory.Under4Months
        };
        db.MovementCertificates.Add(movement);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var request = new AssignMovementBirthLotsRequest([new(first.Id, 5)]);
        var denied = () => service.AssignBirthLotsAsync(999, UserRole.Farmer, movement.Id, request, default);
        await denied.Should().ThrowAsync<DomainException>();
        await service.AssignBirthLotsAsync(farm.FarmerId, UserRole.Farmer, movement.Id, request, default);
        await service.AssignBirthLotsAsync(farm.FarmerId, UserRole.Farmer, movement.Id, request, default);
        db.MovementBirthLots.Should().ContainSingle().Which.Quantity.Should().Be(5);
        var options = await service.GetBirthLotsAsync(farm.FarmerId, UserRole.Farmer, farm.Id, new DateOnly(2026, 3, 1), null, default);
        options.UnallocatedMovements.Should().Be(0);
        options.Lots.Single(lot => lot.BirthId == first.Id).Available.Should().Be(5);
    }

    [PostgresFact]
    public async Task HistoricalAssignments_Postgres_PersistAndIdentifyTheActualPendingGuides()
    {
        await using var database = new PostgresTestDatabase();
        await database.InitializeAsync(useCurrentModel: true);
        await using var db = database.CreateContext();
        await VerifyHistoricalAssignmentsAsync(db);
    }

    [Fact]
    public async Task HistoricalAssignments_IdentifyLaterPendingGuides_WithoutLosingEarlierAssignments()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        await VerifyHistoricalAssignmentsAsync(db);
    }

    private static async Task VerifyHistoricalAssignmentsAsync(PecualiaDbContext db)
    {
        var (farm, birth, _) = await SeedAsync(db);
        var guides = new[] { "2026-02-03", "2026-03-04", "2026-06-04", "2026-07-01", "2026-07-29" }
            .Select((date, index) => new MovementCertificate
            {
                OriginLivestockId = farm.Id, Specie = "Ovine", NumberOfAnimals = 1,
                Serie = $"SYNTHETIC-{index}",
                DepartureDate = DateTime.SpecifyKind(DateOnly.Parse(date).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                UnidentifiedCategory = MovementUnidentifiedCategory.Under4Months
            }).ToList();
        db.MovementCertificates.AddRange(guides);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var assignment = new AssignMovementBirthLotsRequest([new(birth.Id, 1)]);
        foreach (var guide in guides.Take(2))
            await service.AssignBirthLotsAsync(farm.FarmerId, UserRole.Farmer, guide.Id, assignment, default);

        db.ChangeTracker.Clear();
        var options = await service.GetBirthLotsAsync(farm.FarmerId, UserRole.Farmer, farm.Id,
            new DateOnly(2026, 6, 4), guides[2].Id, default);
        options.UnallocatedMovements.Should().Be(2);
        options.UnallocatedGuides.Select(guide => guide.Id).Should().Equal(guides.Skip(3).Select(guide => guide.Id));
        options.UnallocatedGuides.Select(guide => guide.DepartureDate).Should().Equal(guides.Skip(3).Select(guide => guide.DepartureDate));
        options.Lots.Single(lot => lot.BirthId == birth.Id).Available.Should().Be(8);

        var newExit = () => service.CommitImportAsync(farm.FarmerId, UserRole.Farmer,
            Exit(farm.Id, new DateOnly(2026, 8, 1), [new(birth.Id, 1)]), default);
        await newExit.Should().ThrowAsync<DomainException>().WithMessage("*siguen pendientes*");
        // Existing guides must remain assignable even when later guides are still pending.
        foreach (var guide in guides.Skip(2))
            await service.AssignBirthLotsAsync(farm.FarmerId, UserRole.Farmer, guide.Id, assignment, default);

        db.ChangeTracker.Clear();
        options = await service.GetBirthLotsAsync(farm.FarmerId, UserRole.Farmer, farm.Id,
            new DateOnly(2026, 8, 1), null, default);
        options.UnallocatedMovements.Should().Be(0);
        options.UnallocatedGuides.Should().BeEmpty();
        options.Lots.Single(lot => lot.BirthId == birth.Id).Available.Should().Be(5);
        foreach (var guide in guides)
        {
            var detail = await service.GetMovementAsync(farm.FarmerId, UserRole.Farmer, guide.Id, default);
            detail.BirthLots.Should().BeEquivalentTo(assignment.BirthLots);
        }
        await newExit.Should().NotThrowAsync();
    }

    [PostgresFact]
    public async Task ConcurrentExits_CannotConsumeTheSameBirthLotTwice()
    {
        await using var database = new PostgresTestDatabase();
        await database.InitializeAsync(useCurrentModel: true);
        long farmId, ownerId, birthId;
        await using (var setup = database.CreateContext())
        {
            var (farm, birth, _) = await SeedAsync(setup);
            farmId = farm.Id; ownerId = farm.FarmerId; birthId = birth.Id;
        }
        await using var first = database.CreateContext();
        await using var second = database.CreateContext();
        var request = Exit(farmId, new DateOnly(2026, 3, 1), [new(birthId, 8)]);
        var results = await Task.WhenAll(
            Record.ExceptionAsync(() => CreateService(first).CommitImportAsync(ownerId, UserRole.Farmer, request, default)),
            Record.ExceptionAsync(() => CreateService(second).CommitImportAsync(ownerId, UserRole.Farmer, request with { Serie = "SYNTHETIC-2" }, default)));
        results.Should().ContainSingle(error => error == null);
        results.Should().ContainSingle(error => error is DomainException);
        await using var verify = database.CreateContext();
        (await verify.MovementBirthLots.SumAsync(lot => lot.Quantity)).Should().Be(8);
        (await verify.MovementCertificates.CountAsync()).Should().Be(1);
    }

    private static MovementService CreateService(PecualiaDbContext db) => new(db, new FarmCensusProjectionService(db, Clock), Clock);

    private static CommitMovementImportRequest Exit(long farmId, DateOnly date, IReadOnlyList<MovementBirthLotRequest> lots) => new(
        farmId, MovementImportOperation.Baja, "ES410010009137", "Synthetic destination", null, "SYNTHETIC-1",
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
        null, null, null, null, MovementImportCause.Salida, null, null, null, null, null,
        lots.Sum(lot => lot.Quantity), null, lots);

    private static async Task<(LivestockFarm, AnimalBirth, AnimalBirth)> SeedAsync(PecualiaDbContext db)
    {
        var user = ServiceTestData.CreateUser(0, UserRole.Farmer, "Synthetic", "Owner", email: "lots@example.test");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var farmer = ServiceTestData.CreateFarmer(user.Id, user);
        db.Farmers.Add(farmer);
        var farm = ServiceTestData.CreateFarm(0, user.Id, LivestockSpecies.Ovine, "Synthetic farm", "ES410010000137");
        db.Farms.Add(farm);
        await db.SaveChangesAsync();
        var first = new AnimalBirth { LivestockFarmId = farm.Id, BirthDate = new DateOnly(2026, 1, 1), OffspringNumber = 10 };
        var second = new AnimalBirth { LivestockFarmId = farm.Id, BirthDate = new DateOnly(2026, 2, 1), OffspringNumber = 10 };
        db.AnimalBirths.AddRange(first, second);
        await db.SaveChangesAsync();
        return (farm, first, second);
    }
}
