namespace Pecualia.Api.Models.Entities;

public sealed class MovementCertificateAnimal
{
    public long Id { get; set; }

    public long MovementCertificateId { get; set; }

    public long AnimalId { get; set; }

    public MovementCertificate MovementCertificate { get; set; } = null!;

    public Animal Animal { get; set; } = null!;
}
