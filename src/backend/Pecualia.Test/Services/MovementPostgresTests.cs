using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Movements;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class MovementPostgresTests
{
    [PostgresFact]
    public async Task ExitTxt_With17ValidAnimals_AndHistoricalUnidentifiedExit_PersistsNonNegativeCensus()
    {
        await using var database = new PostgresTestDatabase();
        await database.InitializeAsync();
        await using var db = database.CreateContext();
        var (userId, request) = await SeedAsync(db);
        db.MovementCertificates.Add(new MovementCertificate
        {
            OriginLivestockId = request.FarmId,
            DepartureDate = request.DepartureDate.AddMonths(-6),
            NumberOfAnimals = 100,
            Specie = LivestockSpecies.Ovine.ToString(),
            UnidentifiedCategory = MovementUnidentifiedCategory.Under4Months
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var preview = await service.PreviewImportAsync(userId, UserRole.Farmer, Preview(request), default);
        preview.Summary.ValidRows.Should().Be(17);

        var result = await service.CommitImportAsync(userId, UserRole.Farmer, request, default);

        result.ProcessedRows.Should().Be(17);
        db.ChangeTracker.Clear();
        (await db.Animals.CountAsync(a => a.DischargeDate != null)).Should().Be(17);
        (await db.MovementCertificateAnimals.CountAsync()).Should().Be(17);
        (await db.MovementCertificates.CountAsync()).Should().Be(2);
        (await db.Balances.SingleAsync()).NumberOfAnimals.Should().Be(17);
        var census = await db.CensusOvinoCaprino.SingleAsync();
        census.NonReproductiveUnder4Months.Should().Be(0);
        census.NonReproductiveBetween4And12Months.Should().Be(0);
        census.ReproductiveFemale.Should().Be(0);
        census.ReproductiveMale.Should().Be(0);
    }

    [PostgresFact]
    public async Task ExitTxt_With17ValidAnimals_PersistsGuideDischargesBalanceAndCensus()
    {
        await using var database = new PostgresTestDatabase();
        await database.InitializeAsync();
        await using var db = database.CreateContext();
        var (userId, request) = await SeedAsync(db);
        var service = CreateService(db);

        var preview = await service.PreviewImportAsync(userId, UserRole.Farmer, Preview(request), default);
        preview.Summary.ValidRows.Should().Be(17);
        var result = await service.CommitImportAsync(userId, UserRole.Farmer, request, default);

        result.ProcessedRows.Should().Be(17);
        result.RejectedRows.Should().Be(0);
        db.ChangeTracker.Clear();
        (await db.MovementCertificates.SingleAsync()).NumberOfAnimals.Should().Be(17);
        (await db.MovementCertificateAnimals.CountAsync()).Should().Be(17);
        (await db.Animals.CountAsync(a => a.DischargeDate == new DateOnly(2026, 9, 17)
            && a.DischargeCause == AnimalDischargeCause.Salida && a.DestinationCode == request.CounterpartyExternalCode)).Should().Be(17);
        (await db.Balances.SingleAsync()).NumberOfAnimals.Should().Be(17);
        (await db.Census.CountAsync()).Should().Be(1);
        (await db.CensusOvinoCaprino.CountAsync()).Should().Be(1);
    }

    private static MovementService CreateService(PecualiaDbContext db)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero));
        return new MovementService(db, new FarmCensusProjectionService(db, clock), clock);
    }

    private static async Task<(long UserId, CommitMovementImportRequest Request)> SeedAsync(PecualiaDbContext db)
    {
        var user = ServiceTestData.CreateUser(0, UserRole.Farmer, "Prueba", "Ficticia");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var farmer = ServiceTestData.CreateFarmer(user.Id, user);
        db.Farmers.Add(farmer);
        await db.SaveChangesAsync();
        var farm = ServiceTestData.CreateFarm(0, farmer.UserId, LivestockSpecies.Ovine, "Prueba ficticia", "ES410010000100");
        db.Farms.Add(farm);
        await db.SaveChangesAsync();
        var identifications = Enumerable.Range(0, 17).Select(index => $"ES{123456789010L + index}").ToArray();
        db.Animals.AddRange(identifications.Select(id => ServiceTestData.CreateAnimal(0, farm.Id, id,
            new DateOnly(2026, 1, 1), birthYear: 2025, sex: "H", registrationCause: AnimalRegistrationCause.Entrada)));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var departure = new DateTime(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);
        return (user.Id, new CommitMovementImportRequest(
            farm.Id, MovementImportOperation.Baja, "ES410010009999", "Destino ficticio", "REMO-TEST", "SERIE-TEST",
            departure, departure.AddHours(1), null, "Camión", "Transportista ficticio", "1234BCD", MovementImportCause.Salida,
            null, null, null, string.Join("\r\n", identifications), null, null, null));
    }

    private static PreviewMovementImportRequest Preview(CommitMovementImportRequest r) => new(
        r.FarmId, r.Operation, r.CounterpartyExternalCode, r.CounterpartyExternalName, r.CodRemo, r.Serie,
        r.DepartureDate, r.ArrivalDate, r.SolicitationDate, r.MeansOfTransport, r.TransportName, r.VehicleRegistrationNumber,
        r.Cause, r.NumberOfAnimals, r.Breed, r.AnimalType, r.RawText, r.SharedAnimalData, r.UnidentifiedAnimalCount, r.UnidentifiedCategory);
}
