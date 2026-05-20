using FluentAssertions;
using Pecualia.Api.Contracts.Books;
using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;
using QuestPDF.Infrastructure;

namespace Pecualia.Test.Services;

public sealed class BookServiceTests
{
    static BookServiceTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public async Task GetPreviewAsync_ReturnsPorcinePreview_ForAccessibleFarm()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var user = ServiceTestData.CreateUser(10, UserRole.Farmer, "Alba", "Porcina", email: "alba@test.local");
        var farmer = ServiceTestData.CreateFarmer(10, user, nifCif: "11111111H");
        var farm = ServiceTestData.CreateFarm(100, farmer.UserId, LivestockSpecies.Porcine, "Porcina Libro", "ES410010007001");
        var animal = ServiceTestData.CreateAnimal(1000, farm.Id, "ES0001", new DateOnly(2026, 01, 15), birthYear: 2025);
        var inspection = new Inspection
        {
            Id = 1,
            LivestockFarmId = farm.Id,
            InspectionDate = new DateOnly(2026, 05, 01),
            Reason = "CC",
            Veterinary = "Dr. Vet"
        };
        var census = new Census
        {
            Id = 3000,
            LivestockFarmId = farm.Id,
            CensusDate = new DateOnly(2026, 05, 15),
            Porcino = new CensusPorcino
            {
                CensusId = 3000,
                Piglets = 1
            }
        };

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        dbContext.Animals.Add(animal);
        dbContext.Inspections.Add(inspection);
        await dbContext.SaveChangesAsync();

        var service = new BookService(dbContext, new StubCensusProjectionService(censuses: [census]));

        var preview = await service.GetPreviewAsync(user.Id, UserRole.Farmer, farm.Id, CancellationToken.None);

