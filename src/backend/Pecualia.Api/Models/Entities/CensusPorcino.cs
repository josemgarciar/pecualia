namespace Pecualia.Api.Models.Entities;

public sealed class CensusPorcino
{
    public long CensusId { get; set; }

    public int Baits { get; set; }

    public int Boars { get; set; }

    public int Piglets { get; set; }

    public int PigsReposition { get; set; }

    public int Rears { get; set; }

    public int Sow { get; set; }

    public int SowsReposition { get; set; }

    public Census Census { get; set; } = null!;
}
