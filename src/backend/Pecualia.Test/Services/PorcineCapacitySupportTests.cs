using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class PorcineCapacitySupportTests
{
    [Fact]
    public void EnsureWithinCapacity_Throws_WhenMothersExceedFarmCapacity()
    {
        var farm = new LivestockFarm
        {
            LivestockSpecies = LivestockSpecies.Porcine,
            PorcineMothersCapacity = 10,
            PorcineFatteningCapacity = 50
        };
        var snapshot = BuildSnapshot(sowsForLive: 10, sowsReposition: 2, malesReposition: 3, baits: 4);

        var action = () => PorcineCapacitySupport.EnsureWithinCapacity(farm, snapshot, "Cerda", 1);

        action.Should().Throw<DomainException>()
            .WithMessage("No puedes agregar más madres que la capacidad máxima autorizada de madres.");
    }

    [Fact]
    public void EnsureWithinCapacity_Throws_WhenFatteningExceedsFarmCapacity()
    {
        var farm = new LivestockFarm
        {
            LivestockSpecies = LivestockSpecies.Porcine,
            PorcineMothersCapacity = 20,
            PorcineFatteningCapacity = 12
        };
        var snapshot = BuildSnapshot(sowsForLive: 6, sowsReposition: 4, malesReposition: 3, baits: 5);

        var action = () => PorcineCapacitySupport.EnsureWithinCapacity(farm, snapshot, "Macho reposición", 1);

        action.Should().Throw<DomainException>()
            .WithMessage("No puedes superar la capacidad máxima autorizada de cebo (machos autoreposición + hembras autoreposición + cebo).");
    }

    [Theory]
    [InlineData("Cerda", 1)]
    [InlineData("Cerda reposición", 2)]
    [InlineData("Macho reposición", 2)]
    [InlineData("Cebo", 2)]
    [InlineData("Verraco", 0)]
    public void ResolveBucket_ClassifiesPorcineTypes(string animalType, int expectedBucket)
    {
        var bucket = PorcineCapacitySupport.ResolveBucket(animalType);

        ((int)bucket).Should().Be(expectedBucket);
    }

    private static FarmCensusResponse BuildSnapshot(int sowsForLive, int sowsReposition, int malesReposition, int baits)
    {
        return new FarmCensusResponse(
            null,
            1,
            2026,
            LivestockSpecies.Porcine.ToString(),
            0,
            0,
            0,
            0,
            0,
            sowsForLive,
            sowsReposition,
            malesReposition,
            0,
            0,
            baits,
            sowsForLive + sowsReposition + malesReposition + baits,
            []);
    }
}
