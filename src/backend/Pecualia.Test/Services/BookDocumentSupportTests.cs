using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class BookDocumentSupportTests
{
    [Theory]
    [InlineData(LivestockSpecies.Ovine)]
    [InlineData(LivestockSpecies.Caprine)]
    public void OtherBreed_IsListedNormalizedAndMappedToOfficialBookCode(LivestockSpecies species)
    {
        BookDocumentSupport.GetBreedCodes(species)
            .Should().ContainSingle(option => option.Key == "Otras" && option.Value == "O");
        BookDocumentSupport.TryNormalizeBreed(species, " otras ", out var normalized).Should().BeTrue();
        normalized.Should().Be("Otras");
        BookDocumentSupport.MapBreedCode(species, normalized).Should().Be("O");
    }

    [Theory]
    [InlineData("Verracos", "V")]
    [InlineData("Cerdas vida", "CV")]
    [InlineData("Machos reposición", "MR")]
    [InlineData("Hembras reposición", "HR")]
    [InlineData("Cebo", "C")]
    [InlineData("Recría", "Rec")]
    [InlineData("Lechones", "L")]
    public void MapPorcineTypeCode_ReturnsOfficialCode_ForKnownAnimalTypes(string animalType, string expectedCode)
    {
        var code = BookDocumentSupport.MapPorcineTypeCode(animalType);

        code.Should().Be(expectedCode);
    }

    [Fact]
    public void MapPorcineTypeCode_FallsBackToSingleCode_WhenRowHasMixedTransition()
    {
        var detail = new BalancePorcino
        {
            Piglets = 6,
            Rear = 4,
            SowsReposition = 2,
            PigsReposition = 1
        };

        var code = BookDocumentSupport.MapPorcineTypeCode(null, detail);

        code.Should().Be("L");
    }
}
