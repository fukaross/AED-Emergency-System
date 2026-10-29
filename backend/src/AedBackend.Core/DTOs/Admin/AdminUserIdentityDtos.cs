namespace AedBackend.Core.DTOs.Admin;

public sealed record AdminSchoolProfileListItem(
    string ProfileId,
    string PersonType,
    string Name,
    string College,
    string? ClassName,
    string? StudentId,
    string? StaffId,
    string? PhoneMasked,
    string Status,
    DateTimeOffset UpdatedAt);

public sealed record AdminSchoolProfileDetail(
    string ProfileId,
    string PersonType,
    string Name,
    string College,
    string? ClassName,
    string? StudentId,
    string? StaffId,
    string? Phone,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminSchoolProfileListResponse(
    IReadOnlyList<AdminSchoolProfileListItem> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record UpsertSchoolProfileRequest(
    string? PersonType,
    string? Name,
    string? College,
    string? ClassName,
    string? StudentId,
    string? StaffId,
    string? Phone,
    string? Status);

public sealed record AdminSchoolBindingListItem(
    string ApplicationId,
    string OpenId,
    string ApplicantName,
    string College,
    string? StudentId,
    string? StaffId,
    string Status,
    int? SchoolProfileId,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReviewedAt);

public sealed record AdminSchoolBindingDetail(
    string ApplicationId,
    string OpenId,
    string Status,
    int? SchoolProfileId,
    AdminSchoolBindingPayload Payload,
    string? ReviewNote,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminSchoolBindingPayload(
    string PersonType,
    string Name,
    string College,
    string? ClassName,
    string? StudentId,
    string? StaffId,
    string? Phone,
    string? Note);

public sealed record AdminSchoolBindingListResponse(
    IReadOnlyList<AdminSchoolBindingListItem> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record ApproveSchoolBindingRequest(
    int? SchoolProfileId,
    UpsertSchoolProfileRequest? NewProfile,
    string? ReviewNote);

public sealed record RejectUserIdentityRequest(string? ReviewNote);

public sealed record AdminSchoolBindingReviewResponse(
    AdminSchoolBindingDetail Application);

public sealed record AdminResponderClaimListItem(
    string ApplicationId,
    string OpenId,
    string ApplicantName,
    string PhoneMasked,
    string Status,
    string? ResponderProfileId,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReviewedAt);

public sealed record AdminResponderClaimDetail(
    string ApplicationId,
    string OpenId,
    string Status,
    string? ResponderProfileId,
    AdminResponderClaimPayload Payload,
    string? ReviewNote,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminResponderClaimPayload(
    string Name,
    string IdNumberLast6,
    string Phone,
    string? CertificateNo,
    string? Note);

public sealed record AdminResponderClaimListResponse(
    IReadOnlyList<AdminResponderClaimListItem> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record ApproveResponderClaimRequest(
    string? ResponderProfileId,
    string? ReviewNote);

public sealed record AdminResponderClaimReviewResponse(
    AdminResponderClaimDetail Application);
