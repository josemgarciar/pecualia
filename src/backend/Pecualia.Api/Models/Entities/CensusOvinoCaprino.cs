namespace Pecualia.Api.Models.Entities;

public sealed class CensusOvinoCaprino
{
    public long CensusId { get; set; }

    public int NonReproductiveBetween4And12Months { get; set; }

    public int NonReproductiveUnder4Months { get; set; }

    public int ReproductiveFemale { get; set; }

    public int ReproductiveMale { get; set; }

    public Census Census { get; set; } = null!;
}
