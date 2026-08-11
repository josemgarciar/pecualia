namespace Pecualia.Api.Models.Entities;

public sealed class AnimalBulkUpdateOperation
{
    public Guid Id { get; set; }

    public long UserId { get; set; }

    public long FarmId { get; set; }

    public string RequestHash { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string? ResultJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
