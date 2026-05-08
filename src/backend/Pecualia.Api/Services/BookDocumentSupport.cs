using Pecualia.Api.Contracts.Books;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pecualia.Api.Services;

internal static class BookDocumentSupport
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

    internal static IReadOnlyList<FarmBookPreviewSectionResponse> BuildSections(BookAggregate aggregate)
    {
        var hasIndividualAnimals = IsOvineOrCaprine(aggregate.Farm) || aggregate.Farm.LivestockSpecies == LivestockSpecies.Porcine;

        return
        [
            new("general", "Información general de la explotación", 1, "Datos del titular y de la explotación con formato oficial."),
            new("animals", hasIndividualAnimals ? "Animales individuales" : "Animales colectivos", hasIndividualAnimals ? aggregate.Animals.Count : aggregate.Balances.Count, hasIndividualAnimals ? "Hoja de identificación individual del ganado." : "Relación colectiva basada en movimientos y balances."),
            new("balance", "Información sobre balance", aggregate.Balances.Count, "Histórico de actualizaciones del censo y balance oficial."),
            new("census", "Información sobre censo", aggregate.Censuses.Count, "Declaraciones de censo imprimibles por especie."),
            new("incidents", "Anexo de incidencias", aggregate.Incidents.Count, "Incidencias de identificación del ganado."),
            new("inspections", "Control de inspecciones", aggregate.Inspections.Count, "Histórico de inspecciones oficiales.")
        ];
    }

    internal static bool IsOvineOrCaprine(LivestockFarm farm) =>
        farm.LivestockSpecies is LivestockSpecies.Ovine or LivestockSpecies.Caprine;

    internal static IReadOnlyList<KeyValuePair<string, string>> GetOvineBreedCodes() => OvineBreedCodes.ToList();

    internal static IReadOnlyList<KeyValuePair<string, string>> GetCaprineBreedCodes() => CaprineBreedCodes.ToList();

    internal static IReadOnlyList<KeyValuePair<string, string>> GetPorcineBreedCodes() => PorcineBreedCodes.ToList();

    internal static IReadOnlyList<KeyValuePair<string, string>> GetBreedCodes(LivestockSpecies species) => species switch
    {
        LivestockSpecies.Ovine => GetOvineBreedCodes(),
        LivestockSpecies.Caprine => GetCaprineBreedCodes(),
        LivestockSpecies.Porcine => GetPorcineBreedCodes(),
        _ => []
    };

    internal static bool TryNormalizeBreed(LivestockSpecies species, string? breed, out string? normalizedBreed)
    {
        normalizedBreed = null;
        if (string.IsNullOrWhiteSpace(breed))
        {
            return false;
        }

        var trimmedBreed = breed.Trim();
        foreach (var option in GetBreedCodes(species))
        {
            if (string.Equals(option.Key, trimmedBreed, StringComparison.OrdinalIgnoreCase))
            {
                normalizedBreed = option.Key;
                return true;
            }
        }

        return false;
    }

    internal static void ConfigureOfficialLedgerPage(PageDescriptor page, BookAggregate aggregate, string title, bool includePorcineRegistry)
    {
        page.Size(PageSizes.A4.Landscape());
        page.MarginHorizontal(18);
        page.MarginVertical(16);
        page.DefaultTextStyle(style => style.FontSize(8));
        page.Header().Column(column =>
        {
            column.Spacing(3);
            column.Item().Row(row =>
            {
                if (includePorcineRegistry)
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(9));
                        text.Span("Nº DE REGISTRO PORCINO: ");
                        text.Span(aggregate.Farm.PorcineRegistryNumber ?? string.Empty).Underline();
                    });
                }
                else
                {
                    row.RelativeItem();
                }

                row.RelativeItem().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(9));
                    text.Span("Nº DE REGISTRO DE EXPLOTACIÓN: ");
                    text.Span(aggregate.Farm.RegaCode).Underline();
                });

                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(9));
                    text.Span("PÁGINA: ");
                    text.CurrentPageNumber();
                });
            });

            column.Item().AlignCenter().Text(title).FontSize(12.5f).Bold();
        });
    }

    internal static IContainer OfficialOvineSheetHeaderCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor("#B64945")
            .PaddingVertical(4)
            .PaddingHorizontal(3)
            .MinHeight(24)
            .AlignCenter()
            .AlignMiddle()
            .DefaultTextStyle(style => style.FontSize(7).FontColor("#9F302D").SemiBold());
    }

    internal static IContainer OfficialOvineSheetBodyCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor("#C5625A")
            .PaddingVertical(2)
            .PaddingHorizontal(3)
            .MinHeight(16)
            .AlignCenter()
            .AlignMiddle()
            .DefaultTextStyle(style => style.FontSize(7.2f));
    }

    internal static IContainer OfficialOvineSheetBodyCellLeft(IContainer container)
    {
        return OfficialOvineSheetBodyCell(container).AlignLeft();
    }

    internal static IContainer OfficialLedgerHeaderCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Darken1)
            .PaddingVertical(4)
            .PaddingHorizontal(3)
            .MinHeight(22)
            .AlignCenter()
            .AlignMiddle()
            .DefaultTextStyle(style => style.FontSize(7.4f).SemiBold());
    }

    internal static IContainer OfficialLedgerBodyCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Darken1)
            .PaddingVertical(3)
            .PaddingHorizontal(3)
            .MinHeight(18)
            .AlignCenter()
            .AlignMiddle()
            .DefaultTextStyle(style => style.FontSize(7.2f));
    }

    internal static IContainer OfficialLedgerBodyCellLeft(IContainer container)
    {
        return OfficialLedgerBodyCell(container).AlignLeft();
    }

    internal static IContainer OfficialLedgerFormLabelCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Darken1)
            .PaddingVertical(4)
            .PaddingHorizontal(5)
            .DefaultTextStyle(style => style.FontSize(7.2f).SemiBold());
    }

    internal static IContainer OfficialLedgerFormValueCell(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Darken1)
            .PaddingVertical(4)
            .PaddingHorizontal(5)
            .DefaultTextStyle(style => style.FontSize(7.2f));
    }

    internal static void ComposeCodeLegendTable(IContainer container, string title, IReadOnlyList<KeyValuePair<string, string>> items)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().AlignCenter().Text(title).FontSize(10.5f).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(48);
                });

                table.Header(header =>
                {
                    header.Cell().Element(OfficialLedgerHeaderCell).Text("Raza");
                    header.Cell().Element(OfficialLedgerHeaderCell).Text("Clave");
                });

                foreach (var item in items)
                {
                    table.Cell().Element(OfficialLedgerBodyCellLeft).Text(item.Key);
                    table.Cell().Element(OfficialLedgerBodyCell).Text(item.Value);
                }
            });
        });
    }

    internal static void ComposeSimpleDefinitionTable(IContainer container, string title, IReadOnlyList<(string Label, string Value)> items)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().AlignCenter().Text(title).FontSize(10.5f).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(52);
                });

                table.Header(header =>
                {
                    header.Cell().Element(OfficialLedgerHeaderCell).Text("Concepto");
                    header.Cell().Element(OfficialLedgerHeaderCell).Text("Clave");
                });

                foreach (var item in items)
                {
                    table.Cell().Element(OfficialLedgerBodyCellLeft).Text(item.Label);
                    table.Cell().Element(OfficialLedgerBodyCell).Text(item.Value);
                }
            });
        });
    }

    internal static void ComposeSimpleBulletTable(IContainer container, string title, IReadOnlyList<string> items)
    {
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().AlignCenter().Text(title).FontSize(10.5f).Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns => columns.RelativeColumn());

                foreach (var item in items)
                {
                    table.Cell().Element(OfficialLedgerBodyCellLeft).Text($"□ {item}");
                }
            });
        });
    }

    internal static IReadOnlyList<T[]> ChunkOrBlank<T>(IReadOnlyList<T> rows, int size, T emptyRow)
    {
        if (rows.Count == 0)
        {
            return [Enumerable.Repeat(emptyRow, Math.Min(size, 12)).ToArray()];
        }

        return rows.Chunk(size).ToList();
    }

    internal static string? MapBreedCode(LivestockSpecies species, string? breed)
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

    internal static string? MapSexCode(string? sex)
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

    internal static string? MapRegistrationCauseCode(AnimalRegistrationCause? cause)
    {
        return cause switch
        {
            AnimalRegistrationCause.Entrada => "E",
            AnimalRegistrationCause.Autorreposicion => "A",
            _ => null
        };
    }

    internal static string? MapDischargeCauseCode(AnimalDischargeCause? cause)
    {
        return cause switch
        {
            AnimalDischargeCause.Salida => "S",
            AnimalDischargeCause.Muerte => "M",
            _ => null
        };
    }

    internal static string MapPorcineCollectiveCauseCode(string cause)
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

    internal static string MapOvineBalanceCause(string cause)
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

    internal static string? MapRegime(FarmRegime? regime)
    {
        return regime switch
        {
            FarmRegime.Extensive => "Extensivo",
            FarmRegime.SemiExtensive => "Semiextensivo",
            FarmRegime.Intensive => "Intensivo",
            _ => null
        };
    }

    internal static string FormatDate(DateOnly? date)
    {
        return date?.ToString("dd-MM-yyyy") ?? string.Empty;
    }

    internal static string BuildFarmerName(Farmer farmer)
    {
        return farmer.PersonType == PersonType.Company
            ? farmer.CompanyName?.Trim() ?? farmer.LegalRepresentative?.Trim() ?? farmer.User.Name
            : $"{farmer.User.Name} {farmer.User.Surname} {farmer.SecondSurname}".Replace("  ", " ").Trim();
    }

    internal static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static string? JoinValues(string separator, params string?[] values)
    {
        var existing = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();

        return existing.Count == 0 ? null : string.Join(separator, existing);
    }

    internal static OvineIncidentReferenceLookup BuildOvineIncidentReferenceLookup(BookAggregate aggregate, IReadOnlySet<string> includedSections)
    {
        var byAnimalId = new Dictionary<long, OvineIncidentReference>();
        var byIdentification = new Dictionary<string, OvineIncidentReference>(StringComparer.OrdinalIgnoreCase);

        if (!includedSections.Contains("incidents"))
        {
            return new OvineIncidentReferenceLookup(byAnimalId, byIdentification);
        }

        var incidentStartPage = 1;

        if (includedSections.Contains("general"))
        {
            incidentStartPage += GetGeneralPageCount(aggregate);
        }

        if (includedSections.Contains("animals"))
        {
            incidentStartPage += GetPageCount(aggregate.Animals.Count, 12);
        }

        if (includedSections.Contains("balance"))
        {
            incidentStartPage += GetBalancePageCount(aggregate);
        }

        if (includedSections.Contains("census"))
        {
            incidentStartPage += GetCensusPageCount(aggregate);
        }

        foreach (var reference in aggregate.Incidents.Select((incident, index) =>
                     new
                     {
                         Incident = incident,
                         Reference = new OvineIncidentReference(
                             (incidentStartPage + (index / GetIncidentPageSize(aggregate))).ToString(),
                             (index + 1).ToString())
                     }))
        {
            if (reference.Incident.AnimalId is long animalId)
            {
                byAnimalId.TryAdd(animalId, reference.Reference);
            }

            foreach (var identification in new[]
                     {
                         reference.Incident.Animal?.Identification,
                         reference.Incident.LastIdentification,
                         reference.Incident.NewIdentification
                     })
            {
                var normalizedIdentification = EmptyToNull(identification);
                if (normalizedIdentification is not null)
                {
                    byIdentification.TryAdd(normalizedIdentification, reference.Reference);
                }
            }
        }

        return new OvineIncidentReferenceLookup(byAnimalId, byIdentification);
    }

    internal static OvineIncidentReferenceLookup BuildAnimalSheetReferenceLookup(BookAggregate aggregate, IReadOnlySet<string> includedSections)
    {
        var byAnimalId = new Dictionary<long, OvineIncidentReference>();
        var byIdentification = new Dictionary<string, OvineIncidentReference>(StringComparer.OrdinalIgnoreCase);

        if (!includedSections.Contains("animals"))
        {
            return new OvineIncidentReferenceLookup(byAnimalId, byIdentification);
        }

        var startPage = includedSections.Contains("general")
            ? 1 + GetGeneralPageCount(aggregate)
            : 1;

        var animalPageSize = aggregate.Farm.LivestockSpecies == LivestockSpecies.Porcine ? 10 : 12;

        foreach (var reference in aggregate.Animals.Select((animal, index) =>
                     new
                     {
                         Animal = animal,
                         Reference = new OvineIncidentReference(
                             (startPage + (index / animalPageSize)).ToString(),
                             (index + 1).ToString())
                     }))
        {
            byAnimalId[reference.Animal.Id] = reference.Reference;

            var normalizedIdentification = EmptyToNull(reference.Animal.Identification);
            if (normalizedIdentification is not null)
            {
                byIdentification[normalizedIdentification] = reference.Reference;
            }
        }

        return new OvineIncidentReferenceLookup(byAnimalId, byIdentification);
    }

    internal static string? ResolveOvineIncidentReferencePage(Animal animal, OvineIncidentReferenceLookup lookup)
    {
        return ResolveOvineIncidentReference(animal, lookup)?.Page;
    }

    internal static string? ResolveOvineIncidentReferenceOrder(Animal animal, OvineIncidentReferenceLookup lookup)
    {
        return ResolveOvineIncidentReference(animal, lookup)?.Order;
    }

    internal static string? ResolveIncidentReferencePage(Incident incident, OvineIncidentReferenceLookup lookup)
    {
        return ResolveIncidentReference(incident, lookup)?.Page;
    }

    internal static string? ResolveIncidentReferenceOrder(Incident incident, OvineIncidentReferenceLookup lookup)
    {
        return ResolveIncidentReference(incident, lookup)?.Order;
    }

    internal static int GetBalancePageCount(BookAggregate aggregate)
    {
        return aggregate.Farm.LivestockSpecies == LivestockSpecies.Porcine
            ? GetPageCount(aggregate.Balances.Count, 20)
            : GetPageCount(aggregate.Balances.Count, 18);
    }

    internal static int GetCensusPageCount(BookAggregate aggregate)
    {
        return aggregate.Farm.LivestockSpecies == LivestockSpecies.Porcine
            ? GetPageCount(aggregate.Censuses.Count, 18)
            : GetPageCount(aggregate.Censuses.Count, 10);
    }

    private static int GetGeneralPageCount(BookAggregate aggregate)
    {
        return aggregate.Farm.LivestockSpecies == LivestockSpecies.Porcine ? 2 : 2;
    }

    private static int GetIncidentPageSize(BookAggregate aggregate)
    {
        return aggregate.Farm.LivestockSpecies == LivestockSpecies.Porcine ? 15 : 10;
    }

    private static int GetPageCount(int rowCount, int pageSize)
    {
        return rowCount == 0 ? 1 : (int)Math.Ceiling(rowCount / (double)pageSize);
    }

    private static OvineIncidentReference? ResolveOvineIncidentReference(Animal animal, OvineIncidentReferenceLookup lookup)
    {
        if (lookup.ByAnimalId.TryGetValue(animal.Id, out var byAnimalIdReference))
        {
            return byAnimalIdReference;
        }

        var identification = EmptyToNull(animal.Identification);
        if (identification is not null && lookup.ByIdentification.TryGetValue(identification, out var byIdentificationReference))
        {
            return byIdentificationReference;
        }

        return null;
    }

    private static OvineIncidentReference? ResolveIncidentReference(Incident incident, OvineIncidentReferenceLookup lookup)
    {
        if (incident.AnimalId is long animalId && lookup.ByAnimalId.TryGetValue(animalId, out var animalReference))
        {
            return animalReference;
        }

        foreach (var identification in new[] { incident.Animal?.Identification, incident.LastIdentification, incident.NewIdentification })
        {
            var normalizedIdentification = EmptyToNull(identification);
            if (normalizedIdentification is not null && lookup.ByIdentification.TryGetValue(normalizedIdentification, out var identificationReference))
            {
                return identificationReference;
            }
        }

        return null;
    }
}
