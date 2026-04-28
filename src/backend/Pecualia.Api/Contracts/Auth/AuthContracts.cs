using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Contracts.Auth;

public sealed record RegisterManagerRequest(
    string Name,
    string Surname,
    string Email,
    string Username,
    string Password,
    string OrganizationName,
    string ProfessionalIdentifier,
    string? PhoneNumber,
    string? Province,
    string? Town,
    PlanType PlanType);

public sealed record RegisterFarmerRequest(
    string Name,
    string Surname,
    string Email,
    string Username,
    string Password,
    string NifCif,
    string? PhoneNumber,
    string? Residence,
    string? Town,
    string? Province,
    string? ZipCode,
    PersonType PersonType,
    DateOnly? BirthDate,
    string? ManagerInvitationCode,
    string? ManagerEmail);

public sealed record LoginRequest(string Identifier, string Password);

public sealed record ActivateAccountRequest(string Token, string Username, string Password);

public sealed record ResendActivationRequest(string Email);

public sealed record AuthResponse(string Token, UserProfileResponse User);

public sealed record ActivationResponse(string Message, string? ActivationUrl);

public sealed record UserProfileResponse(
    long Id,
    string Email,
    string? Username,
    string Name,
    string Surname,
    string Role,
    bool IsActive,
    string? OrganizationName,
    string? FarmerStatus);
