using System.Text.RegularExpressions;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

internal static partial class MerCodeSupport
{
    [GeneratedRegex(@"^AR(?<year>\d{2})-(?<sequence>\d{7})$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MerCodeRegex();

    internal static string NormalizeDeathDestinationCode(LivestockSpecies species, string? destinationCode, int currentYear)
    {
        var normalizedDestinationCode = Normalize(destinationCode)?.ToUpperInvariant();

        if (normalizedDestinationCode is null)
        {
            throw new DomainException("El destino de una baja por muerte es obligatorio.");
        }

        if (normalizedDestinationCode == "SANDACH")
        {
            if (species is LivestockSpecies.Porcine or LivestockSpecies.Caprine)
            {
                throw new DomainException(species == LivestockSpecies.Porcine
                    ? "En ganado porcino, una baja por muerte solo puede registrarse con un número MER válido."
                    : "En ganado caprino, una baja por muerte solo puede registrarse con un número MER válido.");
            }

            return normalizedDestinationCode;
        }

        if (TryNormalizeMerCode(normalizedDestinationCode, currentYear, out var merCode))
        {
            return merCode;
        }

        var currentYearSuffix = GetYearSuffix(currentYear);
        throw species is LivestockSpecies.Porcine or LivestockSpecies.Caprine
            ? new DomainException($"Debes indicar un número MER válido con formato AR{currentYearSuffix}-1234567.")
            : new DomainException($"El destino de una baja por muerte debe ser SANDACH o un número MER válido con formato AR{currentYearSuffix}-1234567.");
    }

    internal static bool IsMerCode(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && MerCodeRegex().IsMatch(value.Trim().ToUpperInvariant());
    }

    internal static string BuildMerCodeExample(int currentYear)
    {
        return $"AR{GetYearSuffix(currentYear)}-1234567";
    }

    private static bool TryNormalizeMerCode(string value, int currentYear, out string merCode)
    {
        var match = MerCodeRegex().Match(value);
        if (!match.Success)
        {
            merCode = string.Empty;
            return false;
        }

        var expectedYear = GetYearSuffix(currentYear);
        if (!string.Equals(match.Groups["year"].Value, expectedYear, StringComparison.Ordinal))
        {
            merCode = string.Empty;
            return false;
        }

        merCode = $"AR{match.Groups["year"].Value}-{match.Groups["sequence"].Value}";
        return true;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GetYearSuffix(int currentYear)
    {
        return Math.Abs(currentYear % 100).ToString("00");
    }
}
