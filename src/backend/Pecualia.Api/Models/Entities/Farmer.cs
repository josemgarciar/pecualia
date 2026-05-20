using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Models.Entities;

public sealed class Farmer
{
    public long UserId { get; set; }

    public long? ManagerId { get; set; }

    public string NifCif { get; set; } = string.Empty;

    public string? CompanyName { get; set; }

    public string? LegalRepresentative { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Province { get; set; }

    public string? Residence { get; set; }

    public string? Town { get; set; }

    public string? ZipCode { get; set; }

    public PersonType PersonType { get; set; }

    public DateOnly? BirthDate { get; set; }

    public FarmerStatus Status { get; set; }

    public AppUser User { get; set; } = null!;

    public Manager? Manager { get; set; }

    public ICollection<LivestockFarm> Farms { get; set; } = new List<LivestockFarm>();
}
