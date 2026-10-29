using System.Text.RegularExpressions;
using AedBackend.Core.DTOs.User;
using AedBackend.Core.Models.Responder;
using AedBackend.Infrastructure.Site;

namespace AedBackend.Api.Endpoints;

internal static class UserArchiveMapping
{
    private const string ActiveStatus = "active";
    private const string DefaultResponderType = "rescuer";
    private const string DefaultSourceType = "school_import";
    private static readonly DateTimeOffset DefaultImportedAt = DateTimeOffset.Parse("2023-01-01T00:00:00+00:00");

    public static ArchiveListResponse ToListResponse(
        IReadOnlyList<ResponderProfileRecord> profiles,
        string openId,
        UserArchiveEndpoints.ArchiveQuery query,
        SiteOptions site)
    {
        var activeProfiles = profiles.Where(IsActive).ToArray();
        var self = activeProfiles.FirstOrDefault(item => item.BoundOpenId == openId);
        var others = activeProfiles.Where(item => item.Id != self?.Id);
        var filtered = ApplyFilters(others, query).ToArray();
        var paged = filtered.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize);

        return new ArchiveListResponse(
            paged.Select(item => ToListItem(item, site)).ToArray(),
            filtered.Length,
            query.SafePage,
            query.SafePageSize,
            Math.Max(1, (int)Math.Ceiling(filtered.Length / (double)query.SafePageSize)),
            true,
            self is null ? null : ToListItem(self, site));
    }

    public static ArchiveDetailResponse ToDetail(ResponderProfileRecord profile)
    {
        return new ArchiveDetailResponse(
            profile.Id,
            ToProfile(profile),
            null,
            null,
            ToQualification(profile),
            [ToCertificateRecord(profile)]);
    }

    private static IEnumerable<ResponderProfileRecord> ApplyFilters(
        IEnumerable<ResponderProfileRecord> profiles,
        UserArchiveEndpoints.ArchiveQuery query)
    {
        var filtered = profiles;
        if (!string.IsNullOrWhiteSpace(query.Level))
            filtered = filtered.Where(item => item.Level == query.Level);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
            filtered = filtered.Where(item => Contains(item.Name, query.Keyword.Trim()));
        return filtered.OrderBy(item => item.Name, StringComparer.Ordinal);
    }

    private static ArchiveListItem ToListItem(ResponderProfileRecord profile, SiteOptions site)
    {
        var certExpiry = ResolveCertExpiry(profile.IssueDate);
        return new ArchiveListItem(
            profile.Id,
            profile.Name,
            MaskPhone(profile.Phone),
            profile.Level,
            ResolveCollege(profile, site),
            certExpiry,
            IsExpired(certExpiry),
            null);
    }

    private static ArchiveProfileResponse ToProfile(ResponderProfileRecord profile)
    {
        return new ArchiveProfileResponse(
            profile.Id,
            profile.Name,
            NormalizeGender(profile.Gender),
            profile.BirthDate,
            MaskPhone(profile.Phone),
            MaskIdNumber(profile.IdNumber),
            profile.WorkUnit);
    }

    private static ArchiveResponderQualificationResponse ToQualification(
        ResponderProfileRecord profile)
    {
        var certExpiry = ResolveCertExpiry(profile.IssueDate);
        return new ArchiveResponderQualificationResponse(
            profile.Id,
            profile.BoundOpenId,
            profile.Id,
            profile.RecordId,
            DefaultResponderType,
            profile.QualificationStatus,
            profile.Level,
            IsActive(profile),
            string.IsNullOrWhiteSpace(certExpiry) ? null : certExpiry,
            IsExpired(certExpiry),
            DefaultSourceType,
            profile.SourceSeqNo,
            profile.Remark);
    }

    private static ArchiveCertificateRecordResponse ToCertificateRecord(
        ResponderProfileRecord profile)
    {
        return new ArchiveCertificateRecordResponse(
            profile.RecordId,
            profile.Id,
            profile.CertificateNo,
            profile.IssuingAuthority,
            profile.IssueDate,
            profile.TrainingYear,
            profile.TrainingProject,
            profile.TrainingSessionName,
            profile.TheoryScore,
            profile.TraumaAssessmentResult,
            profile.CprAssessmentResult,
            profile.AedAssessmentResult,
            profile.CprAedCombinedResult,
            profile.Remark,
            true,
            ToSourceTrace(profile));
    }

    private static ArchiveSourceTraceResponse ToSourceTrace(
        ResponderProfileRecord profile)
    {
        return new ArchiveSourceTraceResponse(
            profile.SourceFile,
            profile.SourceSheet,
            profile.SourceRowNo,
            profile.SourceSeqNo,
            new ArchiveImportBatchResponse(
                profile.SourceSeqNo ?? profile.SourceFile ?? "responder-profiles",
                profile.TrainingYear,
                profile.SourceFile,
                DefaultImportedAt,
                null,
                null));
    }

    /** 机构/校区显示名来自共享站点配置；不保留旧校区显示名分支。 */
    private static string ResolveCollege(ResponderProfileRecord profile, SiteOptions site)
    {
        return !string.IsNullOrWhiteSpace(profile.WorkUnit)
            ? profile.WorkUnit
            : site.ResolveCampusName(profile.Campus);
    }

    private static string ResolveCertExpiry(string? issueDate)
    {
        if (string.IsNullOrWhiteSpace(issueDate)) return string.Empty;
        return DateTimeOffset.TryParse(issueDate, out var parsed)
            ? parsed.AddYears(3).ToString("yyyy-MM-dd")
            : string.Empty;
    }

    private static bool IsExpired(string certExpiry)
    {
        return DateTimeOffset.TryParse(certExpiry, out var parsed)
            && parsed.Date < DateTimeOffset.UtcNow.Date;
    }

    private static string NormalizeGender(string gender)
    {
        return gender switch
        {
            "男" or "male" => "male",
            "女" or "female" => "female",
            _ => "unknown"
        };
    }

    private static bool IsActive(ResponderProfileRecord profile)
    {
        return profile.QualificationStatus == ActiveStatus;
    }

    private static bool Contains(string? value, string keyword)
    {
        return value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string MaskPhone(string phone)
    {
        return string.IsNullOrWhiteSpace(phone) || phone.Length < 7
            ? phone
            : $"{phone[..3]}****{phone[^4..]}";
    }

    private static string MaskIdNumber(string idNumber)
    {
        return string.IsNullOrWhiteSpace(idNumber) || idNumber.Length < 10
            ? idNumber
            : Regex.Replace(idNumber, @"(.{6}).+(.{4})$", "$1******$2");
    }
}
