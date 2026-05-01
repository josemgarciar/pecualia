using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Books;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pecualia.Api.Services;

public interface IBookService
{
    Task<FarmBookPreviewResponse> GetPreviewAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);

    Task<FarmBookPdfFile> GeneratePdfAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);
}

public sealed record FarmBookPdfFile(string FileName, byte[] Content, string ContentType);

public sealed class BookService(PecualiaDbContext dbContext) : IBookService
{
    private static readonly IReadOnlyDictionary<string, string> OvineBreedCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Merina del país"] = "M",
        ["Merina"] = "M",
        ["Aragonesa"] = "A",
        ["Castellana"] = "C",
        ["Churra"] = "CH",
        ["Segureña"] = "S",
        ["Manchega"] = "MA",
        ["Lacha"] = "L",
        ["Talaverana"] = "T",
        ["Colmenareña"] = "CO",
        ["Merina precoz"] = "MP",
        ["Fleischschaf"] = "F",
        ["Landschaf"] = "LA",
        ["Awassi"] = "AW",
        ["Romanof"] = "R",
        ["Ile de France"] = "I",
        ["Berrichon du cher"] = "B",
        ["Charmoise"] = "CM",
        ["Lacaune"] = "LA",
        ["Assaf"] = "AS",
        ["Cruzada"] = "CR"
    };

    private static readonly IReadOnlyDictionary<string, string> CaprineBreedCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Verata"] = "V",
        ["Serrana"] = "S",
        ["Retinta extremeña"] = "R",
        ["Blanca Celtibérica"] = "B",
        ["Murciano-granadina"] = "M",
        ["Malagueña"] = "MA",
        ["Andaluza"] = "A",
        ["Canaria"] = "C",
        ["Saanen"] = "SA",
        ["Cruzada"] = "CR"
    };

    private static readonly IReadOnlyDictionary<string, string> PorcineBreedCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Ibérico"] = "IB",
        ["Duroc"] = "D",
        ["Duroc Jersey"] = "DJ",
        ["Landrace"] = "L",
        ["Large White"] = "LW",
        ["Large Black"] = "LB",
        ["Pietrain"] = "P"
    };

    public async Task<FarmBookPreviewResponse> GetPreviewAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        var aggregate = await LoadAggregateAsync(userId, role, farmId, cancellationToken);

        return new FarmBookPreviewResponse(
            aggregate.Farm.Id,
            aggregate.Farm.Name,
            aggregate.Farm.RegaCode,
            aggregate.Farm.LivestockSpecies.ToString(),
            IsOvineOrCaprine(aggregate.Farm) ? "official-ovino-caprino" : "official-porcino",
            new FarmBookPreviewSummaryResponse(
                BuildFarmerName(aggregate.Farm.Farmer),
                aggregate.Farm.Farmer.NifCif,
                EmptyToNull(aggregate.Farm.Town),
                EmptyToNull(aggregate.Farm.Province),
                aggregate.Animals.Count,
                aggregate.Balances.Count,
                aggregate.Censuses.Count,
                aggregate.Incidents.Count,
                aggregate.Inspections.Count),
            BuildSections(aggregate));
    }

    public async Task<FarmBookPdfFile> GeneratePdfAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        var aggregate = await LoadAggregateAsync(userId, role, farmId, cancellationToken);
        var content = Document.Create(container => ComposeDocument(container, aggregate)).GeneratePdf();
        var fileName = $"libro-registro-{aggregate.Farm.RegaCode.ToLowerInvariant()}.pdf";
        return new FarmBookPdfFile(fileName, content, "application/pdf");
    }

    private async Task<BookAggregate> LoadAggregateAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        var farm = await BuildAccessibleFarmQuery(userId, role)
            .AsNoTracking()
            .Include(entity => entity.Farmer)
            .ThenInclude(entity => entity.User)
            .SingleOrDefaultAsync(entity => entity.Id == farmId, cancellationToken);

        if (farm is null)
        {
            throw new DomainException("Explotación no encontrada.");
        }

        var animals = await dbContext.Animals
            .AsNoTracking()
            .Include(entity => entity.OvinoCaprino)
            .Include(entity => entity.Porcino)
            .Where(entity => entity.LivestockFarmId == farm.Id)
            .OrderBy(entity => entity.RegistrationDate)
            .ThenBy(entity => entity.Identification)
            .ToListAsync(cancellationToken);

        var balances = await dbContext.Balances
            .AsNoTracking()
            .Include(entity => entity.OvinoCaprino)
            .Include(entity => entity.Porcino)
            .Where(entity => entity.LivestockFarmId == farm.Id)
            .OrderBy(entity => entity.BalanceDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        var censuses = await dbContext.Census
            .AsNoTracking()
            .Include(entity => entity.OvinoCaprino)
            .Include(entity => entity.Porcino)
            .Where(entity => entity.LivestockFarmId == farm.Id)
            .OrderBy(entity => entity.CensusDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .Include(entity => entity.Animal)
            .Where(entity => entity.LivestockFarmId == farm.Id)
            .OrderBy(entity => entity.IncidentDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        var inspections = await dbContext.Inspections
            .AsNoTracking()
            .Where(entity => entity.LivestockFarmId == farm.Id)
            .OrderBy(entity => entity.InspectionDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        var movements = await dbContext.MovementCertificates
            .AsNoTracking()
            .Where(entity => entity.OriginLivestockId == farm.Id || entity.DestinationLivestockId == farm.Id)
            .OrderBy(entity => entity.DepartureDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        return new BookAggregate(farm, animals, balances, censuses, incidents, inspections, movements);
    }

    private IQueryable<LivestockFarm> BuildAccessibleFarmQuery(long userId, UserRole role)
    {
        return role == UserRole.Manager
            ? dbContext.Farms.Where(entity => entity.Farmer.ManagerId == userId)
            : dbContext.Farms.Where(entity => entity.FarmerId == userId);
    }

    private static IReadOnlyList<FarmBookPreviewSectionResponse> BuildSections(BookAggregate aggregate)
    {
        return
        [
            new("general", "Información general de la explotación", 1, "Datos del titular y de la explotación con formato oficial."),
            new("animals", IsOvineOrCaprine(aggregate.Farm) ? "Animales individuales" : "Animales colectivos", IsOvineOrCaprine(aggregate.Farm) ? aggregate.Animals.Count : aggregate.Balances.Count, IsOvineOrCaprine(aggregate.Farm) ? "Hoja de identificación individual del ganado." : "Relación colectiva basada en movimientos y balances."),
            new("balance", "Información sobre balance", aggregate.Balances.Count, "Histórico de actualizaciones del censo y balance oficial."),
            new("census", "Información sobre censo", aggregate.Censuses.Count, "Declaraciones de censo imprimibles por especie."),
            new("incidents", "Anexo de incidencias", aggregate.Incidents.Count, "Incidencias de identificación del ganado."),
            new("inspections", "Control de inspecciones", aggregate.Inspections.Count, "Histórico de inspecciones oficiales.")
        ];
    }

    private static void ComposeDocument(IDocumentContainer container, BookAggregate aggregate)
    {
        ComposeGeneralPage(container, aggregate);

        if (IsOvineOrCaprine(aggregate.Farm))
        {
            ComposeOvineAnimalsSection(container, aggregate);
        }
        else
        {
            ComposePorcineCollectiveSection(container, aggregate);
        }

        ComposeBalanceSection(container, aggregate);
        ComposeCensusSection(container, aggregate);
        ComposeIncidentSection(container, aggregate);
        ComposeInspectionSection(container, aggregate);
    }

    private static void ComposeGeneralPage(IDocumentContainer container, BookAggregate aggregate)
    {
        container.Page(page =>
        {
            ConfigurePage(page, aggregate, "Datos generales");

            page.Content().Column(column =>
            {
                column.Spacing(14);
                column.Item().Text(IsOvineOrCaprine(aggregate.Farm)
                    ? "LIBRO DE REGISTRO DE EXPLOTACIÓN OVINO-CAPRINO"
                    : "LIBRO DE REGISTRO DE EXPLOTACIÓN PORCINO").Bold().FontSize(16);

                column.Item().Element(OfficialPanel).Column(content =>
                {
                    content.Spacing(8);
                    content.Item().Text("DATOS DEL TITULAR").Bold();
                    content.Item().Element(body => ComposeFieldTable(body, new (string Label, string? Value)[]
                    {
                        ("Nombre o razón social", BuildFarmerName(aggregate.Farm.Farmer)),
                        ("NIF / CIF", aggregate.Farm.Farmer.NifCif),
                        ("Domicilio", aggregate.Farm.Farmer.Residence),
                        ("Localidad", aggregate.Farm.Farmer.Town),
                        ("Provincia", aggregate.Farm.Farmer.Province),
                        ("Código postal", aggregate.Farm.Farmer.ZipCode),
                        ("Teléfono", aggregate.Farm.Farmer.PhoneNumber),
                        ("Responsable de los animales", aggregate.Farm.Responsible ?? BuildFarmerName(aggregate.Farm.Farmer))
                    }));
                });

                column.Item().Element(OfficialPanel).Column(content =>
                {
                    content.Spacing(8);
                    content.Item().Text("DATOS DE LA EXPLOTACIÓN").Bold();
                    content.Item().Element(body => ComposeFieldTable(body, BuildGeneralFarmFields(aggregate)));
                });

                if (aggregate.Farm.LivestockSpecies == LivestockSpecies.Porcine)
                {
                    var opening = aggregate.Censuses.LastOrDefault()?.Porcino;
                    contentForPorcineOpening(column, opening);
                }
            });
        });
    }

    private static void contentForPorcineOpening(ColumnDescriptor column, CensusPorcino? opening)
    {
        column.Item().Element(OfficialPanel).Column(content =>
        {
            content.Spacing(8);
            content.Item().Text("APERTURA DEL LIBRO (CENSO)").Bold();
            content.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Element(TableHeaderCell).Text("Verracos");
                    header.Cell().Element(TableHeaderCell).Text("Cerdas vida");
                    header.Cell().Element(TableHeaderCell).Text("M. repos.");
                    header.Cell().Element(TableHeaderCell).Text("H. repos.");
                    header.Cell().Element(TableHeaderCell).Text("Lechones");
                    header.Cell().Element(TableHeaderCell).Text("Recría");
                    header.Cell().Element(TableHeaderCell).Text("Cebo");
                });

                table.Cell().Element(TableBodyCell).Text((opening?.Boars ?? 0).ToString());
                table.Cell().Element(TableBodyCell).Text((opening?.Sow ?? 0).ToString());
                table.Cell().Element(TableBodyCell).Text((opening?.PigsReposition ?? 0).ToString());
                table.Cell().Element(TableBodyCell).Text((opening?.SowsReposition ?? 0).ToString());
                table.Cell().Element(TableBodyCell).Text((opening?.Piglets ?? 0).ToString());
                table.Cell().Element(TableBodyCell).Text((opening?.Rears ?? 0).ToString());
                table.Cell().Element(TableBodyCell).Text((opening?.Baits ?? 0).ToString());
            });
        });
    }

    private static void ComposeOvineAnimalsSection(IDocumentContainer container, BookAggregate aggregate)
    {
        var rows = aggregate.Animals
            .Select((animal, index) => new OvineAnimalRow(
                index + 1,
                animal.Identification,
                animal.BirthYear?.ToString(),
                FormatDate(animal.RegistrationDate),
                MapBreedCode(aggregate.Farm.LivestockSpecies, animal.Breed),
                MapSexCode(animal.Sex),
                EmptyToNull(animal.OvinoCaprino?.Genotyping),
                JoinValues(" / ", animal.OvinoCaprino?.DominantAllele, animal.OvinoCaprino?.LowAllele),
                MapRegistrationCauseCode(animal.RegistrationCause),
                FormatDate(animal.RegistrationDate),
                EmptyToNull(animal.OriginCode),
                MapDischargeCauseCode(animal.DischargeCause),
                FormatDate(animal.DischargeDate),
                EmptyToNull(animal.DestinationCode),
                EmptyToNull(animal.HealthDocumentNumber)))
            .ToList();

        foreach (var pageRows in ChunkOrBlank(rows, 20, OvineAnimalRow.Empty))
        {
            container.Page(page =>
            {
                ConfigurePage(page, aggregate, "Hoja de identificación individual");
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text("HOJA DE IDENTIFICACIÓN INDIVIDUAL DEL GANADO OVINO-CAPRINO").Bold().FontSize(14);
                    column.Item().DefaultTextStyle(style => style.FontSize(7)).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(24);
                            columns.RelativeColumn(2.4f);
                            columns.RelativeColumn(0.8f);
                            columns.RelativeColumn(1f);
                            columns.RelativeColumn(0.7f);
                            columns.RelativeColumn(0.6f);
                            columns.RelativeColumn(0.9f);
                            columns.RelativeColumn(0.9f);
                            columns.RelativeColumn(0.7f);
                            columns.RelativeColumn(1f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(0.7f);
                            columns.RelativeColumn(1f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.2f);
                        });

                        table.Header(header =>
                        {
                            AddHeaderCells(header,
                                "Nº",
                                "Identificación",
                                "Año nac.",
                                "Fecha ident.",
                                "Raza",
                                "Sexo",
                                "Genotipado",
                                "Alelos",
                                "Alta",
                                "Fecha alta",
                                "Procedencia",
                                "Baja",
                                "Fecha baja",
                                "Destino",
                                "Doc. sanitario");
                        });

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(TableBodyCell).Text(row.Order.ToString());
                            table.Cell().Element(TableBodyCell).Text(row.Identification);
                            table.Cell().Element(TableBodyCell).Text(row.BirthYear ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.IdentificationDate ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.BreedCode ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.SexCode ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Genotyping ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Alleles ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.RegistrationCause ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.RegistrationDate ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.OriginCode ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.DischargeCause ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.DischargeDate ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.DestinationCode ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.HealthDocumentNumber ?? string.Empty);
                        }
                    });
                });
            });
        }
    }

    private static void ComposePorcineCollectiveSection(IDocumentContainer container, BookAggregate aggregate)
    {
        var rows = aggregate.Balances
            .Select((balance, index) => new PorcineCollectiveRow(
                index + 1,
                FormatDate(balance.BalanceDate),
                balance.NumberOfAnimals.ToString(),
                EmptyToNull(balance.Porcino?.Type),
                MapBreedCode(LivestockSpecies.Porcine, balance.Porcino?.Breed),
                EmptyToNull(balance.Porcino?.Tag),
                MapPorcineCollectiveCauseCode(balance.ModificationCause),
                JoinValues(" / ", balance.OriginLivestockCode, balance.DestinationLivestockCode),
                EmptyToNull(balance.HealthDocumentNumber)))
            .ToList();

        foreach (var pageRows in ChunkOrBlank(rows, 24, PorcineCollectiveRow.Empty))
        {
            container.Page(page =>
            {
                ConfigurePage(page, aggregate, "Registro colectivo porcino");
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text("HOJA DE REGISTRO COLECTIVO DEL GANADO PORCINO").Bold().FontSize(14);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(28);
                            columns.ConstantColumn(55);
                            columns.ConstantColumn(38);
                            columns.ConstantColumn(58);
                            columns.ConstantColumn(38);
                            columns.ConstantColumn(44);
                            columns.ConstantColumn(34);
                            columns.RelativeColumn();
                            columns.ConstantColumn(72);
                        });

                        table.Header(header =>
                        {
                            AddHeaderCells(header,
                                "Nº",
                                "Fecha",
                                "Anim.",
                                "Tipo",
                                "Raza",
                                "Marca",
                                "Causa",
                                "Origen / destino",
                                "Doc. sanitario");
                        });

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(TableBodyCell).Text(row.Order.ToString());
                            table.Cell().Element(TableBodyCell).Text(row.Date ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.NumberOfAnimals ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Type ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.BreedCode ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Tag ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.CauseCode ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Route ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.HealthDocumentNumber ?? string.Empty);
                        }
                    });
                });
            });
        }
    }

    private static void ComposeBalanceSection(IDocumentContainer container, BookAggregate aggregate)
    {
        if (aggregate.Farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            ComposePorcineBalanceSection(container, aggregate);
            return;
        }

        var rows = aggregate.Balances
            .Select((balance, index) => new OvineBalanceRow(
                index + 1,
                FormatDate(balance.BalanceDate),
                balance.NumberOfAnimals.ToString(),
                MapOvineBalanceCause(balance.ModificationCause),
                EmptyToNull(balance.OriginLivestockCode),
                EmptyToNull(balance.DestinationLivestockCode),
                EmptyToNull(balance.HealthDocumentNumber),
                (balance.OvinoCaprino?.NonReproductiveUnder4Months ?? 0).ToString(),
                (balance.OvinoCaprino?.NonReproductiveBetween4And12Months ?? 0).ToString(),
                (balance.OvinoCaprino?.ReproductiveMales ?? 0).ToString(),
                (balance.OvinoCaprino?.ReproductiveFemales ?? 0).ToString()))
            .ToList();

        foreach (var pageRows in ChunkOrBlank(rows, 24, OvineBalanceRow.Empty))
        {
            container.Page(page =>
            {
                ConfigurePage(page, aggregate, "Balance ovino-caprino");
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text("HOJA DE BALANCE DEL GANADO OVINO-CAPRINO").Bold().FontSize(14);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(28);
                            columns.ConstantColumn(55);
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(62);
                            columns.ConstantColumn(62);
                            columns.ConstantColumn(68);
                            columns.ConstantColumn(38);
                            columns.ConstantColumn(38);
                            columns.ConstantColumn(38);
                            columns.ConstantColumn(38);
                        });

                        table.Header(header =>
                        {
                            AddHeaderCells(header,
                                "Nº",
                                "Fecha",
                                "Anim.",
                                "Causa",
                                "Origen",
                                "Destino",
                                "Doc. sanitario",
                                "<4m",
                                "4-12m",
                                "M rep.",
                                "H rep.");
                        });

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(TableBodyCell).Text(row.Order.ToString());
                            table.Cell().Element(TableBodyCell).Text(row.Date ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.NumberOfAnimals ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.CauseCode ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.OriginCode ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.DestinationCode ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.HealthDocumentNumber ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Under4 ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.From4To12 ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.ReproductiveMales ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.ReproductiveFemales ?? string.Empty);
                        }
                    });
                });
            });
        }
    }

    private static void ComposePorcineBalanceSection(IDocumentContainer container, BookAggregate aggregate)
    {
        var rows = aggregate.Balances
            .Select((balance, index) => new PorcineBalanceRow(
                index + 1,
                FormatDate(balance.BalanceDate),
                balance.NumberOfAnimals.ToString(),
                EmptyToNull(balance.Porcino?.Type),
                MapBreedCode(LivestockSpecies.Porcine, balance.Porcino?.Breed),
                EmptyToNull(balance.Porcino?.Tag),
                MapPorcineCollectiveCauseCode(balance.ModificationCause),
                JoinValues(" / ", balance.OriginLivestockCode, balance.DestinationLivestockCode),
                (balance.Porcino?.Boars ?? 0).ToString(),
                (balance.Porcino?.SowsForLive ?? 0).ToString(),
                (balance.Porcino?.SowsReposition ?? 0).ToString(),
                (balance.Porcino?.PigsReposition ?? 0).ToString(),
                (balance.Porcino?.Piglets ?? 0).ToString(),
                (balance.Porcino?.Rear ?? 0).ToString(),
                (balance.Porcino?.Baits ?? 0).ToString(),
                EmptyToNull(balance.HealthDocumentNumber)))
            .ToList();

        foreach (var pageRows in ChunkOrBlank(rows, 18, PorcineBalanceRow.Empty))
        {
            container.Page(page =>
            {
                ConfigurePage(page, aggregate, "Balance porcino");
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text("HOJA DE BALANCE DEL GANADO PORCINO").Bold().FontSize(14);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(24);
                            columns.ConstantColumn(50);
                            columns.ConstantColumn(36);
                            columns.ConstantColumn(38);
                            columns.ConstantColumn(34);
                            columns.ConstantColumn(40);
                            columns.ConstantColumn(30);
                            columns.ConstantColumn(64);
                            columns.ConstantColumn(24);
                            columns.ConstantColumn(24);
                            columns.ConstantColumn(24);
                            columns.ConstantColumn(24);
                            columns.ConstantColumn(24);
                            columns.ConstantColumn(24);
                            columns.ConstantColumn(24);
                            columns.ConstantColumn(68);
                        });

                        table.Header(header =>
                        {
                            AddHeaderCells(header,
                                "Nº",
                                "Fecha",
                                "Anim.",
                                "Tipo",
                                "Raza",
                                "Marca",
                                "Causa",
                                "Origen / destino",
                                "V",
                                "CV",
                                "HR",
                                "MR",
                                "L",
                                "Rec",
                                "C",
                                "Doc.");
                        });

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(TableBodyCell).Text(row.Order.ToString());
                            table.Cell().Element(TableBodyCell).Text(row.Date ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.NumberOfAnimals ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Type ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.BreedCode ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Tag ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.CauseCode ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Route ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Boars ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.SowsForLive ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.SowsReposition ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.MalesReposition ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Piglets ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Rears ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Baits ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.HealthDocumentNumber ?? string.Empty);
                        }
                    });
                });
            });
        }
    }

    private static void ComposeCensusSection(IDocumentContainer container, BookAggregate aggregate)
    {
        if (aggregate.Farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            ComposePorcineCensusSection(container, aggregate);
            return;
        }

        var rows = aggregate.Censuses
            .Select((census, index) => new OvineCensusRow(
                index + 1,
                FormatDate(census.CensusDate),
                (census.OvinoCaprino?.NonReproductiveUnder4Months ?? 0).ToString(),
                (census.OvinoCaprino?.NonReproductiveBetween4And12Months ?? 0).ToString(),
                (census.OvinoCaprino?.ReproductiveMale ?? 0).ToString(),
                (census.OvinoCaprino?.ReproductiveFemale ?? 0).ToString()))
            .ToList();

        foreach (var pageRows in ChunkOrBlank(rows, 28, OvineCensusRow.Empty))
        {
            container.Page(page =>
            {
                ConfigurePage(page, aggregate, "Censo ovino-caprino");
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text("HOJA DE CENSO TOTAL DEL GANADO OVINO-CAPRINO").Bold().FontSize(14);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(26);
                            columns.ConstantColumn(66);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            AddHeaderCells(header,
                                "Nº",
                                "Fecha",
                                "<4 meses",
                                "4-12 meses",
                                "Rep. machos",
                                "Rep. hembras");
                        });

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(TableBodyCell).Text(row.Order.ToString());
                            table.Cell().Element(TableBodyCell).Text(row.Date ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Under4 ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.From4To12 ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.ReproductiveMales ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.ReproductiveFemales ?? string.Empty);
                        }
                    });
                });
            });
        }
    }

    private static void ComposePorcineCensusSection(IDocumentContainer container, BookAggregate aggregate)
    {
        var rows = aggregate.Censuses
            .Select((census, index) => new PorcineCensusRow(
                index + 1,
                FormatDate(census.CensusDate),
                (census.Porcino?.Boars ?? 0).ToString(),
                (census.Porcino?.Sow ?? 0).ToString(),
                (census.Porcino?.PigsReposition ?? 0).ToString(),
                (census.Porcino?.SowsReposition ?? 0).ToString(),
                (census.Porcino?.Piglets ?? 0).ToString(),
                (census.Porcino?.Rears ?? 0).ToString(),
                (census.Porcino?.Baits ?? 0).ToString()))
            .ToList();

        foreach (var pageRows in ChunkOrBlank(rows, 28, PorcineCensusRow.Empty))
        {
            container.Page(page =>
            {
                ConfigurePage(page, aggregate, "Censo porcino");
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text("HOJA DE CENSO TOTAL DEL GANADO PORCINO").Bold().FontSize(14);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(26);
                            columns.ConstantColumn(66);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            AddHeaderCells(header,
                                "Nº",
                                "Fecha",
                                "V",
                                "CV",
                                "MR",
                                "HR",
                                "L",
                                "Rec",
                                "C");
                        });

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(TableBodyCell).Text(row.Order.ToString());
                            table.Cell().Element(TableBodyCell).Text(row.Date ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Boars ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.SowsForLive ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.MalesReposition ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.SowsReposition ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Piglets ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Rears ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Baits ?? string.Empty);
                        }
                    });
                });
            });
        }
    }

    private static void ComposeIncidentSection(IDocumentContainer container, BookAggregate aggregate)
    {
        if (aggregate.Farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            var rows = aggregate.Incidents
                .Select((incident, index) => new PorcineIncidentRow(
                    index + 1,
                    FormatDate(incident.IncidentDate),
                    EmptyToNull(incident.LastIdentification),
                    EmptyToNull(incident.NewIdentification),
                    EmptyToNull(incident.ChangeReason),
                    incident.AnimalId is null ? string.Empty : "1"))
                .ToList();

            foreach (var pageRows in ChunkOrBlank(rows, 26, PorcineIncidentRow.Empty))
            {
                container.Page(page =>
                {
                    ConfigurePage(page, aggregate, "Incidencias porcino");
                    page.Content().Column(column =>
                    {
                        column.Spacing(10);
                        column.Item().Text("HOJA DE ANEXO DE INCIDENCIAS DE IDENTIFICACIÓN DEL GANADO PORCINO").Bold().FontSize(14);
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(28);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(90);
                                columns.ConstantColumn(90);
                                columns.RelativeColumn();
                                columns.ConstantColumn(42);
                            });

                            table.Header(header => AddHeaderCells(header, "Nº", "Fecha", "Identificación anterior", "Identificación nueva", "Causa", "Anim."));

                            foreach (var row in pageRows)
                            {
                                table.Cell().Element(TableBodyCell).Text(row.Order.ToString());
                                table.Cell().Element(TableBodyCell).Text(row.Date ?? string.Empty);
                                table.Cell().Element(TableBodyCell).Text(row.LastIdentification ?? string.Empty);
                                table.Cell().Element(TableBodyCell).Text(row.NewIdentification ?? string.Empty);
                                table.Cell().Element(TableBodyCell).Text(row.Cause ?? string.Empty);
                                table.Cell().Element(TableBodyCell).Text(row.RemarkedAnimals ?? string.Empty);
                            }
                        });
                    });
                });
            }

            return;
        }

        var ovineRows = aggregate.Incidents
            .Select((incident, index) => new OvineIncidentRow(
                index + 1,
                incident.Animal?.Identification ?? incident.LastIdentification,
                FormatDate(incident.IncidentDate),
                EmptyToNull(incident.Description) ?? EmptyToNull(incident.ChangeReason)))
            .ToList();

        foreach (var pageRows in ChunkOrBlank(ovineRows, 26, OvineIncidentRow.Empty))
        {
            container.Page(page =>
            {
                ConfigurePage(page, aggregate, "Incidencias ovino-caprino");
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text("HOJA DE ANEXO DE INCIDENCIAS DE IDENTIFICACIÓN DEL GANADO").Bold().FontSize(14);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(28);
                            columns.ConstantColumn(120);
                            columns.ConstantColumn(68);
                            columns.RelativeColumn();
                        });

                        table.Header(header => AddHeaderCells(header, "Nº", "Identificación", "Fecha", "Descripción"));

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(TableBodyCell).Text(row.Order.ToString());
                            table.Cell().Element(TableBodyCell).Text(row.Identification ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Date ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Description ?? string.Empty);
                        }
                    });
                });
            });
        }
    }

    private static void ComposeInspectionSection(IDocumentContainer container, BookAggregate aggregate)
    {
        var rows = aggregate.Inspections
            .Select((inspection, index) => new InspectionRow(
                index + 1,
                EmptyToNull(inspection.Reason),
                EmptyToNull(inspection.Observations),
                JoinValues(" · ", EmptyToNull(inspection.Veterinary), FormatDate(inspection.InspectionDate))))
            .ToList();

        foreach (var pageRows in ChunkOrBlank(rows, 24, InspectionRow.Empty))
        {
            container.Page(page =>
            {
                ConfigurePage(page, aggregate, "Control de inspecciones");
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text("HOJA DE CONTROL DE INSPECCIONES").Bold().FontSize(14);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(120);
                            columns.RelativeColumn();
                            columns.ConstantColumn(170);
                        });

                        table.Header(header =>
                        {
                            AddHeaderCells(header,
                                "Motivo",
                                "Observaciones",
                                "Veterinario oficial");
                        });

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(TableBodyCell).Text(row.Reason ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Observations ?? string.Empty);
                            table.Cell().Element(TableBodyCell).Text(row.Signature ?? string.Empty);
                        }
                    });
                });
            });
        }
    }

    private static void ConfigurePage(PageDescriptor page, BookAggregate aggregate, string sectionTitle)
    {
        page.Size(PageSizes.A4);
        page.Margin(20);
        page.DefaultTextStyle(style => style.FontSize(8.5f));
        page.Header().Row(row =>
        {
            row.RelativeItem().Text($"{aggregate.Farm.RegaCode} · {aggregate.Farm.Name}").Bold();
            row.ConstantItem(180).AlignRight().Text(sectionTitle);
        });
        page.Footer().AlignRight().Text(text =>
        {
            text.Span("Página ");
            text.CurrentPageNumber();
            text.Span(" / ");
            text.TotalPages();
        });
    }

    private static IContainer OfficialPanel(IContainer container)
    {
        return container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10);
    }

    private static void ComposeFieldTable(IContainer container, IReadOnlyList<(string Label, string? Value)> fields)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(130);
                columns.RelativeColumn();
            });

            foreach (var (label, value) in fields)
            {
                table.Cell().Element(TableLabelCell).Text(label);
                table.Cell().Element(TableBodyCell).Text(value ?? "No informado");
            }
        });
    }

    private static IReadOnlyList<(string Label, string? Value)> BuildGeneralFarmFields(BookAggregate aggregate)
    {
        var fields = new List<(string Label, string? Value)>
        {
            ("Código explotación REGA", aggregate.Farm.RegaCode),
            ("Número de registro porcino", aggregate.Farm.PorcineRegistryNumber),
            ("Nombre", aggregate.Farm.Name),
            ("Dirección", aggregate.Farm.Address),
            ("Localidad", aggregate.Farm.Town),
            ("Provincia", aggregate.Farm.Province),
            ("Código postal", aggregate.Farm.ZipCode),
            ("Ubicación / huso", aggregate.Farm.Spindle?.ToString()),
            ("Coordenada X", aggregate.Farm.XCoordinate?.ToString("0.##")),
            ("Coordenada Y", aggregate.Farm.YCoordinate?.ToString("0.##")),
            ("Régimen", MapRegime(aggregate.Farm.Regime)),
            ("Clasificación zootécnica", aggregate.Farm.ZootechnicClassification),
            ("Capacidad autorizada", aggregate.Farm.AuthorisedCapacity?.ToString())
        };

        return fields;
    }

    private static void AddHeaderCells(TableCellDescriptor header, params string[] labels)
    {
        foreach (var label in labels)
        {
            header.Cell().Element(TableHeaderCell).Text(label);
        }
    }

    private static IContainer TableHeaderCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten1)
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(4)
            .PaddingHorizontal(3)
            .AlignMiddle();
    }

    private static IContainer TableBodyCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(3)
            .PaddingHorizontal(3)
            .AlignMiddle();
    }

    private static IContainer TableLabelCell(IContainer container)
    {
        return TableHeaderCell(container);
    }

    private static IReadOnlyList<T[]> ChunkOrBlank<T>(IReadOnlyList<T> rows, int size, T emptyRow)
    {
        if (rows.Count == 0)
        {
            return [Enumerable.Repeat(emptyRow, Math.Min(size, 12)).ToArray()];
        }

        return rows.Chunk(size).ToList();
    }

    private static bool IsOvineOrCaprine(LivestockFarm farm) =>
        farm.LivestockSpecies is LivestockSpecies.Ovine or LivestockSpecies.Caprine;

    private static string? MapBreedCode(LivestockSpecies species, string? breed)
    {
        if (string.IsNullOrWhiteSpace(breed))
        {
            return null;
        }

        return species switch
        {
            LivestockSpecies.Ovine => OvineBreedCodes.TryGetValue(breed.Trim(), out var ovineCode) ? ovineCode : "O",
            LivestockSpecies.Caprine => CaprineBreedCodes.TryGetValue(breed.Trim(), out var caprineCode) ? caprineCode : "O",
            LivestockSpecies.Porcine => PorcineBreedCodes.TryGetValue(breed.Trim(), out var porcineCode) ? porcineCode : "O",
            _ => breed.Trim()
        };
    }

    private static string? MapSexCode(string? sex)
    {
        if (string.IsNullOrWhiteSpace(sex))
        {
            return null;
        }

        return sex.Trim().ToLowerInvariant() switch
        {
            "female" or "hembra" or "h" => "H",
            "male" or "macho" or "m" => "M",
            _ => sex.Trim()
        };
    }

    private static string? MapRegistrationCauseCode(AnimalRegistrationCause? cause)
    {
        return cause switch
        {
            AnimalRegistrationCause.Entrada => "E",
            AnimalRegistrationCause.Autorreposicion => "A",
            _ => null
        };
    }

    private static string? MapDischargeCauseCode(AnimalDischargeCause? cause)
    {
        return cause switch
        {
            AnimalDischargeCause.Salida => "S",
            AnimalDischargeCause.Muerte => "M",
            _ => null
        };
    }

    private static string MapPorcineCollectiveCauseCode(string cause)
    {
        if (cause.Equals("Nacimiento", StringComparison.OrdinalIgnoreCase))
        {
            return "N";
        }

        if (cause.Equals("Muerte", StringComparison.OrdinalIgnoreCase))
        {
            return "M";
        }

        if (cause.Equals("Salida", StringComparison.OrdinalIgnoreCase))
        {
            return "S";
        }

        return "I";
    }

    private static string MapOvineBalanceCause(string cause)
    {
        if (cause.Equals("Nacimiento", StringComparison.OrdinalIgnoreCase))
        {
            return "N";
        }

        if (cause.Equals("Muerte", StringComparison.OrdinalIgnoreCase))
        {
            return "M";
        }

        if (cause.Equals("Salida", StringComparison.OrdinalIgnoreCase))
        {
            return "S";
        }

        return cause.Equals("Autorreposicion", StringComparison.OrdinalIgnoreCase) ? "A" : "E";
    }

    private static string? MapRegime(FarmRegime? regime)
    {
        return regime switch
        {
            FarmRegime.Extensive => "Extensivo",
            FarmRegime.SemiExtensive => "Semiextensivo",
            FarmRegime.Intensive => "Intensivo",
            _ => null
        };
    }

    private static string FormatDate(DateOnly? date)
    {
        return date?.ToString("dd-MM-yyyy") ?? string.Empty;
    }

    private static string BuildFarmerName(Farmer farmer)
    {
        return farmer.PersonType == PersonType.Company
            ? farmer.CompanyName?.Trim() ?? farmer.LegalRepresentative?.Trim() ?? farmer.User.Name
            : $"{farmer.User.Name} {farmer.User.Surname} {farmer.SecondSurname}".Replace("  ", " ").Trim();
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? JoinValues(string separator, params string?[] values)
    {
        var existing = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();

        return existing.Count == 0 ? null : string.Join(separator, existing);
    }

    private sealed record BookAggregate(
        LivestockFarm Farm,
        IReadOnlyList<Animal> Animals,
        IReadOnlyList<Balance> Balances,
        IReadOnlyList<Census> Censuses,
        IReadOnlyList<Incident> Incidents,
        IReadOnlyList<Inspection> Inspections,
        IReadOnlyList<MovementCertificate> Movements);

    private sealed record OvineAnimalRow(
        int Order,
        string Identification,
        string? BirthYear,
        string? IdentificationDate,
        string? BreedCode,
        string? SexCode,
        string? Genotyping,
        string? Alleles,
        string? RegistrationCause,
        string? RegistrationDate,
        string? OriginCode,
        string? DischargeCause,
        string? DischargeDate,
        string? DestinationCode,
        string? HealthDocumentNumber)
    {
        public static OvineAnimalRow Empty => new(0, string.Empty, null, null, null, null, null, null, null, null, null, null, null, null, null);
    }

    private sealed record PorcineCollectiveRow(
        int Order,
        string? Date,
        string? NumberOfAnimals,
        string? Type,
        string? BreedCode,
        string? Tag,
        string? CauseCode,
        string? Route,
        string? HealthDocumentNumber)
    {
        public static PorcineCollectiveRow Empty => new(0, null, null, null, null, null, null, null, null);
    }

    private sealed record OvineBalanceRow(
        int Order,
        string? Date,
        string? NumberOfAnimals,
        string? CauseCode,
        string? OriginCode,
        string? DestinationCode,
        string? HealthDocumentNumber,
        string? Under4,
        string? From4To12,
        string? ReproductiveMales,
        string? ReproductiveFemales)
    {
        public static OvineBalanceRow Empty => new(0, null, null, null, null, null, null, null, null, null, null);
    }

    private sealed record PorcineBalanceRow(
        int Order,
        string? Date,
        string? NumberOfAnimals,
        string? Type,
        string? BreedCode,
        string? Tag,
        string? CauseCode,
        string? Route,
        string? Boars,
        string? SowsForLive,
        string? SowsReposition,
        string? MalesReposition,
        string? Piglets,
        string? Rears,
        string? Baits,
        string? HealthDocumentNumber)
    {
        public static PorcineBalanceRow Empty => new(0, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
    }

    private sealed record OvineCensusRow(
        int Order,
        string? Date,
        string? Under4,
        string? From4To12,
        string? ReproductiveMales,
        string? ReproductiveFemales)
    {
        public static OvineCensusRow Empty => new(0, null, null, null, null, null);
    }

    private sealed record PorcineCensusRow(
        int Order,
        string? Date,
        string? Boars,
        string? SowsForLive,
        string? MalesReposition,
        string? SowsReposition,
        string? Piglets,
        string? Rears,
        string? Baits)
    {
        public static PorcineCensusRow Empty => new(0, null, null, null, null, null, null, null, null);
    }

    private sealed record OvineIncidentRow(int Order, string? Identification, string? Date, string? Description)
    {
        public static OvineIncidentRow Empty => new(0, null, null, null);
    }

    private sealed record PorcineIncidentRow(int Order, string? Date, string? LastIdentification, string? NewIdentification, string? Cause, string? RemarkedAnimals)
    {
        public static PorcineIncidentRow Empty => new(0, null, null, null, null, null);
    }

    private sealed record InspectionRow(int Order, string? Reason, string? Observations, string? Signature)
    {
        public static InspectionRow Empty => new(0, null, null, null);
    }
}
