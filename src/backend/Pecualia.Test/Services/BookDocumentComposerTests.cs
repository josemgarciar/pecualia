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

    [Theory]
    [InlineData(LivestockSpecies.Ovine)]
    [InlineData(LivestockSpecies.Caprine)]
    [InlineData(LivestockSpecies.Porcine)]
    public void ComposeDocument_GeneratesPdf_ForAllSections(LivestockSpecies species)
    {
        var aggregate = CreateRichAggregate(species);

        var pdf = Document.Create(container =>
            BookDocumentComposer.ComposeDocument(
                container,
                aggregate,
                new HashSet<string>(["general", "animals", "balance", "census", "incidents", "inspections"], StringComparer.OrdinalIgnoreCase)))
            .GeneratePdf();

        pdf.Should().NotBeEmpty();
        pdf.Length.Should().BeGreaterThan(5_000);
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

    private static BookAggregate CreateRichAggregate(LivestockSpecies species)
    {
        var user = ServiceTestData.CreateUser(21, UserRole.Farmer, "Laura", "Libro", email: "laura@test.local");
        var farmer = ServiceTestData.CreateFarmer(21, user, nifCif: "87654321X");
        var farm = ServiceTestData.CreateFarm(210, farmer.UserId, species, $"Libro {species}", "ES410010009999", authorisedCapacity: 120, porcineMothersCapacity: 40, porcineFatteningCapacity: 80);
        farm.Farmer = farmer;
        farm.Address = "Camino Real, 1";
        farm.ZipCode = "41001";
        farm.Responsible = "Laura Libro";
        farm.ZootechnicClassification = species == LivestockSpecies.Porcine ? "Producción" : "Reproducción";
        farm.PorcineRegistryNumber = species == LivestockSpecies.Porcine ? "PR-210" : string.Empty;
        farm.XCoordinate = 123456.78;
        farm.YCoordinate = 987654.32;

        var firstAnimal = ServiceTestData.CreateAnimal(
            301,
            farm.Id,
            species == LivestockSpecies.Porcine ? "GT1800001301" : "ES123456789012",
            new DateOnly(2026, 01, 10),
            birthDate: new DateOnly(2025, 10, 1),
            birthYear: 2025,
            registrationCause: AnimalRegistrationCause.Entrada,
            sex: "H");
        firstAnimal.Breed = species switch
        {
            LivestockSpecies.Porcine => "Ibérico",
            LivestockSpecies.Caprine => "Murciano-granadina",
            _ => "Merina"
        };
        firstAnimal.OriginCode = "ES410010000001";

        var secondAnimal = ServiceTestData.CreateAnimal(
            302,
            farm.Id,
            species == LivestockSpecies.Porcine ? "GT1800001302" : "ES123456789013",
            new DateOnly(2026, 02, 15),
            birthDate: new DateOnly(2025, 11, 5),
            birthYear: 2025,
            registrationCause: AnimalRegistrationCause.Autorreposicion,
            sex: "M");
        secondAnimal.Breed = firstAnimal.Breed;
        secondAnimal.DischargeDate = new DateOnly(2026, 04, 20);
        secondAnimal.DischargeCause = AnimalDischargeCause.Salida;
        secondAnimal.DestinationCode = "ES410010000777";

        if (species == LivestockSpecies.Porcine)
        {
            firstAnimal.Porcino = new PorcinoAnimal
            {
                AnimalId = firstAnimal.Id,
                AnimalType = "Hembras reposición",
                IdentificationDate = new DateOnly(2026, 01, 10),
                PigRegistrationNumber = "PR-IND-1",
                Tag = "M1"
            };
            secondAnimal.Porcino = new PorcinoAnimal
            {
                AnimalId = secondAnimal.Id,
                AnimalType = "Cebo",
                IdentificationDate = new DateOnly(2026, 02, 15),
                Tag = "M2"
            };
        }
        else
        {
            firstAnimal.OvinoCaprino = new OvinoCaprinoAnimal
            {
                AnimalId = firstAnimal.Id,
                SpeciesType = species,
                Genotyping = "ARR/ARQ",
                DominantAllele = "ARR",
                LowAllele = "ARQ"
            };
            secondAnimal.OvinoCaprino = new OvinoCaprinoAnimal
            {
                AnimalId = secondAnimal.Id,
                SpeciesType = species,
                Genotyping = "ARQ/ARQ",
                DominantAllele = "ARQ",
                LowAllele = "ARQ"
            };
        }

        var balances = species == LivestockSpecies.Porcine
            ? new List<Balance>
            {
                new()
                {
                    Id = 410,
                    LivestockFarmId = farm.Id,
                    BalanceDate = new DateOnly(2026, 01, 10),
                    ModificationCause = "Nacimiento",
                    NumberOfAnimals = 4,
                    DestinationLivestockCode = farm.RegaCode,
                    OriginLivestockCode = "APERTURA",
                    Porcino = new BalancePorcino
                    {
                        BalanceId = 410,
                        Type = "Lechones",
                        Breed = "Ibérico",
                        Tag = "M1",
                        Piglets = 4
                    }
                },
                new()
                {
                    Id = 411,
                    LivestockFarmId = farm.Id,
                    BalanceDate = new DateOnly(2026, 04, 20),
                    ModificationCause = AnimalDischargeCause.Salida.ToString(),
                    NumberOfAnimals = 1,
                    DestinationLivestockCode = "ES410010000777",
                    OriginLivestockCode = farm.RegaCode,
                    Porcino = new BalancePorcino
                    {
                        BalanceId = 411,
                        Type = "Cebo",
                        Breed = "Ibérico",
                        Tag = "M2",
                        Baits = 1
                    }
                }
            }
            : new List<Balance>
            {
                new()
                {
                    Id = 420,
                    LivestockFarmId = farm.Id,
                    BalanceDate = new DateOnly(2026, 01, 10),
                    ModificationCause = AnimalRegistrationCause.Entrada.ToString(),
                    NumberOfAnimals = 2,
                    DestinationLivestockCode = farm.RegaCode,
                    OriginLivestockCode = "ES410010000001",
                    OvinoCaprino = new BalanceOvinoCaprino
                    {
                        BalanceId = 420,
                        NonReproductiveUnder4Months = 2,
                        TransporterName = "Transportes Sierra",
                        TransportTicketNumber = "1234-BCD"
                    }
                },
                new()
                {
                    Id = 421,
                    LivestockFarmId = farm.Id,
                    BalanceDate = new DateOnly(2026, 04, 20),
                    ModificationCause = AnimalDischargeCause.Salida.ToString(),
                    NumberOfAnimals = 1,
                    DestinationLivestockCode = "ES410010000777",
                    OriginLivestockCode = farm.RegaCode,
                    OvinoCaprino = new BalanceOvinoCaprino
                    {
                        BalanceId = 421,
                        NonReproductiveBetween4And12Months = 1,
                        ReproductiveFemales = species == LivestockSpecies.Caprine ? 1 : 0,
                        ReproductiveMales = species == LivestockSpecies.Ovine ? 1 : 0,
                        TransporterName = "Transportes Sierra",
                        TransportTicketNumber = "9876-ZYX"
                    }
                }
            };

        var censuses = species == LivestockSpecies.Porcine
            ? new List<Census>
            {
                new()
                {
                    Id = 510,
                    LivestockFarmId = farm.Id,
                    CensusDate = new DateOnly(2026, 01, 01),
                    Porcino = new CensusPorcino
                    {
                        CensusId = 510,
                        Boars = 1,
                        Sow = 8,
                        PigsReposition = 3,
                        SowsReposition = 4,
                        Piglets = 12,
                        Rears = 6,
                        Baits = 10
                    }
                }
            }
            : new List<Census>
            {
                new()
                {
                    Id = 520,
                    LivestockFarmId = farm.Id,
                    CensusDate = new DateOnly(2026, 01, 01),
                    OvinoCaprino = new CensusOvinoCaprino
                    {
                        CensusId = 520,
                        NonReproductiveUnder4Months = 3,
                        NonReproductiveBetween4And12Months = 2,
                        ReproductiveMale = 1,
                        ReproductiveFemale = 5
                    }
                }
            };

        var incidents = new List<Incident>
        {
            new()
            {
                Id = 610,
                LivestockFarmId = farm.Id,
                AnimalId = firstAnimal.Id,
                Animal = firstAnimal,
                IncidentDate = new DateOnly(2026, 03, 5),
                ChangeReason = "Pérdida",
                Description = "Sustitución de crotal",
                LastIdentification = firstAnimal.Identification,
                NewIdentification = $"{firstAnimal.Identification}-N"
            }
        };

        var inspections = new List<Inspection>
        {
            new()
            {
                Id = 710,
                LivestockFarmId = farm.Id,
                InspectionDate = new DateOnly(2026, 05, 10),
                Reason = "CC",
                Observations = "Sin incidencias",
                Veterinary = "Dr. Test",
                LivestockFarm = farm
            }
        };

        var movements = new List<MovementCertificate>
        {
            new()
            {
                Id = 810,
                CodRemo = "REMO-BOOK-1",
                Serie = "SER-01",
                Specie = species.ToString(),
                NumberOfAnimals = 1,
                DepartureDate = new DateTime(2026, 04, 20, 0, 0, 0, DateTimeKind.Utc),
                ArrivalDate = new DateTime(2026, 04, 20, 0, 0, 0, DateTimeKind.Utc),
                OriginLivestockId = farm.Id,
                OriginFarm = farm,
                DestinationLivestockId = null,
                DestinationFarm = null,
                Status = MovementStatus.Confirmed
            }
        };

        return new BookAggregate(
            farm,
            [firstAnimal, secondAnimal],
            balances,
            censuses,
            incidents,
            inspections,
            movements,
            new Dictionary<long, BookAnimalGuideSeries>
            {
                [firstAnimal.Id] = new("ENT-01", "SAL-01"),
                [secondAnimal.Id] = new("ENT-02", "SAL-02")
            });
    }
}
