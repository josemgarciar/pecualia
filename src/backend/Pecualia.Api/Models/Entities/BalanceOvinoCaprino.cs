namespace Pecualia.Api.Models.Entities;

public sealed class BalanceOvinoCaprino
{
    public long BalanceId { get; set; }

    public int NonReproductiveBetween4And12Months { get; set; }

    public int NonReproductiveUnder4Months { get; set; }

    public int ReproductiveFemales { get; set; }

    public int ReproductiveMales { get; set; }

    public string? TransportTicketNumber { get; set; }

    public string? TransporterName { get; set; }

    public Balance Balance { get; set; } = null!;
}
