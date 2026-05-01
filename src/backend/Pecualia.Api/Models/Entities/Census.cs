namespace Pecualia.Api.Models.Entities;

public sealed class Census
{
    public long Id { get; set; }

    public long LivestockFarmId { get; set; }

    public DateOnly CensusDate { get; set; }

    public LivestockFarm LivestockFarm { get; set; } = null!;

    public CensusOvinoCaprino? OvinoCaprino { get; set; }

    public CensusPorcino? Porcino { get; set; }
}
