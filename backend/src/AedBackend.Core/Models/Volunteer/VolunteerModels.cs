namespace AedBackend.Core.Models.Volunteer;

public static class VolunteerApplicationStatuses
{
    public const string Submitted = "submitted";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public sealed record VolunteerImageAttachmentRecord(
    string Url,
    string? FileId,
    string? Name);

public sealed record VolunteerApplicationPayloadRecord
{
    public VolunteerImageAttachmentRecord IdPhoto { get; init; } = new(string.Empty, null, null);
    public string Name { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
    public string BirthDate { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PoliticalStatus { get; init; } = string.Empty;
    public string Education { get; init; } = string.Empty;
    public string Occupation { get; init; } = string.Empty;
    public string ProfessionalTitle { get; init; } = string.Empty;
    public string EmergencyContactName { get; init; } = string.Empty;
    public string EmergencyContactPhone { get; init; } = string.Empty;
    public IReadOnlyList<VolunteerImageAttachmentRecord> QualificationImages { get; init; } = [];
    public IReadOnlyList<VolunteerImageAttachmentRecord> VolunteerCertificateImages { get; init; } = [];
    public string IdNumber { get; init; } = string.Empty;
    public VolunteerImageAttachmentRecord IdCardFront { get; init; } = new(string.Empty, null, null);
    public VolunteerImageAttachmentRecord IdCardBack { get; init; } = new(string.Empty, null, null);
    public string HomeAddress { get; init; } = string.Empty;
    public string PersonalResume { get; init; } = string.Empty;
    public string JoinReason { get; init; } = string.Empty;
    public bool CommitmentAccepted { get; init; }
}

public sealed record VolunteerApplicationRecord
{
    public int Id { get; init; }
    public string OpenId { get; init; } = string.Empty;
    public string Status { get; init; } = VolunteerApplicationStatuses.Submitted;
    public int? DerivedFromId { get; init; }
    public int AttemptNumber { get; init; } = 1;
    public VolunteerApplicationPayloadRecord Payload { get; init; } = new();
    public string? ReviewNote { get; init; }
    public string? ReviewedBy { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public string? CreatedRescuerId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
