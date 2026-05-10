using System.Text.RegularExpressions;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

internal static class DomainValidators
{
    private const string DniLetters = "TRWAGMYFPDXBNJZSQVHLCKE";
    private const string CifControlLetters = "JABCDEFGHI";

    private static readonly Regex RegaCodeRegex = new("^ES\\d{12}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex OfficialAnimalIdentificationRegex = new("^ES\\d{12}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex OvineOrCaprineLegacyIdentificationRegex = new("^ES\\d{12}-[A-Z0-9]{3,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PorcineAlternativeIdentificationRegex = new("^GT\\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DniRegex = new("^\\d{8}[A-Z]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NieRegex = new("^[XYZ]\\d{7}[A-Z]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SpecialNifRegex = new("^[KLM]\\d{7}[A-Z]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CompanyTaxIdentifierRegex = new("^[ABCDEFGHJNPQRSUVW]\\d{7}[0-9A-J]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static string NormalizeRegaCode(string value) => value.Trim().ToUpperInvariant();

    internal static bool IsValidRegaCode(string value)
    {
        return RegaCodeRegex.IsMatch(NormalizeRegaCode(value));
    }

    internal static string NormalizeAnimalIdentification(string value)
    {
        var token = value.Trim().ToUpperInvariant();

        var officialMatch = Regex.Match(token, "^ES[\\s._-]*((?:\\d[\\s._-]*){12})(?:-([A-Z0-9]{3,}))?$");
        if (officialMatch.Success)
        {
            var digits = Regex.Replace(officialMatch.Groups[1].Value, "\\D", string.Empty);
            var suffix = officialMatch.Groups[2].Success ? $"-{officialMatch.Groups[2].Value}" : string.Empty;
            return $"ES{digits}{suffix}";
        }

        var porcineAlternativeMatch = Regex.Match(token, "^GT[\\s._-]*(\\d+)$");
        if (porcineAlternativeMatch.Success)
        {
            return $"GT{porcineAlternativeMatch.Groups[1].Value}";
        }

        return token;
    }

    internal static bool IsValidAnimalIdentification(LivestockSpecies species, string value)
    {
        var normalizedValue = NormalizeAnimalIdentification(value);
        return species == LivestockSpecies.Porcine
            ? OfficialAnimalIdentificationRegex.IsMatch(normalizedValue) ||
              PorcineAlternativeIdentificationRegex.IsMatch(normalizedValue)
            : OfficialAnimalIdentificationRegex.IsMatch(normalizedValue) ||
              OvineOrCaprineLegacyIdentificationRegex.IsMatch(normalizedValue);
    }

    internal static string NormalizeTaxIdentifier(string value) => value.Trim().ToUpperInvariant();

    internal static bool IsValidTaxIdentifier(PersonType personType, string value)
    {
        var normalizedValue = NormalizeTaxIdentifier(value);
        return personType == PersonType.Company
            ? IsValidCompanyTaxIdentifier(normalizedValue)
            : IsValidIndividualTaxIdentifier(normalizedValue);
    }

    private static bool IsValidIndividualTaxIdentifier(string value)
    {
        if (DniRegex.IsMatch(value))
        {
            return HasExpectedControlLetter(value[..8], value[^1]);
        }

        if (NieRegex.IsMatch(value))
        {
            var prefix = value[0] switch
            {
                'X' => "0",
                'Y' => "1",
                'Z' => "2",
                _ => string.Empty
            };

            return HasExpectedControlLetter($"{prefix}{value[1..8]}", value[^1]);
        }

        if (SpecialNifRegex.IsMatch(value))
        {
            return HasExpectedControlLetter($"0{value[1..8]}", value[^1]);
        }

        return false;
    }

    private static bool IsValidCompanyTaxIdentifier(string value)
    {
        if (!CompanyTaxIdentifierRegex.IsMatch(value))
        {
            return false;
        }

        var bodyDigits = value[1..8];
        var sum = 0;

        for (var index = 0; index < bodyDigits.Length; index++)
        {
            var digit = bodyDigits[index] - '0';
            if (index % 2 == 0)
            {
                var doubled = digit * 2;
                sum += doubled / 10 + doubled % 10;
            }
            else
            {
                sum += digit;
            }
        }

        var controlDigit = (10 - (sum % 10)) % 10;
        var expectedDigit = (char)('0' + controlDigit);
        var expectedLetter = CifControlLetters[controlDigit];
        var controlCharacter = value[^1];

        return value[0] switch
        {
            'A' or 'B' or 'E' or 'H' => controlCharacter == expectedDigit,
            'K' or 'P' or 'Q' or 'S' or 'N' or 'W' => controlCharacter == expectedLetter,
            _ => controlCharacter == expectedDigit || controlCharacter == expectedLetter
        };
    }

    private static bool HasExpectedControlLetter(string numericPart, char controlLetter)
    {
        return int.TryParse(numericPart, out var number) &&
            DniLetters[number % DniLetters.Length] == controlLetter;
    }
}
