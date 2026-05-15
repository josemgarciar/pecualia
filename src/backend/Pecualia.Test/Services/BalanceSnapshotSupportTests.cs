using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class BalanceSnapshotSupportTests
{
    [Fact]
    public async Task UpsertBalanceSnapshotAsync_CreatesPorcineDetail_AndNormalizesMetadata()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var farm = ServiceTestData.CreateFarm(100, 10, LivestockSpecies.Porcine, "Porcina", "ES410010009001");
        var balance = new Balance
        {
            Id = 200,
            LivestockFarmId = farm.Id,
            BalanceDate = new DateOnly(2026, 05, 15),
            ModificationCause = "Nacimiento",
            NumberOfAnimals = 8
        };

        dbContext.Farms.Add(farm);
        dbContext.Balances.Add(balance);
        await dbContext.SaveChangesAsync();

        await BalanceSnapshotSupport.UpsertBalanceSnapshotAsync(
            dbContext,
            balance,
            farm,
            CreateSnapshot(boars: 1, sowsForLive: 2, sowsReposition: 3, malesReposition: 4, piglets: 5, rears: 6, baits: 7),
            CancellationToken.None,
            porcineMetadata: new PorcineBalanceMetadata("  Lechones  ", "   ", "  TAG-001  "));
        await dbContext.SaveChangesAsync();

        var detail = await dbContext.BalancePorcino.SingleAsync(entity => entity.BalanceId == balance.Id);

        detail.Type.Should().Be("Lechones");
        detail.Breed.Should().BeNull();
        detail.Tag.Should().Be("TAG-001");
        detail.Boars.Should().Be(1);
        detail.SowsForLive.Should().Be(2);
        detail.SowsReposition.Should().Be(3);
        detail.PigsReposition.Should().Be(4);
        detail.Piglets.Should().Be(5);
        detail.Rear.Should().Be(6);
        detail.Baits.Should().Be(7);
    }

    [Fact]
    public async Task UpsertBalanceSnapshotAsync_UpdatesExistingOvineDetail_AndNormalizesMetadata()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var farm = ServiceTestData.CreateFarm(101, 11, LivestockSpecies.Ovine, "Ovina", "ES410010009002");
        var balance = new Balance
        {
            Id = 201,
            LivestockFarmId = farm.Id,
            BalanceDate = new DateOnly(2026, 05, 15),
            ModificationCause = "Compra",
            NumberOfAnimals = 4
        };
        var existing = new BalanceOvinoCaprino
        {
            BalanceId = balance.Id,
            NonReproductiveUnder4Months = 9,
            NonReproductiveBetween4And12Months = 8,
            ReproductiveFemales = 7,
            ReproductiveMales = 6,
            TransporterName = "Anterior",
            TransportTicketNumber = "OLD"
        };

        dbContext.Farms.Add(farm);
        dbContext.Balances.Add(balance);
        dbContext.BalanceOvinoCaprino.Add(existing);
        await dbContext.SaveChangesAsync();

        await BalanceSnapshotSupport.UpsertBalanceSnapshotAsync(
            dbContext,
            balance,
            farm,
            CreateSnapshot(nonReproductiveUnder4Months: 1, nonReproductiveBetween4And12Months: 2, reproductiveFemales: 3, reproductiveMales: 4),
            CancellationToken.None,
            ovineMetadata: new OvineBalanceMetadata("  Transportes Ruiz  ", "   "));
        await dbContext.SaveChangesAsync();

        var details = await dbContext.BalanceOvinoCaprino.Where(entity => entity.BalanceId == balance.Id).ToListAsync();

        details.Should().ContainSingle();
        details[0].NonReproductiveUnder4Months.Should().Be(1);
        details[0].NonReproductiveBetween4And12Months.Should().Be(2);
        details[0].ReproductiveFemales.Should().Be(3);
        details[0].ReproductiveMales.Should().Be(4);
        details[0].TransporterName.Should().Be("Transportes Ruiz");
        details[0].TransportTicketNumber.Should().BeNull();
    }

    [Fact]
    public async Task UpsertCensusSnapshotAsync_CreatesPorcineDetail()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var farm = ServiceTestData.CreateFarm(102, 12, LivestockSpecies.Porcine, "Porcina 2", "ES410010009003");
        var census = new Census
        {
            Id = 300,
            LivestockFarmId = farm.Id,
            CensusDate = new DateOnly(2026, 12, 31)
        };

        dbContext.Farms.Add(farm);
        dbContext.Census.Add(census);
        await dbContext.SaveChangesAsync();

        await BalanceSnapshotSupport.UpsertCensusSnapshotAsync(
            dbContext,
            census,
            farm,
            CreateSnapshot(boars: 2, sowsForLive: 4, sowsReposition: 6, malesReposition: 8, piglets: 10, rears: 12, baits: 14),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var detail = await dbContext.CensusPorcino.SingleAsync(entity => entity.CensusId == census.Id);

        detail.Boars.Should().Be(2);
        detail.Sow.Should().Be(4);
        detail.SowsReposition.Should().Be(6);
        detail.PigsReposition.Should().Be(8);
        detail.Piglets.Should().Be(10);
        detail.Rears.Should().Be(12);
        detail.Baits.Should().Be(14);
    }

    [Fact]
    public async Task UpsertCensusSnapshotAsync_UpdatesExistingOvineDetail()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var farm = ServiceTestData.CreateFarm(103, 13, LivestockSpecies.Caprine, "Caprina", "ES410010009004");
        var census = new Census
        {
            Id = 301,
            LivestockFarmId = farm.Id,
            CensusDate = new DateOnly(2026, 12, 31)
        };
        var existing = new CensusOvinoCaprino
        {
            CensusId = census.Id,
            NonReproductiveUnder4Months = 20,
            NonReproductiveBetween4And12Months = 21,
            ReproductiveFemale = 22,
            ReproductiveMale = 23
        };

        dbContext.Farms.Add(farm);
        dbContext.Census.Add(census);
        dbContext.CensusOvinoCaprino.Add(existing);
        await dbContext.SaveChangesAsync();

        await BalanceSnapshotSupport.UpsertCensusSnapshotAsync(
            dbContext,
            census,
            farm,
            CreateSnapshot(nonReproductiveUnder4Months: 5, nonReproductiveBetween4And12Months: 6, reproductiveFemales: 7, reproductiveMales: 8),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var details = await dbContext.CensusOvinoCaprino.Where(entity => entity.CensusId == census.Id).ToListAsync();

        details.Should().ContainSingle();
        details[0].NonReproductiveUnder4Months.Should().Be(5);
        details[0].NonReproductiveBetween4And12Months.Should().Be(6);
        details[0].ReproductiveFemale.Should().Be(7);
        details[0].ReproductiveMale.Should().Be(8);
    }

    private static FarmCensusResponse CreateSnapshot(
        int nonReproductiveUnder4Months = 0,
        int nonReproductiveBetween4And12Months = 0,
        int reproductiveFemales = 0,
        int reproductiveMales = 0,
        int boars = 0,
        int sowsForLive = 0,
        int sowsReposition = 0,
        int malesReposition = 0,
        int piglets = 0,
        int rears = 0,
        int baits = 0)
    {
        return new FarmCensusResponse(
            null,
            1,
            2026,
            LivestockSpecies.Porcine.ToString(),
            nonReproductiveUnder4Months,
            nonReproductiveBetween4And12Months,
            reproductiveFemales,
            reproductiveMales,
            boars,
            sowsForLive,
            sowsReposition,
            malesReposition,
            piglets,
            rears,
            baits,
            0,
            nonReproductiveUnder4Months + nonReproductiveBetween4And12Months + reproductiveFemales + reproductiveMales + boars + sowsForLive + sowsReposition + malesReposition + piglets + rears + baits,
            []);
    }
}
