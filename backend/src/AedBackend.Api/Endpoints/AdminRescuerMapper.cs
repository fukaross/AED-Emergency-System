using System.Text.RegularExpressions;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.Models.Responder;

namespace AedBackend.Api.Endpoints;

internal static class AdminRescuerMapper
{
    public static AdminRescuerListItem ToListItem(ResponderProfileRecord p)
    {
        return new AdminRescuerListItem(
            p.Id,
            p.RecordId,
            p.Name,
            p.AvatarUrl,
            p.Campus,
            p.Level,
            p.QualificationStatus,
            p.Lat,
            p.Lng,
            p.CertificateNoMasked,
            p.WorkUnit,
            MaskPhone(p.Phone),
            p.SkillCpr,
            p.SkillAed,
            p.SkillStb);
    }

    public static AdminRescuerDetail ToDetail(ResponderProfileRecord p)
    {
        return new AdminRescuerDetail(
            p.Id,
            p.RecordId,
            p.Name,
            p.Gender,
            p.BirthDate,
            MaskPhone(p.Phone),
            p.Phone,
            MaskIdNumber(p.IdNumber),
            p.IdNumber,
            p.PhoneMasked,
            p.IdMasked,
            p.AvatarUrl,
            p.Campus,
            p.Level,
            p.QualificationStatus,
            p.Lat,
            p.Lng,
            p.CertificateNo,
            p.CertificateNoMasked,
            p.IssuingAuthority,
            p.IssueDate,
            p.WorkUnit,
            p.SkillCpr,
            p.SkillAed,
            p.SkillStb,
            p.TrainingYear,
            p.TrainingProject,
            p.TrainingSessionName,
            p.TheoryScore,
            p.TraumaAssessmentResult,
            p.CprAssessmentResult,
            p.AedAssessmentResult,
            p.CprAedCombinedResult,
            p.SourceFile,
            p.SourceSheet,
            p.SourceRowNo,
            p.SourceSeqNo,
            p.Remark);
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 7) return phone;
        return Regex.Replace(phone, @"(\d{3})\d{4}(\d+)", "$1****$2");
    }

    private static string MaskIdNumber(string idNumber)
    {
        if (string.IsNullOrWhiteSpace(idNumber) || idNumber.Length < 10) return idNumber;
        return Regex.Replace(idNumber, @"(.{6}).+(.{4})$", "$1******$2");
    }
}
