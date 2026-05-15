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
