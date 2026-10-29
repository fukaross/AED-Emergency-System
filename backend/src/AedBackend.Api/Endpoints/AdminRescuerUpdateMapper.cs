using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.Models.Responder;

namespace AedBackend.Api.Endpoints;

internal static class AdminRescuerUpdateMapper
{
    public static RescuerUpdateTargets Resolve(
        UpdateRescuerRequest body,
        ResponderProfileRecord existing)
    {
        return new RescuerUpdateTargets
        {
            Name = ResolveString(body.HasName, body.Name, existing.Name),
            AvatarUrl = ResolveString(body.HasAvatarUrl, body.AvatarUrl, existing.AvatarUrl),
            Phone = ResolveString(body.HasPhone, body.Phone, existing.Phone),
            Campus = ResolveString(body.HasCampus, body.Campus, existing.Campus),
            Level = ResolveNullableString(body.HasLevel, body.Level, existing.Level),
            QualificationStatus = ResolveString(
                body.HasQualificationStatus, body.QualificationStatus, existing.QualificationStatus),
            Lat = body.HasLat ? body.Lat ?? existing.Lat : existing.Lat,
            Lng = body.HasLng ? body.Lng ?? existing.Lng : existing.Lng,
            CertificateNo = ResolveNullableString(body.HasCertificateNo, body.CertificateNo, existing.CertificateNo),
            IssuingAuthority = ResolveNullableString(
                body.HasIssuingAuthority, body.IssuingAuthority, existing.IssuingAuthority),
            IssueDate = ResolveNullableString(body.HasIssueDate, body.IssueDate, existing.IssueDate),
            WorkUnit = ResolveNullableString(body.HasWorkUnit, body.WorkUnit, existing.WorkUnit),
            SkillCpr = body.HasSkillCpr ? body.SkillCpr ?? existing.SkillCpr : existing.SkillCpr,
            SkillAed = body.HasSkillAed ? body.SkillAed ?? existing.SkillAed : existing.SkillAed,
            SkillStb = body.HasSkillStb ? body.SkillStb ?? existing.SkillStb : existing.SkillStb,
            IdNumber = ResolveString(body.HasIdNumber, body.IdNumber, existing.IdNumber),
            TraumaAssessmentResult = ResolveNullableString(
                body.HasTraumaAssessmentResult, body.TraumaAssessmentResult, existing.TraumaAssessmentResult),
            CprAssessmentResult = ResolveNullableString(
                body.HasCprAssessmentResult, body.CprAssessmentResult, existing.CprAssessmentResult),
            AedAssessmentResult = ResolveNullableString(
                body.HasAedAssessmentResult, body.AedAssessmentResult, existing.AedAssessmentResult),
            CprAedCombinedResult = ResolveNullableString(
                body.HasCprAedCombinedResult, body.CprAedCombinedResult, existing.CprAedCombinedResult)
        };
    }

    private static string ResolveString(bool hasValue, string? value, string current) =>
        hasValue ? value?.Trim() ?? string.Empty : current;

    private static string? ResolveNullableString(
        bool hasValue,
        string? value,
        string? current)
    {
        if (!hasValue) return current;

        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}

internal sealed record RescuerUpdateTargets
{
    public required string Name { get; init; }
    public required string AvatarUrl { get; init; }
    public required string Phone { get; init; }
    public required string Campus { get; init; }
    public string? Level { get; init; }
    public required string QualificationStatus { get; init; }
    public double Lat { get; init; }
    public double Lng { get; init; }
    public string? CertificateNo { get; init; }
    public string? IssuingAuthority { get; init; }
    public string? IssueDate { get; init; }
    public string? WorkUnit { get; init; }
    public bool SkillCpr { get; init; }
    public bool SkillAed { get; init; }
    public bool SkillStb { get; init; }
    public required string IdNumber { get; init; }
    public string? TraumaAssessmentResult { get; init; }
    public string? CprAssessmentResult { get; init; }
    public string? AedAssessmentResult { get; init; }
    public string? CprAedCombinedResult { get; init; }

    public ResponderProfileRecord ToRecord(string id)
    {
        return new ResponderProfileRecord
        {
            Id = id,
            Name = Name,
            AvatarUrl = AvatarUrl,
            Phone = Phone,
            Campus = Campus,
            Level = Level,
            QualificationStatus = QualificationStatus,
            Lat = Lat,
            Lng = Lng,
            CertificateNo = CertificateNo,
            IssuingAuthority = IssuingAuthority,
            IssueDate = IssueDate,
            WorkUnit = WorkUnit,
            SkillCpr = SkillCpr,
            SkillAed = SkillAed,
            SkillStb = SkillStb,
            IdNumber = IdNumber,
            TraumaAssessmentResult = TraumaAssessmentResult,
            CprAssessmentResult = CprAssessmentResult,
            AedAssessmentResult = AedAssessmentResult,
            CprAedCombinedResult = CprAedCombinedResult
        };
    }
}
