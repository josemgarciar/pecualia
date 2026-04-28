namespace Pecualia.Api.Models.Entities;

public sealed class Manager
{
    public long UserId { get; set; }

    public string OrganizationName { get; set; } = string.Empty;

    public string ProfessionalIdentifier { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? Province { get; set; }

    public string? Town { get; set; }

    public string InvitationCode { get; set; } = string.Empty;

    public AppUser User { get; set; } = null!;

    public ICollection<Farmer> Farmers { get; set; } = new List<Farmer>();
}
