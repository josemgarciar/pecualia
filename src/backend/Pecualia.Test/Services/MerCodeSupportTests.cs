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
    public void NormalizeDeathDestinationCode_RejectsSandach_ForCaprine()
    {
        var action = () => MerCodeSupport.NormalizeDeathDestinationCode(LivestockSpecies.Caprine, "SANDACH", 2026);

        action.Should().Throw<DomainException>()
            .WithMessage("En ganado caprino, una baja por muerte solo puede registrarse con un número MER válido.");
    }

    [Fact]
    public void NormalizeDeathDestinationCode_RejectsMerCodeFromAnotherYear_ForCaprine()
    {
        var action = () => MerCodeSupport.NormalizeDeathDestinationCode(LivestockSpecies.Caprine, "AR25-1234567", 2026);

        action.Should().Throw<DomainException>()
            .WithMessage("Debes indicar un número MER válido con formato AR26-1234567.");
    }
}
