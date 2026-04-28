namespace Pecualia.Api.Models.Entities;

public sealed class Inspection
{
    public long Id { get; set; }

    public long LivestockFarmId { get; set; }

    public DateOnly InspectionDate { get; set; }

    public string? Reason { get; set; }

    public string? Observations { get; set; }

    public string? Veterinary { get; set; }

    public int? TaggedAnimals { get; set; }

    public LivestockFarm LivestockFarm { get; set; } = null!;
}
