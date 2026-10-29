namespace AedBackend.Core.Models.Responder;

public sealed record ResponderProfileRecord
{
    public string Id { get; set; } = string.Empty;
    public string RecordId { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
    public string BirthDate { get; init; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public bool PhoneMasked { get; init; }
    public bool IdMasked { get; init; }
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
    public int TrainingYear { get; init; }
    public string? TrainingProject { get; init; }
    public string? TrainingSessionName { get; init; }
    public int TheoryScore { get; init; }
    public string? TraumaAssessmentResult { get; set; }
    public string? CprAssessmentResult { get; set; }
    public string? AedAssessmentResult { get; set; }
    public string? CprAedCombinedResult { get; set; }
    public string? SourceFile { get; init; }
    public string? SourceSheet { get; init; }
    public int SourceRowNo { get; init; }
    public string? SourceSeqNo { get; init; }
    public string? Remark { get; init; }
    public string? BoundOpenId { get; set; }
    public string Source { get; set; } = "seed";
    public int? SourceApplicationId { get; set; }
}
