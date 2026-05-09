using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Models.Entities;

public sealed class AppUser
{
    public long Id { get; set; }

    public string? Email { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Surname { get; set; } = string.Empty;

    public string? Username { get; set; }

    public string? PasswordHash { get; set; }

    public UserRole Role { get; set; }

    public DateTimeOffset? EmailVerifiedAt { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Manager? Manager { get; set; }

    public Farmer? Farmer { get; set; }

    public Subscription? Subscription { get; set; }
}
