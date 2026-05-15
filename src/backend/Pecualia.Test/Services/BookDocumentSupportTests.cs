using Pecualia.Api.Models.Entities;
using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class BookDocumentSupportTests
{
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
