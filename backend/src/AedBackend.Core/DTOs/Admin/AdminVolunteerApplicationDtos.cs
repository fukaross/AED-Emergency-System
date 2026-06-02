using AedBackend.Core.Models.Volunteer;

namespace AedBackend.Core.DTOs.Admin;

public sealed record AdminVolunteerApplicationListItem(
    string ApplicationId,
    string OpenId,
    string ApplicantName,
    string PhoneMasked,
    string Status,
    int AttemptNumber,
    int? DerivedFromId,
    string? CreatedRescuerId,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReviewedAt);

public sealed record AdminVolunteerApplicationDetail(
    string ApplicationId,
    string OpenId,
    string Status,
    int AttemptNumber,
    int? DerivedFromId,
    VolunteerApplicationPayloadRecord Payload,
    string? ReviewNote,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? CreatedRescuerId,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminVolunteerApplicationListResponse(
    IReadOnlyList<AdminVolunteerApplicationListItem> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record ApproveVolunteerApplicationRequest(
    string? Campus,
    double? Lat,
    double? Lng,
    string? ReviewNote);

public sealed record RejectVolunteerApplicationRequest(string? ReviewNote);

public sealed record AdminVolunteerApplicationReviewResponse(
    AdminVolunteerApplicationDetail Application,
    string? CreatedRescuerId);
