using Pecualia.Api.Contracts.Books;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Pecualia.Api.Services;

internal static class BookDocumentComposer
{
    private static readonly string[] DefaultSectionOrder = ["general", "animals", "balance", "census", "incidents", "inspections"];
    private static readonly HashSet<string> KnownSectionIds = new(DefaultSectionOrder, StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlyList<FarmBookPreviewSectionResponse> BuildSections(BookAggregate aggregate)
    {
        return BookDocumentSupport.BuildSections(aggregate);
    }

    internal static IReadOnlySet<string> ResolveIncludedSections(IReadOnlyCollection<string>? sectionIds)
    {
        if (sectionIds is null || sectionIds.Count == 0)
        {
            return new HashSet<string>(DefaultSectionOrder, StringComparer.OrdinalIgnoreCase);
        }

        var includedSections = sectionIds
            .Where(sectionId => !string.IsNullOrWhiteSpace(sectionId))
            .Select(sectionId => sectionId.Trim())
            .Where(KnownSectionIds.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (includedSections.Count == 0)
        {
            throw new DomainException("Debes seleccionar al menos un apartado del libro.");
        }

        return includedSections;
    }

    internal static void ComposeDocument(IDocumentContainer container, BookAggregate aggregate, IReadOnlySet<string> includedSections)
    {
        var context = new BookRenderContext(aggregate, includedSections);

        if (context.IsPorcine)
        {
            ComposePorcineDocument(container, context);
        }
        else
        {
            ComposeOvineDocument(container, context);
        }

        if (includedSections.Contains("inspections"))
        {
            ComposeInspectionSection(container, aggregate);
        }
    }

    private static void ComposeOvineDocument(IDocumentContainer container, BookRenderContext context)
    {
        if (context.IncludedSections.Contains("general"))
        {
            OvineBookDocumentComposer.ComposeGeneralPages(container, context);
        }

        if (context.IncludedSections.Contains("animals"))
        {
            OvineBookDocumentComposer.ComposeAnimalsSection(container, context);
        }

        if (context.IncludedSections.Contains("balance"))
        {
            OvineBookDocumentComposer.ComposeBalanceSection(container, context);
        }

        if (context.IncludedSections.Contains("census"))
        {
            OvineBookDocumentComposer.ComposeCensusSection(container, context);
        }

        if (context.IncludedSections.Contains("incidents"))
        {
            OvineBookDocumentComposer.ComposeIncidentSection(container, context);
        }
    }

    private static void ComposePorcineDocument(IDocumentContainer container, BookRenderContext context)
    {
        if (context.IncludedSections.Contains("general"))
        {
            PorcineBookDocumentComposer.ComposeGeneralPages(container, context);
        }

        if (context.IncludedSections.Contains("animals"))
        {
            PorcineBookDocumentComposer.ComposeAnimalsSection(container, context);
        }

        if (context.IncludedSections.Contains("balance"))
        {
            PorcineBookDocumentComposer.ComposeBalanceSection(container, context);
        }

        if (context.IncludedSections.Contains("census"))
        {
            PorcineBookDocumentComposer.ComposeCensusSection(container, context);
        }

        if (context.IncludedSections.Contains("incidents"))
        {
            PorcineBookDocumentComposer.ComposeIncidentSection(container, context);
        }
    }

    private static void ComposeInspectionSection(IDocumentContainer container, BookAggregate aggregate)
    {
        var rows = aggregate.Inspections
            .Select((inspection, index) => new InspectionRow(
                index + 1,
                BookDocumentSupport.EmptyToNull(inspection.Reason),
                BookDocumentSupport.EmptyToNull(inspection.Observations),
                BookDocumentSupport.JoinValues(" · ", BookDocumentSupport.EmptyToNull(inspection.Veterinary), BookDocumentSupport.FormatDate(inspection.InspectionDate))))
            .ToList();

        foreach (var pageRows in BookDocumentSupport.ChunkOrBlank(rows, 10, InspectionRow.Empty))
        {
            container.Page(page =>
            {
                BookDocumentSupport.ConfigureOfficialLedgerPage(page, aggregate, "HOJA DE CONTROL DE INSPECCIONES", aggregate.Farm.LivestockSpecies == Models.Enums.LivestockSpecies.Porcine);
                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(120);
                            columns.RelativeColumn();
                            columns.ConstantColumn(210);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Motivo (1)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Observaciones (2)");
                            header.Cell().Element(BookDocumentSupport.OfficialLedgerHeaderCell).Text("Nombre, fecha y firma del Veterinario oficial actuante");
                        });

                        foreach (var row in pageRows)
                        {
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).MinHeight(36).Text(row.Reason ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).MinHeight(36).Text(row.Observations ?? string.Empty);
                            table.Cell().Element(BookDocumentSupport.OfficialLedgerBodyCellLeft).MinHeight(36).Text(row.Signature ?? string.Empty);
                        }
                    });
                });

                page.Footer().PaddingTop(4).Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(7));
                    text.Span("(1) Indíquese lo que proceda: Control de Censo (CC); Control Infraestructuras (CI); Control de Identificación y registro (CIR); Control de PNIR (PNIR); Control serológico (CSE); Movimiento pecuario (CM); Bienestar Animal (BA); Otros (O).\n");
                    text.Span("(2) Se detallarán brevemente las posibles anomalías o deficiencias encontradas y las medidas correctoras que se hayan indicado.");
                });
            });
        }
    }
}
