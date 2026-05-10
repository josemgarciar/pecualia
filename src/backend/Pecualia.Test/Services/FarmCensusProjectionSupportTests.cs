using Pecualia.Api.Models.Entities;
using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class FarmCensusProjectionSupportTests
{
    [Fact]
    public void ResolveBirthDate_PrefersExactBirthDate_WhenPresent()
    {
        var animal = new Animal
        {
            BirthDate = new DateOnly(2026, 2, 14),
            BirthYear = 2024
        };

        var birthDate = FarmCensusProjectionSupport.ResolveBirthDate(animal);

        birthDate.Should().Be(new DateOnly(2026, 2, 14));
    }

    [Fact]
    public void ResolveBirthDate_FallsBackToFirstDayOfBirthYear()
    {
        var animal = new Animal
        {
            BirthYear = 2025
        };

        var birthDate = FarmCensusProjectionSupport.ResolveBirthDate(animal);

        birthDate.Should().Be(new DateOnly(2025, 1, 1));
    }

    [Fact]
    public void ResolveBirthYear_UsesBirthDateYear_WhenAvailable()
    {
        var animal = new Animal
        {
            BirthDate = new DateOnly(2026, 3, 1),
            BirthYear = 2020
        };

        var birthYear = FarmCensusProjectionSupport.ResolveBirthYear(animal);

        birthYear.Should().Be(2026);
    }

    [Fact]
    public void IsYoungerThanMonths_ReturnsFalse_WhenAnimalHasExactlyReachedThreshold()
    {
        var birthDate = new DateOnly(2026, 1, 10);
        var asOfDate = new DateOnly(2026, 5, 10);

        var isYounger = FarmCensusProjectionSupport.IsYoungerThanMonths(birthDate, asOfDate, 4);

        isYounger.Should().BeFalse();
    }

    [Fact]
    public void IsYoungerThanMonths_ReturnsTrue_WhenThresholdIsNotReached()
    {
        var birthDate = new DateOnly(2026, 1, 11);
        var asOfDate = new DateOnly(2026, 5, 10);

        var isYounger = FarmCensusProjectionSupport.IsYoungerThanMonths(birthDate, asOfDate, 4);

        isYounger.Should().BeTrue();
    }
}
