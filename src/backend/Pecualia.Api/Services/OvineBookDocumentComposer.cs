using Pecualia.Api.Models.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pecualia.Api.Services;

internal static class OvineBookDocumentComposer
{
    internal static void ComposeGeneralPages(IDocumentContainer container, BookRenderContext context)
    {
        var aggregate = context.Aggregate;

        container.Page(page =>
        {
            BookDocumentSupport.ConfigureOfficialLedgerPage(page, aggregate, "DATOS DEL TITULAR Y DATOS DE LA EXPLOTACIÓN", false);

            page.Content().Column(column =>
            {
                column.Spacing(10);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(180);
                        columns.RelativeColumn();
                        columns.ConstantColumn(124);
                        columns.ConstantColumn(110);
                    });

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("NOMBRE O RAZÓN SOCIAL:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(BookDocumentSupport.BuildFarmerName(aggregate.Farm.Farmer));
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("NIF o CIF:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Farmer.NifCif);

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("DOMICILIO:");
                    table.Cell().ColumnSpan(3).Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Farmer.Residence ?? string.Empty);

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("LOCALIDAD:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Farmer.Town ?? string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("PROVINCIA:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Farmer.Province ?? string.Empty);

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("RESPONSABLE DE LOS ANIMALES:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Responsible ?? BookDocumentSupport.BuildFarmerName(aggregate.Farm.Farmer));
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("TELÉFONO:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Farmer.PhoneNumber ?? string.Empty);
                });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(138);
                        columns.ConstantColumn(150);
                        columns.ConstantColumn(98);
                        columns.ConstantColumn(98);
                        columns.ConstantColumn(98);
                        columns.ConstantColumn(98);
                    });

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("CÓDIGO EXPLOTACIÓN REGA:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.RegaCode);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("NOMBRE:");
                    table.Cell().ColumnSpan(3).Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Name);

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("DIRECCIÓN:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Address ?? string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("PROVINCIA:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Province ?? string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("LOCALIDAD:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Town ?? string.Empty);

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("UBICACIÓN EXPLOTACIÓN. COORDENADAS:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text($"HUSO {aggregate.Farm.Spindle?.ToString() ?? "—"}");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("COORDENADA X:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.XCoordinate?.ToString("0.##") ?? string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("COORDENADA Y:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.YCoordinate?.ToString("0.##") ?? string.Empty);

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("ESPECIE GANADERA:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.LivestockSpecies == LivestockSpecies.Caprine ? "CAPRINA" : "OVINA");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("TIPO EXPLOTACIÓN:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.ZootechnicClassification ?? string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("CLASIFICACIÓN ZOOTÉCNICA:");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.ZootechnicClassification ?? string.Empty);
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Diligencia: Para hacer constar que son verdaderos todos los datos reseñados")
                        .FontSize(8);
                    row.ConstantItem(220).AlignRight().Text($"En {aggregate.Farm.Town ?? "________"}, a ____ de __________ de ______")
                        .FontSize(8);
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Visado Administración").FontSize(8);
                    row.ConstantItem(180).AlignRight().Text("El Titular / El Representante").FontSize(8);
                });
            });
        });

