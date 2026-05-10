using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class DomainValidatorsTests
{
    [Theory]
    [InlineData("ES061230000145")]
    [InlineData("es061230000145")]
    public void IsValidRegaCode_ReturnsTrue_ForValidCodes(string regaCode)
    {
        DomainValidators.IsValidRegaCode(regaCode).Should().BeTrue();
    }

    [Theory]
    [InlineData("ES06123000145")]
    [InlineData("PT061230000145")]
    [InlineData("")]
    public void IsValidRegaCode_ReturnsFalse_ForInvalidCodes(string regaCode)
    {
        DomainValidators.IsValidRegaCode(regaCode).Should().BeFalse();
    }

    [Theory]
    [InlineData(LivestockSpecies.Ovine, "ES060000583100")]
    [InlineData(LivestockSpecies.Caprine, "ES060000583100-ABC")]
    [InlineData(LivestockSpecies.Porcine, "GT1800001004")]
    public void IsValidAnimalIdentification_ReturnsTrue_ForSupportedFormats(LivestockSpecies species, string identification)
    {
        DomainValidators.IsValidAnimalIdentification(species, identification).Should().BeTrue();
    }

    [Theory]
    [InlineData(LivestockSpecies.Ovine, "GT1800001004")]
    [InlineData(LivestockSpecies.Porcine, "ES0600005831-ABC")]
    [InlineData(LivestockSpecies.Caprine, "INVALID")]
    public void IsValidAnimalIdentification_ReturnsFalse_ForUnsupportedFormats(LivestockSpecies species, string identification)
    {
        DomainValidators.IsValidAnimalIdentification(species, identification).Should().BeFalse();
    }

    [Theory]
    [InlineData(PersonType.Individual, "12345678Z")]
    [InlineData(PersonType.Individual, "X1234567L")]
    [InlineData(PersonType.Company, "B12345674")]
    public void IsValidTaxIdentifier_ReturnsTrue_ForValidIdentifiers(PersonType personType, string identifier)
    {
        DomainValidators.IsValidTaxIdentifier(personType, identifier).Should().BeTrue();
    }

    [Theory]
    [InlineData(PersonType.Individual, "12345678A")]
    [InlineData(PersonType.Individual, "X1234567A")]
    [InlineData(PersonType.Company, "B12345673")]
    public void IsValidTaxIdentifier_ReturnsFalse_ForInvalidIdentifiers(PersonType personType, string identifier)
    {
        DomainValidators.IsValidTaxIdentifier(personType, identifier).Should().BeFalse();
    }

    [Fact]
    public void NormalizeAnimalIdentification_RemovesSeparators_AndUppercasesValue()
    {
        var normalized = DomainValidators.NormalizeAnimalIdentification("es 0600.0058_3100");

        normalized.Should().Be("ES060000583100");
    }
}
