using System.Text.Json.Serialization;
using AedBackend.Core.Models.User;
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
    UserSchoolProfileResponse? SchoolProfile,
    UserRescuerSummaryResponse? RescuerSummary);

public sealed record UserSchoolProfileResponse(
    int ProfileId,
    string PersonType,
    string Name,
    string College,
    string? ClassName,
    string? StudentId,
    string? StaffId,
    string? Phone,
    string BindingStatus,
    DateTimeOffset BoundAt);

public sealed record UserRescuerSummaryResponse(
    string ResponderProfileId,
    string Name,
    string Campus,
    bool Certified,
    string? Level,
    string ResponderType,
    string ResponderStatus,
    string? CertificateNoMasked,
    string? WorkUnit);

public sealed record ArchiveListResponse(
    IReadOnlyList<ArchiveListItem> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages,
    bool CanViewFullPhone,
    ArchiveListItem? SelfArchive);

public sealed record ArchiveListItem(
    string Id,
    string Name,
    string Phone,
    string? Level,
    string College,
    string CertExpiry,
    bool CertExpired,
    string? SchoolBindingStatus);

public sealed record ArchiveDetailResponse(
    string Id,
    ArchiveProfileResponse Profile,
    UserSchoolProfileResponse? SchoolProfile,
    string? SchoolBindingStatus,
    ArchiveResponderQualificationResponse? ResponderQualification,
    IReadOnlyList<ArchiveCertificateRecordResponse> CertificateRecords);

public sealed record ArchiveProfileResponse(
    string Id,
    string Name,
    string Gender,
    string BirthDate,
    string PhoneMasked,
    string IdMasked,
    string? WorkUnit);

public sealed record ArchiveResponderQualificationResponse(
    string Id,
    string? UserId,
    string RescuerArchiveProfileId,
    string? CurrentCertificateRecordId,
    string ResponderType,
    string Status,
    string? Level,
    bool Certified,
    string? CertExpiry,
    bool CertExpired,
    string SourceType,
    string? SourceBatchNo,
    string? Remark);

public sealed record ArchiveCertificateRecordResponse(
    string Id,
    string ProfileId,
    string? CertificateNo,
    string? IssuingAuthority,
    string? IssueDate,
    int TrainingYear,
    string? TrainingProject,
    string? TrainingSessionName,
    int? TheoryScore,
    string? TraumaAssessmentResult,
    string? CprAssessmentResult,
    string? AedAssessmentResult,
    string? CprAedCombinedResult,
    string? Remark,
    bool IsCurrent,
    ArchiveSourceTraceResponse SourceTrace);

public sealed record ArchiveSourceTraceResponse(
    string? SourceFile,
    string? SourceSheet,
    int SourceRowNo,
    string? SourceSeqNo,
    ArchiveImportBatchResponse Batch);

public sealed record ArchiveImportBatchResponse(
    string Id,
    int SourceYear,
    string? SourceFileName,
    DateTimeOffset ImportedAt,
    string? Operator,
    string? Note);

public sealed record ArchivePhoneResponse(string Phone);

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

public sealed record SubmitSchoolBindingRequest(
    string? PersonType,
    string? Name,
    string? College,
    string? ClassName,
    string? StudentId,
    string? StaffId,
    string? Phone,
    string? Note);

public sealed record SchoolBindingResponse(
    string ApplicationId,
    string Status,
    DateTimeOffset SubmittedAt);

public sealed record SchoolBindingListResponse(
    IReadOnlyList<SchoolBindingDetailResponse> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record SchoolBindingDetailResponse(
    string ApplicationId,
    string Status,
    int? SchoolProfileId,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt,
    string? ReviewNote,
    DateTimeOffset? ReviewedAt,
    SchoolBindingPayloadRecordDto Payload);

public sealed record SchoolBindingPayloadRecordDto(
    string PersonType,
    string Name,
    string College,
    string? ClassName,
    string? StudentId,
    string? StaffId,
    string? Phone,
    string? Note);

public sealed record SubmitResponderClaimRequest(
    string? Name,
    string? IdNumberLast6,
    string? Phone,
    string? CertificateNo,
    string? Note);

public sealed record ResponderClaimResponse(
    string ApplicationId,
    string Status,
    DateTimeOffset SubmittedAt);

public sealed record ResponderClaimListResponse(
    IReadOnlyList<ResponderClaimDetailResponse> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record ResponderClaimDetailResponse(
    string ApplicationId,
    string Status,
    string? ResponderProfileId,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt,
    string? ReviewNote,
    DateTimeOffset? ReviewedAt,
    ResponderClaimPayloadRecordDto Payload);

public sealed record ResponderClaimPayloadRecordDto(
    string Name,
    string IdNumberLast6,
    string Phone,
    string? CertificateNo,
    string? Note);
