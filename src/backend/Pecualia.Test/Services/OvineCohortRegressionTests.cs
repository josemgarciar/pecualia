using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class OvineCohortRegressionTests
{
    [Theory]
    [InlineData(LivestockSpecies.Ovine)]
    [InlineData(LivestockSpecies.Caprine)]
    public async Task ConfirmedBirthAndExitSequence_LeavesOnlyUnsoldStock(LivestockSpecies species)
    {
        await using var database = ServiceTestDbFactory.CreateContext();
        var farm = ServiceTestData.CreateFarm(1, 1, species, "Synthetic sequence", "ES000000000001");
        database.Farms.Add(farm);
        var births = new[]
        {
            ("2025-08-15", 45), ("2025-12-01", 100), ("2025-12-15", 110),
            ("2026-01-02", 46), ("2026-03-20", 75), ("2026-04-15", 38), ("2026-05-15", 17)
        };
        foreach (var (date, quantity) in births)
            database.AnimalBirths.Add(new AnimalBirth
            {
                LivestockFarmId = farm.Id, BirthDate = DateOnly.Parse(date), OffspringNumber = quantity
            });
        await database.SaveChangesAsync();
        foreach (var (date, quantity) in new[]
        {
            ("2026-02-03", 100), ("2026-03-04", 46), ("2026-06-04", 75),
            ("2026-07-01", 38), ("2026-07-29", 17)
        })
            database.MovementCertificates.Add(new MovementCertificate
            {
                OriginLivestockId = farm.Id, Specie = species.ToString(), NumberOfAnimals = quantity,
                DepartureDate = DateTime.SpecifyKind(DateOnly.Parse(date).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                UnidentifiedCategory = MovementUnidentifiedCategory.Under4Months,
                BirthLots = [new MovementBirthLot
                {
                    BirthId = database.AnimalBirths.Single(birth => birth.OffspringNumber == quantity).Id,
                    Quantity = quantity
                }]
            });
        for (var i = 0; i < 506; i++)
            database.Animals.Add(new Animal
            {
                LivestockFarmId = farm.Id, Identification = $"SYNTHETIC-{i}",
                Sex = i < 491 ? "Female" : "Male",
                BirthDate = i < 44 ? new DateOnly(2025, 8, 15) : i >= 499 ? new DateOnly(2025, 12, 15) : new DateOnly(2024, 1, 1),
                RegistrationDate = new DateOnly(2025, 12, 30),
                RegistrationCause = i < 44 ? AnimalRegistrationCause.Autorreposicion : AnimalRegistrationCause.Entrada
            });
        await database.SaveChangesAsync();
        var service = new FarmCensusProjectionService(database,
            new TestClock(new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero)));

        var snapshot = await service.BuildSnapshotAsync(farm, new DateOnly(2026, 10, 4), default);

        snapshot.Total.Should().Be(617);
        snapshot.NonReproductiveUnder4Months.Should().Be(0);
        snapshot.NonReproductiveBetween4And12Months.Should().Be(118);
        snapshot.ReproductiveMales.Should().Be(8);
        snapshot.ReproductiveFemales.Should().Be(491);
        var censuses = await service.BuildBookCensusesAsync(farm, default);
        censuses.Should().OnlyContain(census => census.CensusDate.Month == 1 && census.CensusDate.Day == 1);
    }

    [Theory]
    [InlineData(LivestockSpecies.Ovine)]
    [InlineData(LivestockSpecies.Caprine)]
    public async Task SoldBirths_DoNotReappearOrConsumeLaterBirths(LivestockSpecies species)
    {
        await using var database = ServiceTestDbFactory.CreateContext();
        var farm = ServiceTestData.CreateFarm(1, 1, species, "Synthetic cohort", "ES000000000001");
        database.Farms.Add(farm);
        database.AnimalBirths.Add(new AnimalBirth
        {
            Id = 101, LivestockFarmId = farm.Id, BirthDate = new DateOnly(2026, 1, 1), OffspringNumber = 10
        });
        database.MovementCertificates.Add(new MovementCertificate
        {
            OriginLivestockId = farm.Id, Specie = species.ToString(), NumberOfAnimals = 10,
            DepartureDate = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc),
            UnidentifiedCategory = MovementUnidentifiedCategory.Under4Months,
            BirthLots = [new MovementBirthLot { BirthId = 101, Quantity = 10 }]
        });
        database.AnimalBirths.Add(new AnimalBirth
        {
            LivestockFarmId = farm.Id, BirthDate = new DateOnly(2026, 5, 2), OffspringNumber = 3
        });
        await database.SaveChangesAsync();
        var service = new FarmCensusProjectionService(database,
            new TestClock(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        foreach (var (date, expected) in new[]
        {
            (new DateOnly(2026, 2, 1), 0), (new DateOnly(2026, 5, 1), 0),
            (new DateOnly(2026, 5, 2), 3), (new DateOnly(2027, 1, 1), 3)
        })
        {
            var snapshot = await service.BuildSnapshotAsync(farm, date, default);
            snapshot.Total.Should().Be(expected, $"stock must be conserved at {date}");
            var census = await service.BuildCensusResponseAsync(farm, date.Year, date, default);
            census.Total.Should().Be(expected);
        }

        var book = await service.BuildBookCensusesAsync(farm, default);
        var annual = book.Single(census => census.CensusDate == new DateOnly(2027, 1, 1));
        annual.OvinoCaprino!.NonReproductiveBetween4And12Months.Should().Be(3);
    }
}
