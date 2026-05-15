using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class PorcineTransitionSupportTests
{
    [Fact]
    public void IsPigletStage_ReturnsTrue_UntilThreeMonthsExclusive()
    {
        var birthDate = new DateOnly(2026, 02, 16);
        var asOfDate = new DateOnly(2026, 05, 15);

        var isPiglet = PorcineTransitionSupport.IsPigletStage(birthDate, asOfDate);

        isPiglet.Should().BeTrue();
    }

    [Fact]
    public void IsPigletStage_ReturnsFalse_WhenThreeMonthsAreReached()
    {
        var birthDate = new DateOnly(2026, 02, 15);
        var asOfDate = new DateOnly(2026, 05, 15);

        var isPiglet = PorcineTransitionSupport.IsPigletStage(birthDate, asOfDate);

        isPiglet.Should().BeFalse();
    }

    [Fact]
    public void IsIntermediateStage_ReturnsTrue_BetweenThreeAndSixMonths()
    {
        var birthDate = new DateOnly(2026, 01, 20);
        var asOfDate = new DateOnly(2026, 05, 15);

        var isIntermediate = PorcineTransitionSupport.IsIntermediateStage(birthDate, asOfDate);

        isIntermediate.Should().BeTrue();
    }

    [Fact]
    public void IsIntermediateStage_ReturnsFalse_WhenSixMonthsAreReached()
    {
        var birthDate = new DateOnly(2025, 11, 15);
        var asOfDate = new DateOnly(2026, 05, 15);

        var isIntermediate = PorcineTransitionSupport.IsIntermediateStage(birthDate, asOfDate);

        isIntermediate.Should().BeFalse();
    }
}
