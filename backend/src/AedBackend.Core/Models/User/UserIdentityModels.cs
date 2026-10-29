namespace AedBackend.Core.Models.User;

public static class SchoolBindingStatuses
{
    public const string Submitted = "submitted";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public static class ResponderClaimStatuses
{
    public const string Submitted = "submitted";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public sealed record SchoolPersonProfileRecord
{
    public int Id { get; init; }
    public string PersonType { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string College { get; init; } = string.Empty;
    public string? ClassName { get; init; }
    public string? StudentId { get; init; }
    public string? StaffId { get; init; }
    public string? Phone { get; init; }
    public string Status { get; init; } = "active";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record SchoolBindingApplicationRecord
{
    public int Id { get; init; }
    public string OpenId { get; init; } = string.Empty;
    public string Status { get; init; } = SchoolBindingStatuses.Submitted;
    public int? SchoolProfileId { get; init; }
    public SchoolBindingPayloadRecord Payload { get; init; } = new();
    public string? ReviewNote { get; init; }
    public string? ReviewedBy { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record SchoolBindingPayloadRecord
{
    public string PersonType { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string College { get; init; } = string.Empty;
    public string? ClassName { get; init; }
    public string? StudentId { get; init; }
    public string? StaffId { get; init; }
    public string? Phone { get; init; }
    public string? Note { get; init; }
}

public sealed record ResponderClaimApplicationRecord
{
    public int Id { get; init; }
    public string OpenId { get; init; } = string.Empty;
    public string Status { get; init; } = ResponderClaimStatuses.Submitted;
    public string? ResponderProfileId { get; init; }
    public ResponderClaimPayloadRecord Payload { get; init; } = new();
    public string? ReviewNote { get; init; }
    public string? ReviewedBy { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record ResponderClaimPayloadRecord
{
    public string Name { get; init; } = string.Empty;
    public string IdNumberLast6 { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string? CertificateNo { get; init; }
    public string? Note { get; init; }
}
