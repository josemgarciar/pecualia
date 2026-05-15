using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class AnimalServiceTests
{
    [Fact]
    public void EnsureAutorrepositionSupportedSpecies_Throws_ForPorcine()
    {
        var action = () => AnimalService.EnsureAutorrepositionSupportedSpecies(LivestockSpecies.Porcine);

        action.Should().Throw<DomainException>()
            .WithMessage("La autoreposición no está disponible para explotaciones porcinas.");
    }

    [Theory]
    [InlineData(LivestockSpecies.Ovine)]
    [InlineData(LivestockSpecies.Caprine)]
    public void EnsureAutorrepositionSupportedSpecies_Allows_OvineAndCaprine(LivestockSpecies species)
    {
        var action = () => AnimalService.EnsureAutorrepositionSupportedSpecies(species);

        action.Should().NotThrow();
    }
}
