namespace AedBackend.Core.DTOs.Admin;

public sealed record AdminRescuerListItem(
    string Id,
    string RecordId,
    string Name,
    string AvatarUrl,
    string Campus,
    string? Level,
    string QualificationStatus,
    double Lat,
    double Lng,
    string? CertificateNoMasked,
    string? WorkUnit,
    string PhoneMasked,
    bool SkillCpr,
    bool SkillAed,
    bool SkillStb);

public sealed record AdminRescuerDetail(
    string Id,
    string RecordId,
    string Name,
    string Gender,
    string BirthDate,
    string PhoneMasked,
    string Phone,
    string IdNumberMasked,
    string IdNumber,
    bool PhoneMaskedFlag,
    bool IdMaskedFlag,
    string AvatarUrl,
    string Campus,
    string? Level,
    string QualificationStatus,
    double Lat,
    double Lng,
    string? CertificateNo,
    string? CertificateNoMasked,
    string? IssuingAuthority,
    string? IssueDate,
    string? WorkUnit,
    bool SkillCpr,
    bool SkillAed,
    bool SkillStb,
    int TrainingYear,
    string? TrainingProject,
    string? TrainingSessionName,
    int TheoryScore,
    string? TraumaAssessmentResult,
    string? CprAssessmentResult,
    string? AedAssessmentResult,
    string? CprAedCombinedResult,
    string? SourceFile,
    string? SourceSheet,
    int SourceRowNo,
    string? SourceSeqNo,
    string? Remark);

public sealed record AdminRescuerListResponse(
    IReadOnlyList<AdminRescuerListItem> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed class UpdateRescuerRequest
{
    private string? avatarUrl;
    private string? campus;
    private string? certificateNo;
    private string? issueDate;
    private string? issuingAuthority;
    private double? lat;
    private string? level;
    private double? lng;
    private string? name;
    private string? phone;
    private string? qualificationStatus;
    private bool? skillAed;
    private bool? skillCpr;
    private bool? skillStb;
    private string? workUnit;
    private string? cprAssessmentResult;
    private string? aedAssessmentResult;
    private string? traumaAssessmentResult;
    private string? cprAedCombinedResult;
    private string? idNumber;

    public string? Name { get => name; set { HasName = true; name = value; } }
    public string? AvatarUrl { get => avatarUrl; set { HasAvatarUrl = true; avatarUrl = value; } }
    public string? Phone { get => phone; set { HasPhone = true; phone = value; } }
    public string? Campus { get => campus; set { HasCampus = true; campus = value; } }
    public string? Level { get => level; set { HasLevel = true; level = value; } }
    public string? QualificationStatus { get => qualificationStatus; set { HasQualificationStatus = true; qualificationStatus = value; } }
    public double? Lat { get => lat; set { HasLat = true; lat = value; } }
    public double? Lng { get => lng; set { HasLng = true; lng = value; } }
    public string? CertificateNo { get => certificateNo; set { HasCertificateNo = true; certificateNo = value; } }
    public string? IssuingAuthority { get => issuingAuthority; set { HasIssuingAuthority = true; issuingAuthority = value; } }
    public string? IssueDate { get => issueDate; set { HasIssueDate = true; issueDate = value; } }
    public string? WorkUnit { get => workUnit; set { HasWorkUnit = true; workUnit = value; } }
    public bool? SkillCpr { get => skillCpr; set { HasSkillCpr = true; skillCpr = value; } }
    public bool? SkillAed { get => skillAed; set { HasSkillAed = true; skillAed = value; } }
    public bool? SkillStb { get => skillStb; set { HasSkillStb = true; skillStb = value; } }
    public string? CprAssessmentResult { get => cprAssessmentResult; set { HasCprAssessmentResult = true; cprAssessmentResult = value; } }
    public string? AedAssessmentResult { get => aedAssessmentResult; set { HasAedAssessmentResult = true; aedAssessmentResult = value; } }
    public string? TraumaAssessmentResult { get => traumaAssessmentResult; set { HasTraumaAssessmentResult = true; traumaAssessmentResult = value; } }
    public string? CprAedCombinedResult { get => cprAedCombinedResult; set { HasCprAedCombinedResult = true; cprAedCombinedResult = value; } }
    public string? IdNumber { get => idNumber; set { HasIdNumber = true; idNumber = value; } }

    public bool HasName { get; private set; }
    public bool HasAvatarUrl { get; private set; }
    public bool HasPhone { get; private set; }
    public bool HasCampus { get; private set; }
    public bool HasLevel { get; private set; }
    public bool HasQualificationStatus { get; private set; }
    public bool HasLat { get; private set; }
    public bool HasLng { get; private set; }
    public bool HasCertificateNo { get; private set; }
    public bool HasIssuingAuthority { get; private set; }
    public bool HasIssueDate { get; private set; }
    public bool HasWorkUnit { get; private set; }
    public bool HasSkillCpr { get; private set; }
    public bool HasSkillAed { get; private set; }
    public bool HasSkillStb { get; private set; }
    public bool HasCprAssessmentResult { get; private set; }
    public bool HasAedAssessmentResult { get; private set; }
    public bool HasTraumaAssessmentResult { get; private set; }
    public bool HasCprAedCombinedResult { get; private set; }
    public bool HasIdNumber { get; private set; }
}
