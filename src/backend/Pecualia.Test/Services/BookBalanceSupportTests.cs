using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class BookBalanceSupportTests
{
    [Fact]
    public void BuildBalanceMovementLookup_UsesDestinationCodeAndGuideForExitBalances()
    {
        var farm = new LivestockFarm
        {
            Id = 10,
            LivestockSpecies = LivestockSpecies.Ovine,
            RegaCode = "ES010"
        };
        var destinationFarm = new LivestockFarm
        {
            Id = 20,
            RegaCode = "ES020"
        };
        var balance = new Balance
        {
            Id = 100,
            BalanceDate = new DateOnly(2026, 5, 10),
            ModificationCause = "Salida",
            NumberOfAnimals = 12,
            DestinationLivestockCode = "ES020"
        };
        var movement = new MovementCertificate
        {
            Id = 500,
            OriginLivestockId = farm.Id,
            DestinationLivestockId = destinationFarm.Id,
            DestinationFarm = destinationFarm,
            DepartureDate = new DateTime(2026, 5, 10, 8, 0, 0),
            NumberOfAnimals = 12,
            Serie = "GUIA-EXIT-01",
            Specie = LivestockSpecies.Ovine.ToString()
        };

        var lookup = BookBalanceSupport.BuildBalanceMovementLookup(farm, [balance], [movement]);

        lookup.Should().ContainKey(balance.Id);
        lookup[balance.Id].CounterpartyCode.Should().Be("ES020");
        lookup[balance.Id].GuideNumber.Should().Be("GUIA-EXIT-01");
        BookBalanceSupport.ResolveBalanceCounterpartyCode(balance, lookup[balance.Id]).Should().Be("ES020");
        BookBalanceSupport.ResolveGuideSerie(lookup[balance.Id]).Should().Be("GUIA-EXIT-01");
    }

    [Fact]
    public void BuildBalanceMovementLookup_UsesOriginCodeAndGuideForEntryBalances()
    {
        var originFarm = new LivestockFarm
        {
            Id = 20,
            RegaCode = "ES020"
        };
        var farm = new LivestockFarm
        {
            Id = 10,
            LivestockSpecies = LivestockSpecies.Caprine,
            RegaCode = "ES010"
        };
        var balance = new Balance
        {
            Id = 101,
            BalanceDate = new DateOnly(2026, 5, 11),
            ModificationCause = "Entrada",
            NumberOfAnimals = 7,
            OriginLivestockCode = "ES020"
        };
        var movement = new MovementCertificate
        {
            Id = 501,
            OriginLivestockId = originFarm.Id,
            DestinationLivestockId = farm.Id,
            OriginFarm = originFarm,
            DepartureDate = new DateTime(2026, 5, 10, 8, 0, 0),
            ArrivalDate = new DateTime(2026, 5, 11, 9, 0, 0),
            NumberOfAnimals = 7,
            Serie = "GUIA-ENTRY-01",
            Specie = LivestockSpecies.Caprine.ToString()
        };

        var lookup = BookBalanceSupport.BuildBalanceMovementLookup(farm, [balance], [movement]);

        lookup.Should().ContainKey(balance.Id);
        lookup[balance.Id].CounterpartyCode.Should().Be("ES020");
        lookup[balance.Id].GuideNumber.Should().Be("GUIA-ENTRY-01");
        BookBalanceSupport.ResolveBalanceCounterpartyCode(balance, lookup[balance.Id]).Should().Be("ES020");
        BookBalanceSupport.ResolveGuideSerie(lookup[balance.Id]).Should().Be("GUIA-ENTRY-01");
    }

    [Fact]
    public void ResolveBalanceFields_FallBackToStoredBalanceValues_WhenNoMovementIsMatched()
    {
        var exitBalance = new Balance
        {
            ModificationCause = "Salida",
            DestinationLivestockCode = "ES999"
        };
        var entryBalance = new Balance
        {
            ModificationCause = "Entrada",
            OriginLivestockCode = "ES111"
        };

        BookBalanceSupport.ResolveBalanceCounterpartyCode(exitBalance, null).Should().Be("ES999");
        BookBalanceSupport.ResolveBalanceCounterpartyCode(entryBalance, null).Should().Be("ES111");
        BookBalanceSupport.ResolveGuideSerie(null).Should().BeNull();
    }
}
