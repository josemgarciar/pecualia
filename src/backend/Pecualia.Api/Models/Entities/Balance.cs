namespace Pecualia.Api.Models.Entities;

public sealed class Balance
{
    public long Id { get; set; }

    public long LivestockFarmId { get; set; }

    public DateOnly BalanceDate { get; set; }

    public string? DestinationLivestockCode { get; set; }

    public string ModificationCause { get; set; } = string.Empty;

    public int NumberOfAnimals { get; set; }

    public string? OriginLivestockCode { get; set; }

    public LivestockFarm LivestockFarm { get; set; } = null!;

    public BalanceOvinoCaprino? OvinoCaprino { get; set; }

    public BalancePorcino? Porcino { get; set; }
}
