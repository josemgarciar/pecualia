namespace Pecualia.Api.Models.Entities;

public sealed class MovementBirthLot
{
    public long MovementCertificateId { get; set; }
    public long BirthId { get; set; }
    public int Quantity { get; set; }
    public MovementCertificate MovementCertificate { get; set; } = null!;
    public AnimalBirth Birth { get; set; } = null!;
}
