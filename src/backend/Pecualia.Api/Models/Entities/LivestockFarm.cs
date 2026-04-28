using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Models.Entities;

public sealed class LivestockFarm
{
    public long Id { get; set; }

    public long FarmerId { get; set; }

    public int? AuthorisedCapacity { get; set; }

    public string? Address { get; set; }

    public FarmStatus Status { get; set; } = FarmStatus.Active;

    public LivestockSpecies LivestockSpecies { get; set; }

    public string? LivestockType { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ProductionCapacity { get; set; }

    public string? Province { get; set; }

    public string RegaCode { get; set; } = string.Empty;

    public FarmRegime? Regime { get; set; }

    public string? Responsible { get; set; }

    public int? Spindle { get; set; }

    public string? Town { get; set; }

    public double? XCoordinate { get; set; }

    public double? YCoordinate { get; set; }

    public string? ZipCode { get; set; }

    public string? ZootechnicClassification { get; set; }

    public Farmer Farmer { get; set; } = null!;

    public ICollection<Animal> Animals { get; set; } = new List<Animal>();
}