        preview.Should().BeEquivalentTo(new FarmBookPreviewResponse(
            farm.Id,
            farm.Name,
            farm.RegaCode,
            LivestockSpecies.Porcine.ToString(),
            "official-porcino",
            new FarmBookPreviewSummaryResponse(
                "Alba Porcina",
                farmer.NifCif,
                farm.Town,
                farm.Province,
                1,
                0,
                1,
                0,
                1),
            preview.Sections));
        preview.Sections.Should().HaveCount(6);
    }

    [Fact]
    public async Task GeneratePdfAsync_ReturnsPdf_ForRequestedSections()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var user = ServiceTestData.CreateUser(11, UserRole.Farmer, "Luis", "Ovino", email: "luis@test.local");
        var farmer = ServiceTestData.CreateFarmer(11, user, nifCif: "22222222J");
        var farm = ServiceTestData.CreateFarm(101, farmer.UserId, LivestockSpecies.Ovine, "Ovina Libro", "ES410010007002");

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();

        var service = new BookService(dbContext, new StubCensusProjectionService());

        var pdf = await service.GeneratePdfAsync(user.Id, UserRole.Farmer, farm.Id, ["general"], CancellationToken.None);

        pdf.FileName.Should().Be("libro-registro-es410010007002.pdf");
        pdf.ContentType.Should().Be("application/pdf");
        pdf.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetPreviewAsync_Throws_WhenFarmIsNotAccessible()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var ownerUser = ServiceTestData.CreateUser(20, UserRole.Farmer, "Marta", "Dueña", email: "marta@test.local");
        var owner = ServiceTestData.CreateFarmer(20, ownerUser, nifCif: "33333333K");
        var otherUser = ServiceTestData.CreateUser(21, UserRole.Farmer, "Pablo", "Ajeno", email: "pablo@test.local");
        var otherFarmer = ServiceTestData.CreateFarmer(21, otherUser, nifCif: "44444444L");
        var farm = ServiceTestData.CreateFarm(102, owner.UserId, LivestockSpecies.Caprine, "Caprina Libro", "ES410010007003");

        dbContext.Users.AddRange(ownerUser, otherUser);
        dbContext.Farmers.AddRange(owner, otherFarmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();

        var service = new BookService(dbContext, new StubCensusProjectionService());
        var action = () => service.GetPreviewAsync(otherUser.Id, UserRole.Farmer, farm.Id, CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Explotación no encontrada.");
    }

    [Fact]
    public async Task GeneratePdfAsync_LoadsOvineBalancesMovementsAndIncidents()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var user = ServiceTestData.CreateUser(30, UserRole.Farmer, "Lola", "Ovina", email: "lola@test.local");
        var farmer = ServiceTestData.CreateFarmer(30, user, nifCif: "55555555P");
        var farm = ServiceTestData.CreateFarm(130, farmer.UserId, LivestockSpecies.Ovine, "Ovina Completa", "ES410010007030");
        var firstAnimal = ServiceTestData.CreateAnimal(3000, farm.Id, "ES123456789301", new DateOnly(2026, 1, 10), birthDate: new DateOnly(2025, 11, 1), birthYear: 2025, registrationCause: AnimalRegistrationCause.Entrada, sex: "H");
        var secondAnimal = ServiceTestData.CreateAnimal(3001, farm.Id, "ES123456789302", new DateOnly(2026, 2, 10), birthDate: new DateOnly(2025, 8, 1), birthYear: 2025, registrationCause: AnimalRegistrationCause.Autorreposicion, sex: "M");
        secondAnimal.DischargeDate = new DateOnly(2026, 4, 20);
        secondAnimal.DischargeCause = AnimalDischargeCause.Muerte;
        secondAnimal.DestinationCode = "MER-26-001";
        var entryBalance = new Balance
        {
            Id = 4000,
            LivestockFarmId = farm.Id,
            BalanceDate = new DateOnly(2026, 1, 10),
            ModificationCause = AnimalRegistrationCause.Entrada.ToString(),
            NumberOfAnimals = 1,
            OriginLivestockCode = "ES410010000001",
            DestinationLivestockCode = farm.RegaCode
        };
        var deathBalance = new Balance
        {
            Id = 4001,
            LivestockFarmId = farm.Id,
            BalanceDate = new DateOnly(2026, 4, 20),
            ModificationCause = AnimalDischargeCause.Muerte.ToString(),
            NumberOfAnimals = 1,
            OriginLivestockCode = farm.RegaCode,
            DestinationLivestockCode = "MER-26-001"
        };
        var movement = new MovementCertificate
        {
            Id = 5000,
            OriginLivestockId = farm.Id,
            CodRemo = "REMO-BOOK-OV",
            Serie = "SER-OV",
            DepartureDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            ArrivalDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            NumberOfAnimals = 1,
            Specie = LivestockSpecies.Ovine.ToString(),
            Status = MovementStatus.Confirmed
        };
        var incident = new Incident
        {
            Id = 6000,
            LivestockFarmId = farm.Id,
            AnimalId = firstAnimal.Id,
            IncidentDate = new DateOnly(2026, 3, 1),
            Description = "Cambio de crotal",
            LastIdentification = firstAnimal.Identification,
            NewIdentification = "ES123456789399"
        };

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        dbContext.Animals.AddRange(firstAnimal, secondAnimal);
        dbContext.Balances.AddRange(entryBalance, deathBalance);
        dbContext.BalanceOvinoCaprino.AddRange(
            new BalanceOvinoCaprino
            {
                BalanceId = entryBalance.Id,
                NonReproductiveUnder4Months = 1,
                TransporterName = "Transporte Uno",
                TransportTicketNumber = "ABC123"
            },
            new BalanceOvinoCaprino
            {
                BalanceId = deathBalance.Id,
                NonReproductiveBetween4And12Months = 1
            });
        dbContext.MovementCertificates.Add(movement);
        dbContext.MovementCertificateAnimals.Add(new MovementCertificateAnimal
        {
            MovementCertificateId = movement.Id,
            AnimalId = secondAnimal.Id
        });
        dbContext.Incidents.Add(incident);
        await dbContext.SaveChangesAsync();

        var service = new BookService(dbContext, new StubCensusProjectionService(
            snapshot: new FarmCensusResponse(null, farm.Id, 2026, LivestockSpecies.Ovine.ToString(), 2, 1, 3, 1, 0, 0, 0, 0, 0, 0, 0, 0, 7, [2026]),
            censuses:
            [
                new Census
                {
                    Id = 7000,
                    LivestockFarmId = farm.Id,
                    CensusDate = new DateOnly(2026, 1, 1),
                    OvinoCaprino = new CensusOvinoCaprino
                    {
                        CensusId = 7000,
                        NonReproductiveUnder4Months = 2,
                        NonReproductiveBetween4And12Months = 1,
                        ReproductiveFemale = 3,
                        ReproductiveMale = 1
                    }
                }
            ]));

        var pdf = await service.GeneratePdfAsync(user.Id, UserRole.Farmer, farm.Id, ["general", "animals", "balance", "census", "incidents"], CancellationToken.None);

        pdf.Content.Should().NotBeEmpty();
        pdf.FileName.Should().Be("libro-registro-es410010007030.pdf");
    }

    [Fact]
    public async Task GetPreviewAsync_LoadsPorcineBalancesAndCensuses()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var user = ServiceTestData.CreateUser(31, UserRole.Farmer, "Pablo", "Porcino", email: "pablo@test.local");
        var farmer = ServiceTestData.CreateFarmer(31, user, nifCif: "66666666Q");
        var farm = ServiceTestData.CreateFarm(131, farmer.UserId, LivestockSpecies.Porcine, "Porcina Completa", "ES410010007031", authorisedCapacity: 20, porcineMothersCapacity: 8, porcineFatteningCapacity: 12);
        var animal = ServiceTestData.CreateAnimal(3100, farm.Id, "GT1800003100", new DateOnly(2026, 1, 10), birthDate: new DateOnly(2025, 12, 1), birthYear: 2025, registrationCause: AnimalRegistrationCause.Entrada, sex: "H");
        var balance = new Balance
        {
            Id = 4100,
            LivestockFarmId = farm.Id,
            BalanceDate = new DateOnly(2026, 2, 1),
            ModificationCause = AnimalRegistrationCause.Autorreposicion.ToString(),
            NumberOfAnimals = 2
        };

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        dbContext.Animals.Add(animal);
        dbContext.Balances.Add(balance);
        dbContext.BalancePorcino.Add(new BalancePorcino
        {
            BalanceId = balance.Id,
            Piglets = 2,
            Type = "Reclasificación porcina",
            Breed = "Ibérico",
            Tag = "M1"
        });
        await dbContext.SaveChangesAsync();

        var service = new BookService(dbContext, new StubCensusProjectionService(
            snapshot: new FarmCensusResponse(null, farm.Id, 2026, LivestockSpecies.Porcine.ToString(), 0, 0, 0, 0, 1, 3, 2, 1, 5, 4, 6, 0, 22, [2026]),
            censuses:
            [
                new Census
                {
                    Id = 7100,
                    LivestockFarmId = farm.Id,
                    CensusDate = new DateOnly(2026, 1, 1),
                    Porcino = new CensusPorcino
                    {
                        CensusId = 7100,
                        Boars = 1,
                        Sow = 3,
                        PigsReposition = 2,
                        SowsReposition = 1,
                        Piglets = 5,
                        Rears = 4,
                        Baits = 6
                    }
                }
            ]));

        var preview = await service.GetPreviewAsync(user.Id, UserRole.Farmer, farm.Id, CancellationToken.None);

        preview.LivestockSpecies.Should().Be(LivestockSpecies.Porcine.ToString());
        preview.Sections.Should().HaveCount(6);
        preview.Summary.Animals.Should().Be(1);
        preview.Summary.Balances.Should().Be(1);
        preview.Summary.Censuses.Should().Be(1);
    }

    private sealed class StubCensusProjectionService(
        FarmCensusResponse? snapshot = null,
        IReadOnlyList<Census>? censuses = null) : IFarmCensusProjectionService
    {
        private readonly FarmCensusResponse snapshot = snapshot ?? new FarmCensusResponse(
            null,
            0,
            2026,
            LivestockSpecies.Ovine.ToString(),
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            []);

        private readonly IReadOnlyList<Census> censuses = censuses ?? [];

        public Task<FarmCensusResponse> BuildSnapshotAsync(LivestockFarm farm, DateOnly asOfDate, CancellationToken cancellationToken)
        {
            return Task.FromResult(snapshot);
        }

        public Task<FarmCensusResponse> BuildCensusResponseAsync(LivestockFarm farm, int year, DateOnly asOfDate, CancellationToken cancellationToken)
        {
            return Task.FromResult(snapshot);
        }

        public Task<IReadOnlyList<int>> GetAvailableYearsAsync(long farmId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<int>>([]);
        }

        public Task<IReadOnlyList<Census>> BuildBookCensusesAsync(LivestockFarm farm, CancellationToken cancellationToken)
        {
            return Task.FromResult(censuses);
        }
    }
}