        container.Page(page =>
        {
            BookDocumentSupport.ConfigureOfficialLedgerPage(page, aggregate, "INSTRUCCIONES", false);

            page.Content().Column(column =>
            {
                column.Spacing(8);
                column.Item().Text("1. Los titulares o poseedores de ovinos/caprinos, excepto el transportista, deberán llevar en su explotación, de manera actualizada, un libro de registro de explotación.")
                    .FontSize(8);
                column.Item().Text("2. El libro de registro tendrá un formato aprobado por el Servicio de Sanidad Animal, se llevará de forma manual o informatizada y contendrá los datos mínimos establecidos en la normativa vigente.")
                    .FontSize(8);
                column.Item().Text("3. El libro de registro estará disponible en la explotación y será accesible para la autoridad competente durante un período no inferior a 3 años desde la última anotación.")
                    .FontSize(8);
                column.Item().Text("4. Los titulares deberán conservar los documentos de traslado de los animales que han entrado y un duplicado de los que han salido durante un período mínimo de 3 años.")
                    .FontSize(8);
                column.Item().Text("5. Las explotaciones ovinas/caprinas deberán suministrar antes del 1 de marzo de cada año el censo total de animales mantenidos en la explotación a día 1 de enero.")
                    .FontSize(8);
                column.Item().Text("6. Se realizará un balance de reproductores cada vez que se realice una entrada, salida o que los animales accedan a la condición de reproductores.")
                    .FontSize(8);
                column.Item().Text("7. Los medios de identificación se colocarán en los ovinos/caprinos en el plazo máximo legal y, en cualquier caso, antes de que el animal abandone la explotación en la que ha nacido.")
                    .FontSize(8);
                column.Item().Text("8. Los códigos de las razas que se deben indicar en la hoja de identificación individual del ganado son los siguientes:")
                    .FontSize(8);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(item => BookDocumentSupport.ComposeCodeLegendTable(item, "RAZAS OVINAS", BookDocumentSupport.GetOvineBreedCodes().Take(7).ToList()));
                    row.RelativeItem().Element(item => BookDocumentSupport.ComposeCodeLegendTable(item, "RAZAS OVINAS", BookDocumentSupport.GetOvineBreedCodes().Skip(7).ToList()));
                    row.RelativeItem().Element(item => BookDocumentSupport.ComposeCodeLegendTable(item, "RAZAS CAPRINAS", BookDocumentSupport.GetCaprineBreedCodes()));
                });
            });
        });
    }

    internal static void ComposeAnimalsSection(IDocumentContainer container, BookRenderContext context)
    {
        var aggregate = context.Aggregate;
        var incidentReferences = BookDocumentSupport.BuildOvineIncidentReferenceLookup(aggregate, context.IncludedSections);
        var rows = aggregate.Animals
            .Select((animal, index) => new OvineAnimalRow(
                index + 1,
                animal.Identification,
                animal.BirthYear?.ToString(),
                BookDocumentSupport.FormatDate(animal.RegistrationDate),
                BookDocumentSupport.MapBreedCode(aggregate.Farm.LivestockSpecies, animal.Breed),
                BookDocumentSupport.MapSexCode(animal.Sex),
                BookDocumentSupport.EmptyToNull(animal.OvinoCaprino?.Genotyping),
                BookDocumentSupport.EmptyToNull(animal.OvinoCaprino?.DominantAllele),
                BookDocumentSupport.EmptyToNull(animal.OvinoCaprino?.LowAllele),
                BookDocumentSupport.MapRegistrationCauseCode(animal.RegistrationCause),
                BookDocumentSupport.FormatDate(animal.RegistrationDate),
                BookDocumentSupport.EmptyToNull(animal.OriginCode),
                BookDocumentSupport.MapDischargeCauseCode(animal.DischargeCause),
                BookDocumentSupport.FormatDate(animal.DischargeDate),
                BookDocumentSupport.EmptyToNull(animal.DestinationCode),
                aggregate.GuideSeriesByAnimalId.TryGetValue(animal.Id, out var guideSeries)
                    ? BookDocumentSupport.FormatAnimalGuideSeries(guideSeries.Entry, guideSeries.Exit)
                    : null,
                BookDocumentSupport.ResolveOvineIncidentReferencePage(animal, incidentReferences),
                BookDocumentSupport.ResolveOvineIncidentReferenceOrder(animal, incidentReferences)))
            .ToList();

        var pagedRows = rows.Count == 0
            ? new[] { Enumerable.Repeat(OvineAnimalRow.Empty, 12).ToArray() }
            : rows.Chunk(12).ToArray();

        foreach (var pageRows in pagedRows)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginHorizontal(18);
                page.MarginVertical(16);
                page.DefaultTextStyle(style => style.FontSize(8));
                page.Header().Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(9).FontColor("#9F302D"));
                        text.Span("Nº REGISTRO DE EXPLOTACIÓN ");
                        text.Span(aggregate.Farm.RegaCode).Underline();
                    });

                    row.ConstantItem(170).AlignCenter().Text("HOJA DE IDENTIFICACIÓN INDIVIDUAL DEL GANADO OVINO-CAPRINO")
                        .FontSize(13)
                        .Bold()
                        .FontColor("#9F302D");

                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(9).FontColor("#9F302D"));
                        text.Span("Página: ");
                        text.CurrentPageNumber();
                    });
                });

                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().PaddingTop(6).DefaultTextStyle(style => style.FontSize(7.2f)).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(36);
                            columns.ConstantColumn(136);
                            columns.ConstantColumn(76);
                            columns.ConstantColumn(48);
                            columns.ConstantColumn(34);
                            columns.ConstantColumn(34);
                            columns.ConstantColumn(28);
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(54);
                            columns.ConstantColumn(78);
                            columns.ConstantColumn(112);
                            columns.ConstantColumn(34);
                            columns.ConstantColumn(34);
                        });

                        table.Header(header =>
                        {
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Nº\nOrden");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Nº Identificación");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Año nacimiento");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Raza (1)");
                            header.Cell().ColumnSpan(2).Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Genotipado (3)");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Alta");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Causa (4)");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Fecha alta");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Procedencia (6)");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Nº del documento sanitario de acompañamiento (8)");
                            header.Cell().ColumnSpan(2).Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Incidencias (9)");

                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Fecha Identificación");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Sexo (2)");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Alelo");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Alelo");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Baja");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Causa (5)");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Fecha baja");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Destino (7)");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Hoja");
                            header.Cell().Element(BookDocumentSupport.OfficialOvineSheetHeaderCell).Text("Orden");
                        });

                        foreach (var row in pageRows)
                        {
                            var orderText = row.Order == 0 ? string.Empty : row.Order.ToString();

                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(orderText);
                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialOvineSheetBodyCellLeft).Text(row.Identification);
                            table.Cell().Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(row.BirthYear ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(row.BreedCode ?? string.Empty);
                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(row.DominantAllele ?? string.Empty);
                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(row.LowAllele ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(orderText.Length == 0 ? string.Empty : "A");
                            table.Cell().Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(row.RegistrationCause ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(row.RegistrationDate ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialOvineSheetBodyCellLeft).Text(row.OriginCode ?? string.Empty);
                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(row.HealthDocumentNumber ?? string.Empty);
                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(row.IncidentPage ?? string.Empty);
                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(row.IncidentOrder ?? string.Empty);

                            table.Cell().Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(row.IdentificationDate ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(row.SexCode ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(orderText.Length == 0 ? string.Empty : "B");
                            table.Cell().Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(row.DischargeCause ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialOvineSheetBodyCell).Text(row.DischargeDate ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialOvineSheetBodyCellLeft).Text(row.DestinationCode ?? string.Empty);
                        }
                    });
                });

                page.Footer().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(7).FontColor("#9F302D"));
                        text.Span("(1) Indíquese la raza según las claves contenidas en las instrucciones.\n");
                        text.Span("(2) Indíquese lo que proceda: Macho (M), Hembra (H).\n");
                        text.Span("(3) Genotipado: Rellenar sólo en caso de realizar selección genética.\n");
                        text.Span("(4) Como causas de alta: Entrada (E), Autorreposición (A).\n");
                        text.Span("(5) Como causas de baja: Salida (S), Muerte (M).\n");
                        text.Span("(6) Se cumplimenta en caso de apertura del Libro (Apertura) o en caso de entrada (indicando código de explotación de procedencia).\n");
                        text.Span("(7) Se cumplimenta en caso de salida (indicando código de explotación de destino).\n");
                        text.Span("(8) Se cumplimenta en caso de movimiento de animales.\n");
                        text.Span("(9) Se anotará el nº de hoja y nº de orden del Anexo de Incidencias.");
                    });

                    row.ConstantItem(160).AlignBottom().AlignRight().Text("Firma del titular:")
                        .FontSize(9)
                        .FontColor("#9F302D");
                });
            });
        }
    }

    internal static void ComposeBalanceSection(IDocumentContainer container, BookRenderContext context)
    {
        var aggregate = context.Aggregate;
        var rows = aggregate.Balances
            .Select((balance, index) => new OvineBalanceRow(
                index + 1,
                BookDocumentSupport.FormatDate(balance.BalanceDate),
                balance.NumberOfAnimals.ToString(),
                BookDocumentSupport.MapOvineBalanceCause(balance.ModificationCause),
                BookDocumentSupport.EmptyToNull(balance.OriginLivestockCode),
                BookDocumentSupport.EmptyToNull(balance.DestinationLivestockCode),
                BookDocumentSupport.EmptyToNull(balance.HealthDocumentNumber),
                BookDocumentSupport.EmptyToNull(balance.OvinoCaprino?.TransporterName),
                BookDocumentSupport.EmptyToNull(balance.OvinoCaprino?.TransportTicketNumber),
                (balance.OvinoCaprino?.NonReproductiveUnder4Months ?? 0).ToString(),
                (balance.OvinoCaprino?.NonReproductiveBetween4And12Months ?? 0).ToString(),
                (balance.OvinoCaprino?.ReproductiveMales ?? 0).ToString(),
                (balance.OvinoCaprino?.ReproductiveFemales ?? 0).ToString()))
            .ToList();

        foreach (var pageRows in BookDocumentSupport.ChunkOrBlank(rows, 18, OvineBalanceRow.Empty))
        {
            container.Page(page =>
            {
                BookDocumentSupport.ConfigureOfficialLedgerPage(page, aggregate, "HOJA DE BALANCE DEL GANADO OVINO-CAPRINO", false);
                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(112);
                            columns.ConstantColumn(48);
                            columns.ConstantColumn(94);
                            columns.ConstantColumn(82);
                            columns.RelativeColumn();
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(44);
                            columns.ConstantColumn(44);
                            columns.ConstantColumn(44);
                            columns.ConstantColumn(44);
                        });

                        table.Header(header =>
                        {
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Fecha (1)");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Causa de modificación del balance (2)");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº de animales");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Código explotación de procedencia o destino (3)");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº documento sanitario de acompañamiento (4)");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nombre transportista");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº matrícula transportista");
                            header.Cell().ColumnSpan(4).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Balance");

                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("No reproductor\n< 4 meses");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("No reproductor\nde 4 a 12 meses");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Reproductores\nmachos");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Reproductores\nhembras");
                        });

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Date ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.CauseCode ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.NumberOfAnimals ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).Text(BookDocumentSupport.JoinValues(" / ", row.OriginCode, row.DestinationCode) ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.HealthDocumentNumber ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).Text(row.TransporterName ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.TransportTicketNumber ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Under4 ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.From4To12 ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.ReproductiveMales ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.ReproductiveFemales ?? string.Empty);
                        }
                    });
                });

                page.Footer().PaddingTop(4).Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(7));
                    text.Span("(1) Fecha en la que se realiza el asiento.\n");
                    text.Span("(2) Causa: Entrada (E), Autorreposición (A), Salida (S), Muerte (M), Nacimiento (N).\n");
                    text.Span("(3) Código de la explotación de procedencia o de destino.\n");
                    text.Span("(4) Documento sanitario de acompañamiento.");
                });
            });
        }
    }

    internal static void ComposeCensusSection(IDocumentContainer container, BookRenderContext context)
    {
        var aggregate = context.Aggregate;
        var rows = aggregate.Censuses
            .Select((census, index) => new OvineCensusRow(
                index + 1,
                BookDocumentSupport.FormatDate(census.CensusDate),
                (census.OvinoCaprino?.NonReproductiveUnder4Months ?? 0).ToString(),
                (census.OvinoCaprino?.NonReproductiveBetween4And12Months ?? 0).ToString(),
                (census.OvinoCaprino?.ReproductiveMale ?? 0).ToString(),
                (census.OvinoCaprino?.ReproductiveFemale ?? 0).ToString()))
            .ToList();

        foreach (var pageRows in BookDocumentSupport.ChunkOrBlank(rows, 10, OvineCensusRow.Empty))
        {
            container.Page(page =>
            {
                BookDocumentSupport.ConfigureOfficialLedgerPage(page, aggregate, "HOJA DE CENSO TOTAL DEL GANADO OVINO-CAPRINO", false);
                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(12);
                            columns.ConstantColumn(26);
                            columns.RelativeColumn(13);
                            columns.ConstantColumn(26);
                            columns.RelativeColumn(13);
                            columns.ConstantColumn(26);
                            columns.RelativeColumn(13);
                            columns.ConstantColumn(26);
                            columns.RelativeColumn(13);
                            columns.RelativeColumn(18);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Fecha del\ncenso");
                            header.Cell().ColumnSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("No reproductor\nde menos de 4 meses");
                            header.Cell().ColumnSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("No reproductor\nde 4 a 12 meses");
                            header.Cell().ColumnSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Reproductores machos");
                            header.Cell().ColumnSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Reproductores hembras");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Visado de los Servicios Veterinarios Oficiales");
                        });

                        foreach (var row in pageRows)
                        {
                            var isCaprine = aggregate.Farm.LivestockSpecies == LivestockSpecies.Caprine;
                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Date ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text("Ov");
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(isCaprine ? string.Empty : row.Under4 ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text("Ov");
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(isCaprine ? string.Empty : row.From4To12 ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text("Ov");
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(isCaprine ? string.Empty : row.ReproductiveMales ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text("Ov");
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(isCaprine ? string.Empty : row.ReproductiveFemales ?? string.Empty);
                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).Text("Unidad:\nFecha:\nSello:");

                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text("Cap");
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(isCaprine ? row.Under4 ?? string.Empty : string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text("Cap");
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(isCaprine ? row.From4To12 ?? string.Empty : string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text("Cap");
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(isCaprine ? row.ReproductiveMales ?? string.Empty : string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text("Cap");
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(isCaprine ? row.ReproductiveFemales ?? string.Empty : string.Empty);
                        }
                    });
                });

                page.Footer().PaddingTop(4).Text("Esta hoja se debe actualizar cada vez que se declare el censo y de forma obligatoria a 1 de enero y a 1 de junio de cada año.")
                    .FontSize(7);
            });
        }
    }

    internal static void ComposeIncidentSection(IDocumentContainer container, BookRenderContext context)
    {
        var aggregate = context.Aggregate;
        var animalSheetReferences = BookDocumentSupport.BuildAnimalSheetReferenceLookup(aggregate, context.IncludedSections);
        var rows = aggregate.Incidents
            .Select((incident, index) => new OvineIncidentRow(
                index + 1,
                BookDocumentSupport.ResolveIncidentReferencePage(incident, animalSheetReferences),
                BookDocumentSupport.ResolveIncidentReferenceOrder(incident, animalSheetReferences),
                BookDocumentSupport.FormatDate(incident.IncidentDate),
                BookDocumentSupport.EmptyToNull(incident.Description) ?? BookDocumentSupport.EmptyToNull(incident.ChangeReason)))
            .ToList();

        foreach (var pageRows in BookDocumentSupport.ChunkOrBlank(rows, 10, OvineIncidentRow.Empty))
        {
            container.Page(page =>
            {
                BookDocumentSupport.ConfigureOfficialLedgerPage(page, aggregate, "HOJA DE ANEXO DE INCIDENCIAS DE IDENTIFICACIÓN DEL GANADO", false);
                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(52);
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(70);
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº de\nOrden");
                            header.Cell().ColumnSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Identificación ganado (1)");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Fecha (2)");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Descripción de la incidencia (3)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Hoja");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Orden");
                        });

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Order == 0 ? string.Empty : row.Order.ToString());
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.ReferencePage ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.ReferenceOrder ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Date ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).Text(row.Description ?? string.Empty);
                        }
                    });
                });

                page.Footer().PaddingTop(4).Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(7));
                    text.Span("(1) Se indicará el nº de hoja y de orden del apartado del libro destinado a la identificación individual del ganado.\n");
                    text.Span("(2) Fecha de la detección de la incidencia de identificación.\n");
                    text.Span("(3) Se especificará brevemente cómo se originó la incidencia y, si es el caso, cómo se resolvió.");
                });
            });
        }
    }
}
