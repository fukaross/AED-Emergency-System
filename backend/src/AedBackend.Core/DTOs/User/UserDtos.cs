using System.Text.Json.Serialization;
using AedBackend.Core.Models.Volunteer;

namespace AedBackend.Core.DTOs.User;

public sealed record UserProfileResponse(
    string Id,
    string Name,
    string AvatarUrl,
    string Gender,
    string Phone,
    bool Certified,
    string? Level,
    object? SchoolProfile,
    object? RescuerSummary);

public sealed record VolunteerImageAttachmentDto(
    string Url,
    string? FileId,
    string? Name,
    string? LocalPath);

public sealed record SubmitVolunteerApplicationRequest(
    VolunteerImageAttachmentDto? IdPhoto,
    string? Name,
    string? Gender,
    string? BirthDate,
    string? Phone,
    string? Email,
    string? PoliticalStatus,
    string? Education,
    string? Occupation,
    string? ProfessionalTitle,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    IReadOnlyList<VolunteerImageAttachmentDto>? QualificationImages,
    IReadOnlyList<VolunteerImageAttachmentDto>? VolunteerCertificateImages,
    string? IdNumber,
    VolunteerImageAttachmentDto? IdCardFront,
    VolunteerImageAttachmentDto? IdCardBack,
    string? HomeAddress,
    string? PersonalResume,
    string? JoinReason,
    bool CommitmentAccepted,
    int? DerivedFromId);

public sealed record VolunteerApplicationResponse(
    string ApplicationId,
    string Status,
    DateTimeOffset SubmittedAt);

public sealed record VolunteerApplicationListResponse(
    IReadOnlyList<VolunteerApplicationDetailResponse> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record VolunteerApplicationDetailResponse(
    string ApplicationId,
    string Status,
    int AttemptNumber,
    int? DerivedFromId,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt,
    VolunteerApplicationPayloadRecord Payload);
