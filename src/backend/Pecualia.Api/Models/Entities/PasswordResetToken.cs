namespace Pecualia.Api.Models.Entities;

public sealed class PasswordResetToken
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public AppUser User { get; set; } = null!;
}
