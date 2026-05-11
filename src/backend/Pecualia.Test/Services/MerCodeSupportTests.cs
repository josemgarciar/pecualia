using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class MerCodeSupportTests
{
    [Fact]
    public void NormalizeDeathDestinationCode_AcceptsSandach_ForOvine()
    {
        var result = MerCodeSupport.NormalizeDeathDestinationCode(LivestockSpecies.Ovine, "sandach", 2026);

        result.Should().Be("SANDACH");
    }

    [Fact]
    public void NormalizeDeathDestinationCode_AcceptsCurrentYearMerCode()
    {
        var result = MerCodeSupport.NormalizeDeathDestinationCode(LivestockSpecies.Porcine, "ar26-1234567", 2026);

        result.Should().Be("AR26-1234567");
    }

    [Fact]
    public void NormalizeDeathDestinationCode_RejectsLiteralMer()
    {
        var action = () => MerCodeSupport.NormalizeDeathDestinationCode(LivestockSpecies.Porcine, "MER", 2026);

        action.Should().Throw<DomainException>()
            .WithMessage("Debes indicar un número MER válido con formato AR26-1234567.");
    }

    [Fact]
    public void NormalizeDeathDestinationCode_RejectsMerCodeFromAnotherYear()
    {
        var action = () => MerCodeSupport.NormalizeDeathDestinationCode(LivestockSpecies.Caprine, "AR25-1234567", 2026);

        action.Should().Throw<DomainException>()
            .WithMessage("El destino de una baja por muerte debe ser SANDACH o un número MER válido con formato AR26-1234567.");
    }
}
