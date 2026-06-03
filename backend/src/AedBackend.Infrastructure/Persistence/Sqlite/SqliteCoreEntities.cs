namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class AedDeviceEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? StatusNote { get; set; }
    public string Image { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string LastMaintenance { get; set; } = string.Empty;
    public string NextMaintenance { get; set; } = string.Empty;
    public string Maintainer { get; set; } = string.Empty;
}

internal sealed class ResponderProfileEntity
{
    public string Id { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string BirthDate { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public bool PhoneMasked { get; set; }
    public bool IdMasked { get; set; }
    public string AvatarUrl { get; set; } = string.Empty;
    public string Campus { get; set; } = string.Empty;
    public string? Level { get; set; }
    public string QualificationStatus { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? CertificateNo { get; set; }
    public string? CertificateNoMasked { get; set; }
    public string? IssuingAuthority { get; set; }
    public string? IssueDate { get; set; }
    public string? WorkUnit { get; set; }
    public bool SkillCpr { get; set; }
    public bool SkillAed { get; set; }
    public bool SkillStb { get; set; }
    public int TrainingYear { get; set; }
    public string? TrainingProject { get; set; }
    public string? TrainingSessionName { get; set; }
    public int TheoryScore { get; set; }
    public string? TraumaAssessmentResult { get; set; }
    public string? CprAssessmentResult { get; set; }
    public string? AedAssessmentResult { get; set; }
    public string? CprAedCombinedResult { get; set; }
    public string? SourceFile { get; set; }
    public string? SourceSheet { get; set; }
    public int SourceRowNo { get; set; }
    public string? SourceSeqNo { get; set; }
    public string? Remark { get; set; }
    public string? BoundOpenId { get; set; }
    public string Source { get; set; } = "seed";
    public int? SourceApplicationId { get; set; }
}

internal sealed class VolunteerApplicationEntity
{
    public int Id { get; set; }
    public string OpenId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? DerivedFromId { get; set; }
    public int AttemptNumber { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? CreatedRescuerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class SchoolPersonProfileEntity
{
    public int Id { get; set; }
    public string PersonType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string College { get; set; } = string.Empty;
    public string? ClassName { get; set; }
    public string? StudentId { get; set; }
    public string? StaffId { get; set; }
    public string? Phone { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class SchoolBindingApplicationEntity
{
    public int Id { get; set; }
    public string OpenId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? SchoolProfileId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class ResponderClaimApplicationEntity
{
    public int Id { get; set; }
    public string OpenId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ResponderProfileId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class KnowledgeSummaryEntity
{
    public int Id { get; set; }
    public string UpdatedAt { get; set; } = string.Empty;
    public List<KnowledgeStatItemEntity> Items { get; set; } = new();
}

internal sealed class KnowledgeStatItemEntity
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double Value { get; set; }
    public int SortOrder { get; set; }
}
