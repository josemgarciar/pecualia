using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Animals;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class FarmAnimalBulkUpdateServiceTests
{
    private static readonly TestClock Clock = new(new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Preview_DoesNotWrite_AndCommitIsIdempotent()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var (farm, animals) = await SeedAsync(db);
        var service = new FarmAnimalBulkUpdateService(db, Clock);
        var changes = Changes(
            registrationCause: new(BulkFieldChangeMode.Set, AnimalRegistrationCause.Entrada),
            registrationDate: new(BulkFieldChangeMode.Set, new DateOnly(2026, 7, 10)));
        var preview = await service.PreviewAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            Preview(new[] { animals[0].Id, animals[1].Id }, changes),
            CancellationToken.None);

        preview.ConflictAnimals.Should().Be(0);
        (await db.Animals.SingleAsync(entity => entity.Id == animals[0].Id)).RegistrationDate
            .Should().Be(new DateOnly(2026, 1, 1));

        var operationId = Guid.NewGuid();
        var request = new CommitAnimalBulkUpdateRequest(
            operationId,
            preview.ResolvedAnimalIds,
            preview.StateFingerprint,
            changes);
        var first = await service.CommitAsync(farm.FarmerId, UserRole.Farmer, farm.Id, request, CancellationToken.None);
        var replay = await service.CommitAsync(farm.FarmerId, UserRole.Farmer, farm.Id, request, CancellationToken.None);

        first.UpdatedAnimals.Should().Be(2);
        first.Replayed.Should().BeFalse();
        replay.Should().Be(first with { Replayed = true });
        (await db.Animals.Where(entity => preview.ResolvedAnimalIds.Contains(entity.Id)).ToListAsync())
            .Should().OnlyContain(entity =>
                entity.RegistrationCause == AnimalRegistrationCause.Entrada &&
                entity.RegistrationDate == new DateOnly(2026, 7, 10));
        (await db.AnimalBulkUpdateOperations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Preview_ReportsPairConflict_AndCommitWritesNothing()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var (farm, animals) = await SeedAsync(db);
        var service = new FarmAnimalBulkUpdateService(db, Clock);
        var changes = Changes(registrationCause: new(BulkFieldChangeMode.Clear, null));
        var preview = await service.PreviewAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            Preview(new[] { animals[0].Id }, changes),
            CancellationToken.None);

        preview.ConflictAnimals.Should().Be(1);
        preview.Rows[0].Message.Should().Contain("deben informarse o borrarse juntas");

        var action = () => service.CommitAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            new CommitAnimalBulkUpdateRequest(Guid.NewGuid(), preview.ResolvedAnimalIds, preview.StateFingerprint, changes),
            CancellationToken.None);
        await action.Should().ThrowAsync<DomainException>();
        (await db.Animals.SingleAsync(entity => entity.Id == animals[0].Id)).RegistrationCause
            .Should().Be(AnimalRegistrationCause.Autorreposicion);
    }

    [Fact]
    public async Task FilteredSelection_HonoursSearchAndExclusions()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var (farm, animals) = await SeedAsync(db);
        animals[0].Breed = "Merina";
        animals[1].Breed = "Merina";
        animals[2].Breed = "Lacaune";
        await db.SaveChangesAsync();
        var service = new FarmAnimalBulkUpdateService(db, Clock);
        var selection = new BulkAnimalSelectionRequest(
            BulkAnimalSelectionMode.Filtered,
            null,
            "merina",
            null,
            null,
            null,
            new[] { animals[1].Id });

        var preview = await service.PreviewAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            new PreviewAnimalBulkUpdateRequest(
                selection,
                Changes(dischargeCause: new(BulkFieldChangeMode.Set, AnimalDischargeCause.Muerte),
                    dischargeDate: new(BulkFieldChangeMode.Set, new DateOnly(2026, 7, 20)))),
            CancellationToken.None);

        preview.ResolvedAnimalIds.Should().Equal(animals[0].Id);
    }

    [Fact]
    public async Task Commit_CreatesConfirmedEntryGuide_WithoutBalancesOrCensus()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var (farm, animals) = await SeedAsync(db);
        var service = new FarmAnimalBulkUpdateService(db, Clock);
        var arrival = new DateTime(2026, 7, 15, 11, 0, 0, DateTimeKind.Utc);
        var changes = Changes(
            registrationCause: new(BulkFieldChangeMode.Set, AnimalRegistrationCause.Entrada),
            registrationDate: new(BulkFieldChangeMode.Set, new DateOnly(2026, 7, 15)),
            guide: EntryGuide(arrival));
        var preview = await service.PreviewAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            Preview(new[] { animals[0].Id, animals[1].Id }, changes),
            CancellationToken.None);

        var result = await service.CommitAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            new CommitAnimalBulkUpdateRequest(Guid.NewGuid(), preview.ResolvedAnimalIds, preview.StateFingerprint, changes),
            CancellationToken.None);

        result.LinkedAnimals.Should().Be(2);
        var movement = await db.MovementCertificates.SingleAsync();
        movement.Status.Should().Be(MovementStatus.Confirmed);
        movement.DestinationLivestockId.Should().Be(farm.Id);
        movement.CodRemo.Should().Be("REMO-2026");
        movement.Serie.Should().Be("SERIE-1");
        (await db.MovementCertificateAnimals.CountAsync()).Should().Be(2);
        (await db.Balances.CountAsync()).Should().Be(0);
        (await db.Census.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Commit_ReusesAndConfirmsMatchingPendingGuide()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var (farm, animals) = await SeedAsync(db);
        var arrival = new DateTime(2026, 7, 15, 11, 0, 0, DateTimeKind.Utc);
        var movement = BuildMatchingEntryGuide(800, farm.Id, arrival);
        movement.Status = MovementStatus.Pending;
        db.MovementCertificates.Add(movement);
        db.MovementCertificateAnimals.Add(new MovementCertificateAnimal
        {
            Id = 900,
            MovementCertificateId = movement.Id,
            AnimalId = animals[0].Id
        });
        await db.SaveChangesAsync();
        var service = new FarmAnimalBulkUpdateService(db, Clock);
        var changes = Changes(
            registrationCause: new(BulkFieldChangeMode.Set, AnimalRegistrationCause.Entrada),
            registrationDate: new(BulkFieldChangeMode.Set, new DateOnly(2026, 7, 15)),
            guide: EntryGuide(arrival));
        var preview = await service.PreviewAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            Preview(new[] { animals[0].Id, animals[1].Id }, changes),
            CancellationToken.None);

        preview.Guide.MovementId.Should().Be(movement.Id);
        preview.Guide.Resolution.Should().Contain("confirmar");
        var result = await service.CommitAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            new CommitAnimalBulkUpdateRequest(Guid.NewGuid(), preview.ResolvedAnimalIds, preview.StateFingerprint, changes),
            CancellationToken.None);

        result.LinkedAnimals.Should().Be(1);
        (await db.MovementCertificates.SingleAsync()).Status.Should().Be(MovementStatus.Confirmed);
        (await db.MovementCertificateAnimals.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Commit_RejectsStalePreview()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var (farm, animals) = await SeedAsync(db);
        var service = new FarmAnimalBulkUpdateService(db, Clock);
        var changes = Changes(
            dischargeCause: new(BulkFieldChangeMode.Set, AnimalDischargeCause.Muerte),
            dischargeDate: new(BulkFieldChangeMode.Set, new DateOnly(2026, 7, 20)));
        var preview = await service.PreviewAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            Preview(new[] { animals[0].Id }, changes),
            CancellationToken.None);
        animals[0].RegistrationDate = new DateOnly(2026, 2, 2);
        await db.SaveChangesAsync();

        var action = () => service.CommitAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            new CommitAnimalBulkUpdateRequest(Guid.NewGuid(), preview.ResolvedAnimalIds, preview.StateFingerprint, changes),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("*obsoleta*");
    }

    [Fact]
    public async Task Commit_ClearLatestGuide_RemovesOnlyNewestLink()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var (farm, animals) = await SeedAsync(db);
        var older = BuildMatchingEntryGuide(810, farm.Id, new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc));
        older.CodRemo = "OLD";
        older.Serie = "OLD";
        var newest = BuildMatchingEntryGuide(811, farm.Id, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc));
        newest.CodRemo = "NEW";
        newest.Serie = "NEW";
        db.MovementCertificates.AddRange(older, newest);
        db.MovementCertificateAnimals.AddRange(
            new MovementCertificateAnimal { Id = 910, MovementCertificateId = older.Id, AnimalId = animals[0].Id },
            new MovementCertificateAnimal { Id = 911, MovementCertificateId = newest.Id, AnimalId = animals[0].Id });
        await db.SaveChangesAsync();
        var service = new FarmAnimalBulkUpdateService(db, Clock);
        var changes = Changes(guide: EntryGuide(
            new DateTime(2026, 7, 15, 11, 0, 0, DateTimeKind.Utc),
            BulkGuideAction.ClearLatestEntry));
        var preview = await service.PreviewAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            Preview(new[] { animals[0].Id }, changes),
            CancellationToken.None);

        await service.CommitAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            new CommitAnimalBulkUpdateRequest(Guid.NewGuid(), preview.ResolvedAnimalIds, preview.StateFingerprint, changes),
            CancellationToken.None);

        var remaining = await db.MovementCertificateAnimals.SingleAsync();
        remaining.MovementCertificateId.Should().Be(older.Id);
    }

    [Fact]
    public async Task Preview_NullSelection_ReturnsControlledDomainError()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var (farm, _) = await SeedAsync(db);
        var service = new FarmAnimalBulkUpdateService(db, Clock);
        var request = new PreviewAnimalBulkUpdateRequest(
            null!,
            Changes(dischargeCause: new(BulkFieldChangeMode.Clear, null),
                dischargeDate: new(BulkFieldChangeMode.Clear, null)));

        var action = () => service.PreviewAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            request,
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("*selección de animales es obligatoria*");
    }

    [Fact]
    public async Task Commit_NullAnimalIds_ReturnsControlledDomainError()
    {
        await using var db = ServiceTestDbFactory.CreateContext();
        var (farm, _) = await SeedAsync(db);
        var service = new FarmAnimalBulkUpdateService(db, Clock);
        var request = new CommitAnimalBulkUpdateRequest(
            Guid.NewGuid(),
            null!,
            "fingerprint",
            Changes(dischargeCause: new(BulkFieldChangeMode.Clear, null),
                dischargeDate: new(BulkFieldChangeMode.Clear, null)));

        var action = () => service.CommitAsync(
            farm.FarmerId,
            UserRole.Farmer,
            farm.Id,
            request,
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("*Selecciona al menos un animal*");
    }

    private static PreviewAnimalBulkUpdateRequest Preview(
        IReadOnlyList<long> ids,
        BulkAnimalUpdateDefinition changes) =>
        new(
            new BulkAnimalSelectionRequest(
                BulkAnimalSelectionMode.Explicit,
                ids,
                null,
                null,
                null,
                null,
                null),
            changes);

    private static BulkAnimalUpdateDefinition Changes(
        BulkRegistrationCauseChange? registrationCause = null,
        BulkDischargeCauseChange? dischargeCause = null,
        BulkDateChange? registrationDate = null,
        BulkDateChange? dischargeDate = null,
        BulkAnimalGuideRequest? guide = null) =>
        new(
            registrationCause ?? new(BulkFieldChangeMode.Unchanged, null),
            dischargeCause ?? new(BulkFieldChangeMode.Unchanged, null),
            registrationDate ?? new(BulkFieldChangeMode.Unchanged, null),
            dischargeDate ?? new(BulkFieldChangeMode.Unchanged, null),
            guide ?? EntryGuide(DateTime.UtcNow, BulkGuideAction.Unchanged));

    private static BulkAnimalGuideRequest EntryGuide(
        DateTime arrival,
        BulkGuideAction action = BulkGuideAction.SetEntry) =>
        new(
            action,
            action is BulkGuideAction.SetEntry or BulkGuideAction.SetExit ? MovementCounterpartyType.External : null,
            null,
            "ES410010009999",
            "Explotación externa",
            "REMO-2026",
            "SERIE-1",
            arrival.AddHours(-1),
            arrival,
            arrival.AddDays(-1),
            "Camión",
            "Transportes Test",
            "1234-ABC");

    private static MovementCertificate BuildMatchingEntryGuide(long id, long farmId, DateTime arrival) =>
        new()
        {
            Id = id,
            DestinationLivestockId = farmId,
            OriginExternalCode = "ES410010009999",
            OriginExternalName = "Explotación externa",
            CodRemo = "REMO-2026",
            Serie = "SERIE-1",
            DepartureDate = arrival.AddHours(-1),
            ArrivalDate = arrival,
            SolicitationDate = arrival.AddDays(-1),
            MeansOfTransport = "Camión",
            TransportName = "Transportes Test",
            VehicleRegistrationNumber = "1234-ABC",
            NumberOfAnimals = 1,
            Specie = LivestockSpecies.Ovine.ToString(),
            Status = MovementStatus.Confirmed
        };

    private static async Task<(LivestockFarm Farm, Animal[] Animals)> SeedAsync(
        Pecualia.Api.Data.PecualiaDbContext db)
    {
        const long userId = 70;
        var user = ServiceTestData.CreateUser(userId, UserRole.Farmer, "Titular", "Masivo", email: "bulk@test.local");
        var farmer = ServiceTestData.CreateFarmer(userId, user, nifCif: "12345670Z");
        var farm = ServiceTestData.CreateFarm(5070, userId, LivestockSpecies.Ovine, "Ovino", "ES410010001070");
        var animals = new[]
        {
            ServiceTestData.CreateAnimal(701, farm.Id, "ES060000580701", new DateOnly(2026, 1, 1), registrationCause: AnimalRegistrationCause.Autorreposicion),
            ServiceTestData.CreateAnimal(702, farm.Id, "ES060000580702", new DateOnly(2026, 1, 1), registrationCause: AnimalRegistrationCause.Autorreposicion),
            ServiceTestData.CreateAnimal(703, farm.Id, "ES060000580703", new DateOnly(2026, 1, 1), registrationCause: AnimalRegistrationCause.Autorreposicion)
        };
        db.Users.Add(user);
        db.Farmers.Add(farmer);
        db.Farms.Add(farm);
        db.Animals.AddRange(animals);
        await db.SaveChangesAsync();
        return (farm, animals);
    }
}
