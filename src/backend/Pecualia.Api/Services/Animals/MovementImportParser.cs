using System.Text.RegularExpressions;
using Pecualia.Api.Contracts.Movements;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

internal static class MovementImportParser
{
    private static readonly Regex SpanishOfficialIdentificationFinderRegex = new("ES[\\s._-]*(?:\\d[\\s._-]*){12}(?:-[A-Z0-9]{3,})?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PorcineAlternativeIdentificationFinderRegex = new("\\bGT[\\s._-]*\\d+\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static ParsedMovementImport ParsePreviewLines(
        LivestockSpecies species,
        MovementDirection direction,
        IReadOnlyList<ParsedMovementIdentificationLine> parsedLines)
    {
        var deduplicated = new Dictionary<string, ParsedMovementIdentificationLine>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<MovementImportPreviewRowResponse>(parsedLines.Count);

        foreach (var line in parsedLines)
        {
            var columns = line.Value.TrimEnd().Split('\t');
            var identification = NormalizeIdentification(columns[0].TrimStart('\uFEFF'));
            if (!DomainValidators.IsValidAnimalIdentification(species, identification))
            {
                rows.Add(BuildRejectedRow(line.LineNumber, identification, "invalid_format", BuildIdentificationFormatMessage(species)));
                continue;
            }

            if (deduplicated.TryGetValue(identification, out var firstOccurrence))
            {
                rows.Add(BuildRejectedRow(line.LineNumber, identification, "duplicate",
                    $"Identificación duplicada. Primera aparición en la línea {firstOccurrence.LineNumber}."));
                continue;
            }

            SharedAnimalDataRequest? animalData = null;
            if (columns.Length > 1 && direction == MovementDirection.Entry)
            {
                try
                {
                    animalData = ParseGuideAnimalData(species, columns);
                }
                catch (DomainException exception)
                {
                    rows.Add(BuildRejectedRow(line.LineNumber, identification, "invalid_format", exception.Message));
                    continue;
                }
            }

            deduplicated[identification] = new ParsedMovementIdentificationLine(line.LineNumber, identification, animalData);
        }

        return new ParsedMovementImport(deduplicated.Values.ToList(), rows);
    }

    private static MovementImportPreviewRowResponse BuildRejectedRow(
        int lineNumber, string identification, string status, string message) =>
        new(lineNumber, identification, status, "Excluido", message, null, null);

    private static string NormalizeIdentification(string value)
    {
        var normalizedLine = value.Trim().ToUpperInvariant();
        var officialMatch = SpanishOfficialIdentificationFinderRegex.Match(normalizedLine);
        if (officialMatch.Success)
        {
            return NormalizeIdentifierToken(officialMatch.Value);
        }

        var porcineAlternativeMatch = PorcineAlternativeIdentificationFinderRegex.Match(normalizedLine);
        if (porcineAlternativeMatch.Success)
        {
            return NormalizeIdentifierToken(porcineAlternativeMatch.Value);
        }

        var normalizedWholeLine = DomainValidators.NormalizeAnimalIdentification(normalizedLine);
        if (!string.Equals(normalizedWholeLine, normalizedLine, StringComparison.Ordinal))
        {
            return normalizedWholeLine;
        }

        var firstToken = normalizedLine
            .Split(new[] { ' ', '\t', ',', ';', '|', ':', '#', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;

        return NormalizeIdentifierToken(firstToken);
    }

    private static string BuildIdentificationFormatMessage(LivestockSpecies species)
    {
        return species == LivestockSpecies.Porcine
            ? "Formato inválido. Para porcino se espera ES seguido de 12 dígitos o GT seguido de números."
            : "Formato inválido. Para ovino/caprino se espera ES seguido de 12 dígitos o ES seguido de 12 dígitos con sufijo.";
    }

    private static string NormalizeIdentifierToken(string value)
    {
        return DomainValidators.NormalizeAnimalIdentification(value)
            .Replace(" ", string.Empty)
            .Replace("\t", string.Empty)
            .Replace(".", string.Empty)
            .Replace("_", string.Empty);
    }

    private static SharedAnimalDataRequest ParseGuideAnimalData(LivestockSpecies species, string[] columns)
    {
        if (columns.Length < 4)
        {
            throw new DomainException("La fila debe contener crotal, fecha de nacimiento, sexo y raza separados por tabulaciones.");
        }

        var birthDate = ParseGuideBirthDate(columns[1]);
        var sex = ParseGuideSex(columns[2]);
        var breed = NormalizeOfficialBreed(species, columns[3].Trim());
        return new SharedAnimalDataRequest(birthDate, birthDate.Year, breed, sex, null, null, null);
    }

    private static DateOnly ParseGuideBirthDate(string value)
    {
        const string invalidDateMessage = "La fecha de nacimiento no es válida. Usa día/mes/año (por ejemplo, 1/12/16 o 01/12/2016).";
        var dateMatch = Regex.Match(value.Trim(), @"^(\d{1,2})/(\d{1,2})/(\d{2}|\d{4})$");
        if (!dateMatch.Success)
        {
            throw new DomainException(invalidDateMessage);
        }

        var year = int.Parse(dateMatch.Groups[3].Value);
        // The guide's two-digit years represent years in the 2000s (14 = 2014).
        if (dateMatch.Groups[3].Length == 2)
        {
            year += 2000;
        }

        try
        {
            return new DateOnly(year, int.Parse(dateMatch.Groups[2].Value), int.Parse(dateMatch.Groups[1].Value));
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new DomainException(invalidDateMessage);
        }
    }

    private static string ParseGuideSex(string value) => value.Trim().ToLowerInvariant() switch
    {
        "hembra" or "h" or "female" => "Female",
        "macho" or "m" or "male" => "Male",
        _ => throw new DomainException("El sexo no es válido. Indica Hembra o Macho.")
    };

    internal static string NormalizeOfficialBreed(LivestockSpecies species, string? breed)
    {
        if (BookDocumentSupport.TryNormalizeBreed(species, breed, out var normalizedBreed) &&
            !string.IsNullOrWhiteSpace(normalizedBreed))
        {
            return normalizedBreed;
        }

        throw new DomainException("La raza indicada no es válida para la especie de la guía.");
    }

    internal static IReadOnlyList<ParsedMovementIdentificationLine> ParseLines(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return [];
        }

        return ParseLines(rawText.Split(["\r\n", "\n", "\r"], StringSplitOptions.None));
    }

    internal static IReadOnlyList<ParsedMovementIdentificationLine> ParseLines(IReadOnlyList<string>? rawLines)
    {
        if (rawLines is null || rawLines.Count == 0)
        {
            return [];
        }

        return rawLines
            .Select((entity, index) => new ParsedMovementIdentificationLine(index + 1, entity))
            .Where(entity => !string.IsNullOrWhiteSpace(entity.Value))
            .ToList();
    }
}

internal sealed record ParsedMovementIdentificationLine(int LineNumber, string Value, SharedAnimalDataRequest? AnimalData = null);

internal sealed record ParsedMovementImport(
    IReadOnlyList<ParsedMovementIdentificationLine> Lines,
    IReadOnlyList<MovementImportPreviewRowResponse> RejectedRows);
