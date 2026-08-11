using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Contracts.Animals;

public enum BulkAnimalSelectionMode
{
    Explicit = 1,
    Filtered = 2
}

public enum BulkFieldChangeMode
{
    Unchanged = 0,
    Set = 1,
    Clear = 2
}

public enum BulkGuideAction
{
    Unchanged = 0,
    SetEntry = 1,
    SetExit = 2,
    ClearLatestEntry = 3,
    ClearLatestExit = 4
}

public sealed record BulkAnimalSelectionRequest(
    BulkAnimalSelectionMode Mode,
    IReadOnlyList<long>? AnimalIds,
    string? Search,
    long? MovementId,
    string? Sex,
    string? Status,
    IReadOnlyList<long>? ExcludedAnimalIds);

public sealed record BulkRegistrationCauseChange(
    BulkFieldChangeMode Mode,
    AnimalRegistrationCause? Value);

public sealed record BulkDischargeCauseChange(
    BulkFieldChangeMode Mode,
    AnimalDischargeCause? Value);

public sealed record BulkDateChange(
    BulkFieldChangeMode Mode,
    DateOnly? Value);

public sealed record BulkAnimalGuideRequest(
    BulkGuideAction Action,
    MovementCounterpartyType? CounterpartyType,
    long? CounterpartyFarmId,
    string? CounterpartyExternalCode,
    string? CounterpartyExternalName,
    string? CodRemo,
    string? Serie,
    DateTime? DepartureDate,
    DateTime? ArrivalDate,
    DateTime? SolicitationDate,
    string? MeansOfTransport,
    string? TransportName,
    string? VehicleRegistrationNumber);

public sealed record BulkAnimalUpdateDefinition(
    BulkRegistrationCauseChange RegistrationCause,
    BulkDischargeCauseChange DischargeCause,
    BulkDateChange RegistrationDate,
    BulkDateChange DischargeDate,
    BulkAnimalGuideRequest Guide);

public sealed record PreviewAnimalBulkUpdateRequest(
    BulkAnimalSelectionRequest Selection,
    BulkAnimalUpdateDefinition Changes);

public sealed record CommitAnimalBulkUpdateRequest(
    Guid OperationId,
    IReadOnlyList<long> AnimalIds,
    string StateFingerprint,
    BulkAnimalUpdateDefinition Changes);

public sealed record AnimalBulkUpdateRowResponse(
    long AnimalId,
    string Identification,
    string? CurrentRegistrationCause,
    DateOnly? CurrentRegistrationDate,
    string? CurrentDischargeCause,
    DateOnly? CurrentDischargeDate,
    string? ResultRegistrationCause,
    DateOnly? ResultRegistrationDate,
    string? ResultDischargeCause,
    DateOnly? ResultDischargeDate,
    bool IsValid,
    string? Message);

public sealed record AnimalBulkGuidePreviewResponse(
    string Action,
    long? MovementId,
    string? Resolution,
    int AffectedAnimals);

public sealed record AnimalBulkUpdatePreviewResponse(
    IReadOnlyList<long> ResolvedAnimalIds,
    string StateFingerprint,
    IReadOnlyList<AnimalBulkUpdateRowResponse> Rows,
    AnimalBulkGuidePreviewResponse Guide,
    int TotalAnimals,
    int ValidAnimals,
    int ConflictAnimals);

public sealed record AnimalBulkUpdateCommitResponse(
    Guid OperationId,
    int UpdatedAnimals,
    int LinkedAnimals,
    int UnlinkedAnimals,
    long? MovementId,
    string GuideAction,
    bool Replayed);
