using Pecualia.Api.Contracts.Animals;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Contracts.Movements;

public sealed record FarmMovementListItemResponse(
    long Id,
    string CodRemo,
    string? Serie,
    string Direction,
    string CounterpartyName,
    string CounterpartyCode,
    string LivestockSpecies,
    int NumberOfAnimals,
    DateOnly DepartureDate,
    DateOnly? ArrivalDate,
    string? TransportName,
    string? VehicleRegistrationNumber,
    string Status);

public sealed record MovementDetailResponse(
    long Id,
    long? OriginFarmId,
    long? DestinationFarmId,
    string CodRemo,
    string? Serie,
    string LivestockSpecies,
    DateOnly DepartureDate,
    DateOnly? ArrivalDate,
    DateOnly? SolicitationDate,
    string? MeansOfTransport,
    string? TransportName,
    string? VehicleRegistrationNumber,
    string? OriginName,
    string? OriginCode,
    string? DestinationName,
    string? DestinationCode,
    int NumberOfAnimals,
    string Status,
    IReadOnlyList<MovementAnimalItemResponse> Animals);

public sealed record MovementAnimalItemResponse(
    long AnimalId,
    string Identification,
    string? Breed,
    string? Sex,
    int? BirthYear,
    string Status);

public sealed record CreateManualMovementRequest(
    long FarmId,
    MovementDirection Direction,
    MovementCounterpartyType CounterpartyType,
    long? CounterpartyFarmId,
    string? CounterpartyExternalCode,
    string? CounterpartyExternalName,
    string CodRemo,
    string? Serie,
    DateOnly DepartureDate,
    DateOnly? ArrivalDate,
    DateOnly? SolicitationDate,
    string? MeansOfTransport,
    string? TransportName,
    string? VehicleRegistrationNumber,
    string? HealthDocumentNumber,
    string Cause,
    IReadOnlyList<long>? AnimalIds,
    IReadOnlyList<string>? Identifications,
    SharedAnimalDataRequest? SharedAnimalData);

public sealed record PreviewMovementImportRequest(
    long FarmId,
    MovementImportOperation Operation,
    string? CounterpartyExternalCode,
    string? CounterpartyExternalName,
    string? CodRemo,
    string? Serie,
    DateOnly DepartureDate,
    DateOnly? ArrivalDate,
    DateOnly? SolicitationDate,
    string? MeansOfTransport,
    string? TransportName,
    string? VehicleRegistrationNumber,
    string? HealthDocumentNumber,
    MovementImportCause Cause,
    string RawText,
    SharedAnimalDataRequest? SharedAnimalData);

public sealed record MovementImportPreviewResponse(
    string LivestockSpecies,
    bool RequiresSharedAnimalData,
    IReadOnlyList<MovementImportPreviewRowResponse> Rows,
    MovementImportPreviewSummaryResponse Summary);

public sealed record CommitMovementImportRequest(
    long FarmId,
    MovementImportOperation Operation,
    string? CounterpartyExternalCode,
    string? CounterpartyExternalName,
    string? CodRemo,
    string? Serie,
    DateOnly DepartureDate,
    DateOnly? ArrivalDate,
    DateOnly? SolicitationDate,
    string? MeansOfTransport,
    string? TransportName,
    string? VehicleRegistrationNumber,
    string? HealthDocumentNumber,
    MovementImportCause Cause,
    string RawText,
    SharedAnimalDataRequest? SharedAnimalData);

public sealed record MovementImportCommitResponse(
    long MovementId,
    string? CodRemo,
    int ProcessedRows,
    int RejectedRows,
    bool SharedAnimalDataUsed,
    MovementImportPreviewSummaryResponse Summary);

public sealed record MovementImportPreviewSummaryResponse(
    int TotalLines,
    int UniqueLines,
    int ValidRows,
    int DuplicateRows,
    int InvalidFormatRows,
    int ExistingRows,
    int NotFoundRows,
    int ConflictRows);

public sealed record MovementImportPreviewRowResponse(
    int LineNumber,
    string Identification,
    string Status,
    string Action,
    string Message,
    string? AnimalDescription,
    long? AnimalId);

public sealed record SharedAnimalDataRequest(
    int? BirthYear,
    string? Breed,
    string? Sex,
    AnimalRegistrationCause? RegistrationCause,
    string? OriginCode,
    string? HealthDocumentNumber,
    OvinoCaprinoAnimalRequest? OvinoCaprino,
    PorcinoAnimalRequest? Porcino);
