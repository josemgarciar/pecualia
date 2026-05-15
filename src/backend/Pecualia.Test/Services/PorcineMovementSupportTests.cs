using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class PorcineMovementSupportTests
{
    [Theory]
    [InlineData("Cebo", 4, 4, 0, 0, 0, 0, 0, 0)]
    [InlineData("Verracos", 2, 0, 2, 0, 0, 0, 0, 0)]
    [InlineData("Lechones", 5, 0, 0, 5, 0, 0, 0, 0)]
    [InlineData("Hembras reposición", 3, 0, 0, 0, 0, 0, 0, 3)]
    [InlineData("Machos reposición", 4, 0, 0, 0, 4, 0, 0, 0)]
    [InlineData("Cerdas vida", 1, 0, 0, 0, 0, 0, 1, 0)]
    [InlineData("Recría", 6, 0, 0, 0, 0, 6, 0, 0)]
    public void BuildBreakdown_MapsAnimalTypeToExpectedBucket(
        string animalType,
        int quantity,
        int expectedBaits,
        int expectedBoars,
        int expectedPiglets,
        int expectedPigsReposition,
        int expectedRears,
        int expectedSows,
        int expectedSowsReposition)
    {
        var breakdown = PorcineMovementSupport.BuildBreakdown(animalType, quantity);

        breakdown.Baits.Should().Be(expectedBaits);
        breakdown.Boars.Should().Be(expectedBoars);
        breakdown.Piglets.Should().Be(expectedPiglets);
        breakdown.PigsReposition.Should().Be(expectedPigsReposition);
        breakdown.Rears.Should().Be(expectedRears);
        breakdown.Sows.Should().Be(expectedSows);
        breakdown.SowsReposition.Should().Be(expectedSowsReposition);
    }

    [Fact]
    public void GetAvailableAnimals_ReturnsMatchingSnapshotBucket()
    {
        var snapshot = new FarmCensusResponse(
            null,
            10,
            2026,
            "Porcine",
            0,
            0,
            0,
            0,
            2,
            7,
            5,
            4,
            3,
            6,
            9,
            0,
            36,
            []);

        PorcineMovementSupport.GetAvailableAnimals(snapshot, "Verracos").Should().Be(2);
        PorcineMovementSupport.GetAvailableAnimals(snapshot, "Cerdas vida").Should().Be(7);
        PorcineMovementSupport.GetAvailableAnimals(snapshot, "Hembras reposición").Should().Be(5);
        PorcineMovementSupport.GetAvailableAnimals(snapshot, "Machos reposición").Should().Be(4);
        PorcineMovementSupport.GetAvailableAnimals(snapshot, "Lechones").Should().Be(3);
        PorcineMovementSupport.GetAvailableAnimals(snapshot, "Recría").Should().Be(6);
        PorcineMovementSupport.GetAvailableAnimals(snapshot, "Cebo").Should().Be(9);
    }
}
