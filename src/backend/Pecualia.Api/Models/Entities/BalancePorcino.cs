namespace Pecualia.Api.Models.Entities;

public sealed class BalancePorcino
{
    public long BalanceId { get; set; }

    public int Baits { get; set; }

    public int Boars { get; set; }

    public string? Breed { get; set; }

    public int Piglets { get; set; }

    public int PigsReposition { get; set; }

    public int Rear { get; set; }

    public int SowsForLive { get; set; }

    public int SowsReposition { get; set; }

    public string? Tag { get; set; }

    public string? Type { get; set; }

    public Balance Balance { get; set; } = null!;
}
