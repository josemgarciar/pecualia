using FluentAssertions;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Pecualia.Test.Services;

public sealed class BookDocumentComposerTests
{
    static BookDocumentComposerTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void BuildSections_ReturnsExpectedSectionMetadata()
    {
        var aggregate = CreateAggregate(LivestockSpecies.Porcine);

        var sections = BookDocumentComposer.BuildSections(aggregate);

        sections.Should().HaveCount(6);
        sections.Select(section => section.Id).Should().ContainInOrder("general", "animals", "balance", "census", "incidents", "inspections");
        sections.Single(section => section.Id == "animals").Items.Should().Be(aggregate.Animals.Count);
        sections.Single(section => section.Id == "animals").Title.Should().Be("Animales individuales");
    }

    [Fact]
    public void ResolveIncludedSections_ReturnsDefaults_WhenInputIsNull()
    {
        var includedSections = BookDocumentComposer.ResolveIncludedSections(null);

        includedSections.Should().BeEquivalentTo(["general", "animals", "balance", "census", "incidents", "inspections"]);
    }

    [Fact]
    public void ResolveIncludedSections_FiltersUnknownValues_AndTrimsKnownOnes()
    {
        var includedSections = BookDocumentComposer.ResolveIncludedSections([" general ", "INSPECTIONS", "unknown", ""]);

        includedSections.Should().HaveCount(2);
        includedSections.Should().Contain(section => string.Equals(section, "general", StringComparison.OrdinalIgnoreCase));
        includedSections.Should().Contain(section => string.Equals(section, "inspections", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResolveIncludedSections_Throws_WhenNoKnownSectionRemains()
    {
        var action = () => BookDocumentComposer.ResolveIncludedSections(["unknown", "   "]);

        action.Should().Throw<DomainException>()
            .WithMessage("Debes seleccionar al menos un apartado del libro.");
    }

    [Fact]
    public void ComposeDocument_GeneratesPdf_ForInspectionSection()
    {
        var aggregate = CreateAggregate(LivestockSpecies.Ovine);

        var pdf = Document.Create(container =>
            BookDocumentComposer.ComposeDocument(container, aggregate, new HashSet<string>(["inspections"], StringComparer.OrdinalIgnoreCase)))
            .GeneratePdf();

        pdf.Should().NotBeEmpty();
    }

    private static BookAggregate CreateAggregate(LivestockSpecies species)
    {
        var user = ServiceTestData.CreateUser(1, UserRole.Farmer, "Lucia", "Romero", email: "lucia@test.local");
        var farmer = ServiceTestData.CreateFarmer(1, user, nifCif: "12345678Z");
        var farm = ServiceTestData.CreateFarm(10, farmer.UserId, species, "Finca libro", "ES410010008888");
        farm.Farmer = farmer;
        var animal = ServiceTestData.CreateAnimal(100, farm.Id, "ES123", new DateOnly(2026, 01, 10), birthYear: 2025);
        var inspection = new Inspection
        {
            Id = 1,
            LivestockFarmId = farm.Id,
            InspectionDate = new DateOnly(2026, 05, 10),
            Reason = "CC",
            Observations = "Sin incidencias",
            Veterinary = "Dr. Test"
        };

        return new BookAggregate(
            farm,
            [animal],
            [],
            [],
            [],
            [inspection],
            [],
            new Dictionary<long, BookAnimalGuideSeries>());
    }
}
