using Pecualia.Api.Models.Enums;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Pecualia.Api.Services;

internal static class PorcineBookDocumentComposer
{
    internal static void ComposeGeneralPages(IDocumentContainer container, BookRenderContext context)
    {
        var aggregate = context.Aggregate;
        var opening = aggregate.Censuses.LastOrDefault()?.Porcino;

        container.Page(page =>
        {
            BookDocumentSupport.ConfigureOfficialLedgerPage(page, aggregate, "DATOS DEL TITULAR / DATOS DE LA EXPLOTACIÓN", true);

            page.Content().Column(column =>
            {
                column.Spacing(10);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(180);
                        columns.RelativeColumn();
                        columns.ConstantColumn(120);
                        columns.ConstantColumn(120);
                    });

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("NOMBRE O RAZÓN SOCIAL");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(BookDocumentSupport.BuildFarmerName(aggregate.Farm.Farmer));
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("NIF o CIF");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Farmer.NifCif);

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("DOMICILIO");
                    table.Cell().ColumnSpan(3).Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Farmer.Residence ?? string.Empty);

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("LOCALIDAD");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Farmer.Town ?? string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("PROVINCIA");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Farmer.Province ?? string.Empty);

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("RESPONSABLE DE LOS ANIMALES");
                    table.Cell().ColumnSpan(3).Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Responsible ?? BookDocumentSupport.BuildFarmerName(aggregate.Farm.Farmer));
                });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(160);
                        columns.ConstantColumn(150);
                        columns.ConstantColumn(110);
                        columns.ConstantColumn(125);
                        columns.ConstantColumn(110);
                        columns.ConstantColumn(110);
                    });

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("NÚMERO DE REGISTRO PORCINO");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.PorcineRegistryNumber ?? string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("CÓDIGO EXPLOTACIÓN REGA");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.RegaCode);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("NOMBRE");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Name);

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("DIRECCIÓN");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Address ?? string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("CÓDIGO POSTAL");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.ZipCode ?? string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("LOCALIDAD");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Town ?? string.Empty);

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("PROVINCIA");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.Province ?? string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("UBICACIÓN EXPLOTACIÓN. COORDENADAS");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text($"HUSO {aggregate.Farm.Spindle?.ToString() ?? "—"}");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("COORDENADA X");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.XCoordinate?.ToString("0.##") ?? string.Empty);

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("COORDENADA Y");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.YCoordinate?.ToString("0.##") ?? string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("RÉGIMEN");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(BookDocumentSupport.MapRegime(aggregate.Farm.Regime) ?? string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("CLASIFICACIÓN PRODUCTIVA");
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.ZootechnicClassification ?? string.Empty);

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("CLASIFICACIÓN ZOOTÉCNICA");
                    table.Cell().ColumnSpan(2).Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.ZootechnicClassification ?? string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerFormLabelCell).Text("CAPACIDAD AUTORIZADA");
                    table.Cell().ColumnSpan(2).Element(BookDocumentSupport.OfficialLedgerFormValueCell).Text(aggregate.Farm.AuthorisedCapacity?.ToString() ?? string.Empty);
                });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(90);
                        columns.ConstantColumn(90);
                        columns.ConstantColumn(90);
                        columns.ConstantColumn(110);
                        columns.ConstantColumn(100);
                        columns.ConstantColumn(90);
                        columns.ConstantColumn(90);
                        columns.ConstantColumn(90);
                    });

                    table.Header(header =>
                    {
                        header.Cell().ColumnSpan(8).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("APERTURA DEL LIBRO (CENSO)");
                        header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("FECHA");
                        header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº VERRACOS");
                        header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº CERDAS");
                        header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº M. REPOSICIÓN");
                        header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº H. REPOSICIÓN");
                        header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº LECHONES");
                        header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº RECRÍA");
                        header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº CEBO");
                    });

                    table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(aggregate.Censuses.LastOrDefault() is { CensusDate: var censusDate } ? BookDocumentSupport.FormatDate(censusDate) : string.Empty);
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text((opening?.Boars ?? 0).ToString());
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text((opening?.Sow ?? 0).ToString());
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text((opening?.PigsReposition ?? 0).ToString());
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text((opening?.SowsReposition ?? 0).ToString());
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text((opening?.Piglets ?? 0).ToString());
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text((opening?.Rears ?? 0).ToString());
                    table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text((opening?.Baits ?? 0).ToString());
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Diligencia: Para hacer constar que son verdaderos todos los datos reseñados").FontSize(8);
                    row.ConstantItem(220).AlignRight().Text($"En {aggregate.Farm.Town ?? "________"}, a ____ de __________ de ______").FontSize(8);
                });
            });
        });

        container.Page(page =>
        {
            BookDocumentSupport.ConfigureOfficialLedgerPage(page, aggregate, "INSTRUCCIONES", true);

            page.Content().Column(column =>
            {
                column.Spacing(8);
                column.Item().Text("1.- Sistemática de cumplimentación: El presente Libro de Registro Porcino está compuesto de tantas hojas como sean necesarias de acuerdo con el número de animales presentes en la explotación y los movimientos de altas y bajas.")
                    .FontSize(8);
                column.Item().Text("2.- El Libro de Registro de explotación porcina deberá reflejar la relación de animales presentes en la explotación con indicación de su marca de identificación, tipo de animales y raza, así como las bajas y altas que se produzcan en la explotación.")
                    .FontSize(8);
                column.Item().Text("3.- El código de explotación será asignado por la autoridad competente y estará compuesto por el formato ES + provincia + municipio + número único.")
                    .FontSize(8);
                column.Item().Text("4.- Fecha de alta o baja: de todos los animales inscritos en el libro deberá reflejarse la fecha del alta o de la baja en la explotación.")
                    .FontSize(8);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(item => BookDocumentSupport.ComposeCodeLegendTable(item, "RAZA", BookDocumentSupport.GetPorcineBreedCodes()));
                    row.RelativeItem().Element(item => BookDocumentSupport.ComposeSimpleDefinitionTable(
                        item,
                        "ALTAS / BAJAS EN LA EXPLOTACIÓN",
                        [("ALTA: Nacimiento en la explotación", "N"), ("ALTA: Compra de otra explotación", "C"), ("ALTA: Autorreposición", "I*"), ("BAJA: Muerte", "M"), ("BAJA: Ventas para vida", "V"), ("BAJA: Ventas para sacrificio", "S")]));
                    row.RelativeItem().Element(item => BookDocumentSupport.ComposeSimpleBulletTable(
                        item,
                        "CLASIFICACIÓN ZOOTÉCNICA",
                        ["Selección", "Multiplicación", "Recría de reproductores", "Transición reproductoras primíparas", "Centro de inseminación artificial", "Producción", "Transición de lechones", "Cebo", "Otros"]));
                });

                column.Item().Text("8.- Apertura del libro: cumplimentar por el ganadero en el momento de expedición del libro y visar por la OVZ correspondiente.").FontSize(8);
                column.Item().Text("9.- Balance: el ganadero deberá mantener un balance del censo de porcinos de su explotación, anotando el mismo después de cada movimiento de salida, entrada o nacimientos.").FontSize(8);
                column.Item().Text("10.- Firmas: todas las hojas deben ser firmadas por el ganadero, no siendo válidas las que no estén firmadas.").FontSize(8);
                column.Item().Text("11.- Incidencias en identificación: anotar cualquier cambio de marcas, fecha del cambio, marca anterior y nueva, causa de la sustitución y número de animales remarcados.").FontSize(8);
            });
        });
    }

    internal static void ComposeAnimalsSection(IDocumentContainer container, BookRenderContext context)
    {
        var aggregate = context.Aggregate;
        var incidentReferences = BookDocumentSupport.BuildOvineIncidentReferenceLookup(aggregate, context.IncludedSections);
        var rows = aggregate.Animals
            .Select((animal, index) => new PorcineAnimalRow(
                index + 1,
                animal.Identification,
                animal.BirthYear?.ToString(),
                BookDocumentSupport.FormatDate(animal.Porcino?.IdentificationDate ?? animal.RegistrationDate),
                BookDocumentSupport.MapBreedCode(LivestockSpecies.Porcine, animal.Breed),
                BookDocumentSupport.MapSexCode(animal.Sex),
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
            ? new[] { Enumerable.Repeat(PorcineAnimalRow.Empty, 10).ToArray() }
            : rows.Chunk(10).ToArray();

        foreach (var pageRows in pagedRows)
        {
            container.Page(page =>
            {
                BookDocumentSupport.ConfigureOfficialLedgerPage(page, aggregate, "HOJA DE IDENTIFICACIÓN INDIVIDUAL DEL GANADO PORCINO", true);
                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(36);
                            columns.RelativeColumn(19);
                            columns.ConstantColumn(92);
                            columns.ConstantColumn(48);
                            columns.ConstantColumn(34);
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(64);
                            columns.RelativeColumn(16);
                            columns.RelativeColumn(17);
                            columns.ConstantColumn(40);
                            columns.ConstantColumn(40);
                        });

                        table.Header(header =>
                        {
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº\nOrden");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº\nIdentificación");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Año nacimiento");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Raza (1)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Alta");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Causa (3)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Fecha alta");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Procedencia (5)");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº de documento sanitario de acompañamiento (7)");
                            header.Cell().ColumnSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Incidencias (8)");

                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Fecha identificación");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Sexo (2)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Baja");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Causa (4)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Fecha baja");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Destino (6)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Hoja");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Orden");
                        });

                        foreach (var row in pageRows)
                        {
                            var orderText = row.Order == 0 ? string.Empty : row.Order.ToString();

                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(orderText);
                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).Text(row.Identification);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.BirthYear ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.BreedCode ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(orderText.Length == 0 ? string.Empty : "A");
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.RegistrationCause ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.RegistrationDate ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).Text(row.OriginCode ?? string.Empty);
                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.HealthDocumentNumber ?? string.Empty);
                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.IncidentPage ?? string.Empty);
                            table.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.IncidentOrder ?? string.Empty);

                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.IdentificationDate ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.SexCode ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(orderText.Length == 0 ? string.Empty : "B");
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.DischargeCause ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.DischargeDate ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).Text(row.DestinationCode ?? string.Empty);
                        }
                    });
                });

                page.Footer().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(7));
                        text.Span("(1) Indíquese la raza según las claves contenidas en las instrucciones.\n");
                        text.Span("(2) Indíquese lo que proceda: Macho (M), Hembra (H).\n");
                        text.Span("(3) Como causas de alta: Compra de otra explotación (C), Nacimiento (N), Autorreposición (I).\n");
                        text.Span("(4) Como causas de baja: Ventas para vida (V), Ventas para sacrificio (S), Muerte (M).\n");
                        text.Span("(5) Código de procedencia.\n");
                        text.Span("(6) Código de destino.\n");
                        text.Span("(7) Documento sanitario de acompañamiento.\n");
                        text.Span("(8) Nº de hoja y nº de orden del anexo de incidencias.");
                    });

                    row.ConstantItem(160).AlignBottom().AlignRight().Text("Firma del titular:")
                        .FontSize(9);
                });
            });
        }
    }

    internal static void ComposeBalanceSection(IDocumentContainer container, BookRenderContext context)
    {
        var aggregate = context.Aggregate;
        var rows = aggregate.Balances
            .Select((balance, index) => new PorcineBalanceRow(
                index + 1,
                BookDocumentSupport.FormatDate(balance.BalanceDate),
                balance.NumberOfAnimals.ToString(),
                BookDocumentSupport.EmptyToNull(balance.Porcino?.Type),
                BookDocumentSupport.MapBreedCode(LivestockSpecies.Porcine, balance.Porcino?.Breed),
                BookDocumentSupport.EmptyToNull(balance.Porcino?.Tag),
                BookDocumentSupport.MapPorcineCollectiveCauseCode(balance.ModificationCause),
                BookDocumentSupport.JoinValues(" / ", balance.OriginLivestockCode, balance.DestinationLivestockCode),
                (balance.Porcino?.Boars ?? 0).ToString(),
                (balance.Porcino?.SowsForLive ?? 0).ToString(),
                (balance.Porcino?.SowsReposition ?? 0).ToString(),
                (balance.Porcino?.PigsReposition ?? 0).ToString(),
                (balance.Porcino?.Piglets ?? 0).ToString(),
                (balance.Porcino?.Rear ?? 0).ToString(),
                (balance.Porcino?.Baits ?? 0).ToString(),
                BookDocumentSupport.EmptyToNull(balance.HealthDocumentNumber)))
            .ToList();

        foreach (var pageRows in BookDocumentSupport.ChunkOrBlank(rows, 20, PorcineBalanceRow.Empty))
        {
            container.Page(page =>
            {
                BookDocumentSupport.ConfigureOfficialLedgerPage(page, aggregate, "HOJA DE BALANCE DEL GANADO PORCINO", true);
                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(36);
                            columns.ConstantColumn(74);
                            columns.ConstantColumn(52);
                            columns.ConstantColumn(44);
                            columns.ConstantColumn(44);
                            columns.RelativeColumn(10);
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(42);
                            columns.RelativeColumn(12);
                            columns.ConstantColumn(28);
                            columns.ConstantColumn(34);
                            columns.ConstantColumn(28);
                            columns.ConstantColumn(28);
                            columns.ConstantColumn(34);
                            columns.ConstantColumn(34);
                            columns.ConstantColumn(28);
                            columns.ConstantColumn(40);
                            columns.ConstantColumn(40);
                        });

                        table.Header(header =>
                        {
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº\nORDEN");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("FECHA DEL\nALTA O BAJA");
                            header.Cell().ColumnSpan(7).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("ACTUALIZACIÓN DEL CENSO");
                            header.Cell().ColumnSpan(7).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("BALANCE");
                            header.Cell().ColumnSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Incidencias Identificación Explotación (17)");

                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº\nANIMALES");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("TIPO (9)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("RAZA (10)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("MARCA (11)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº Documento\nSanitario (12)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Causa (13)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Origen (14)\nDestino (15)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("V");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("CV");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("HR");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("MR");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("L");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Rec");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("C");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Hoja");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Orden");
                        });

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Order == 0 ? string.Empty : row.Order.ToString());
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Date ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.NumberOfAnimals ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Type ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.BreedCode ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Tag ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.HealthDocumentNumber ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.CauseCode ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).Text(row.Route ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Boars ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.SowsForLive ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.SowsReposition ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.MalesReposition ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Piglets ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Rears ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Baits ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(string.Empty);
                        }
                    });
                });

                page.Footer().PaddingTop(4).Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(7));
                    text.Span("(9) Indíquese el tipo de animal según la codificación oficial.\n");
                    text.Span("(10) Raza según claves del libro. (11) Marca. (12) Documento sanitario.\n");
                    text.Span("(13) Causa del movimiento. (14) Código de origen. (15) Código de destino.\n");
                    text.Span("(17) Nº de hoja y orden del anexo de incidencias de identificación.");
                });
            });
        }
    }

    internal static void ComposeCensusSection(IDocumentContainer container, BookRenderContext context)
    {
        var aggregate = context.Aggregate;
        var rows = aggregate.Censuses
            .Select((census, index) => new PorcineCensusRow(
                index + 1,
                BookDocumentSupport.FormatDate(census.CensusDate),
                (census.Porcino?.Boars ?? 0).ToString(),
                (census.Porcino?.Sow ?? 0).ToString(),
                (census.Porcino?.PigsReposition ?? 0).ToString(),
                (census.Porcino?.SowsReposition ?? 0).ToString(),
                (census.Porcino?.Piglets ?? 0).ToString(),
                (census.Porcino?.Rears ?? 0).ToString(),
                (census.Porcino?.Baits ?? 0).ToString()))
            .ToList();

        foreach (var pageRows in BookDocumentSupport.ChunkOrBlank(rows, 18, PorcineCensusRow.Empty))
        {
            container.Page(page =>
            {
                BookDocumentSupport.ConfigureOfficialLedgerPage(page, aggregate, "HOJA DE CENSO TOTAL DEL GANADO PORCINO", true);
                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(16);
                            columns.RelativeColumn(9);
                            columns.RelativeColumn(9);
                            columns.RelativeColumn(13);
                            columns.RelativeColumn(13);
                            columns.RelativeColumn(13);
                            columns.RelativeColumn(13);
                            columns.RelativeColumn(14);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Fecha");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("V");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("CV");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("M. repos.");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("H. repos.");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Lechones");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Recría");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Cebo");
                        });

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Date ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Boars ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.SowsForLive ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.MalesReposition ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.SowsReposition ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Piglets ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Rears ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Baits ?? string.Empty);
                        }
                    });
                });
            });
        }
    }

    internal static void ComposeIncidentSection(IDocumentContainer container, BookRenderContext context)
    {
        var aggregate = context.Aggregate;
        var animalSheetReferences = BookDocumentSupport.BuildAnimalSheetReferenceLookup(aggregate, context.IncludedSections);
        var rows = aggregate.Incidents
            .Select((incident, index) => new PorcineIncidentRow(
                index + 1,
                BookDocumentSupport.ResolveIncidentReferencePage(incident, animalSheetReferences),
                BookDocumentSupport.ResolveIncidentReferenceOrder(incident, animalSheetReferences),
                BookDocumentSupport.FormatDate(incident.IncidentDate),
                BookDocumentSupport.EmptyToNull(incident.LastIdentification),
                BookDocumentSupport.EmptyToNull(incident.NewIdentification),
                BookDocumentSupport.EmptyToNull(incident.ChangeReason),
                incident.AnimalId is null ? string.Empty : "1"))
            .ToList();

        foreach (var pageRows in BookDocumentSupport.ChunkOrBlank(rows, 15, PorcineIncidentRow.Empty))
        {
            container.Page(page =>
            {
                BookDocumentSupport.ConfigureOfficialLedgerPage(page, aggregate, "HOJA DE ANEXO DE INCIDENCIAS DE IDENTIFICACIÓN (INDIVIDUAL / EXPLOTACIÓN) DEL GANADO PORCINO", true);
                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(46);
                            columns.ConstantColumn(44);
                            columns.ConstantColumn(44);
                            columns.ConstantColumn(96);
                            columns.ConstantColumn(100);
                            columns.ConstantColumn(100);
                            columns.RelativeColumn();
                            columns.ConstantColumn(88);
                        });

                        table.Header(header =>
                        {
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº DE\nORDEN");
                            header.Cell().ColumnSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Hoja de Identificación (1)");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Fecha del cambio\nde identificación");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Identificación\nanterior");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Identificación\nnueva");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("CAUSA (2)");
                            header.Cell().RowSpan(2).Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nº Animales\nRemarcados");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Hoja");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Orden");
                        });

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Order == 0 ? string.Empty : row.Order.ToString());
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.ReferencePage ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.ReferenceOrder ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.Date ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).Text(row.LastIdentification ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).Text(row.NewIdentification ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).Text(row.Cause ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCell).Text(row.RemarkedAnimals ?? string.Empty);
                        }
                    });
                });

                page.Footer().PaddingTop(4).Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(7));
                    text.Span("(1) Se indicará el nº de hoja y de orden de la hoja de identificación individual.\n");
                    text.Span("(2) Causa de sustitución o remarcado.");
                });
            });
        }
    }
}
