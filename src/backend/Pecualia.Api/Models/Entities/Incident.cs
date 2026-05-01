namespace Pecualia.Api.Models.Entities;

public sealed class Incident
{
    public long Id { get; set; }

    public long LivestockFarmId { get; set; }

    public long? AnimalId { get; set; }

    public string? ChangeReason { get; set; }

    public string? Description { get; set; }

    public DateOnly IncidentDate { get; set; }

    public string? LastIdentification { get; set; }

    public string? NewIdentification { get; set; }

    public LivestockFarm LivestockFarm { get; set; } = null!;

    public Animal? Animal { get; set; }
}
