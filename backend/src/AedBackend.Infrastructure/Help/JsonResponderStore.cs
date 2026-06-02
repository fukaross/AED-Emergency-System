using System.Text.Json;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Services.Help;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Help;

public sealed class JsonResponderStore : IResponderStore
{
    private const int GuilinyangOfflineStart = 17;
    private const int FuchengOfflineStart = 8;

    private static readonly (double Lat, double Lng) GuilinyangBase = (REDACTED, REDACTED);
    private static readonly (double Lat, double Lng) FuchengBase = (REDACTED, REDACTED);

    private static readonly string[] AvatarUrls =
    [
        "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=200&h=200&q=80",
        "https://images.unsplash.com/photo-1506744038136-46273834b3fb?auto=format&fit=crop&w=200&h=200&q=80",
        "https://images.unsplash.com/photo-1441974231531-c6227db76b6e?auto=format&fit=crop&w=200&h=200&q=80",
        "https://images.unsplash.com/photo-1501785888041-af3ef285b470?auto=format&fit=crop&w=200&h=200&q=80",
        "https://images.unsplash.com/photo-1470770841072-f978cf4d019e?auto=format&fit=crop&w=200&h=200&q=80",
        "https://images.unsplash.com/photo-1469474968028-56623f02e42e?auto=format&fit=crop&w=200&h=200&q=80",
        "https://images.unsplash.com/photo-1433086966358-54859d0ed716?auto=format&fit=crop&w=200&h=200&q=80"
    ];

    private const string FeaturedRecordId = "REDACTED";
    private const string DefaultLevel = "trainee";
    private const string FeaturedLevel = "honor";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonResponderStore(IOptions<WeChatOptions> options)
    {
        filePath = Path.GetFullPath(options.Value.RescuerProfilesFilePath);
    }

    public async Task<IReadOnlyList<ResponderProfileRecord>> GetAllAsync(CancellationToken ct)
    {
        return await LoadProfilesAsync(ct);
    }

    public async Task<ResponderProfileRecord?> FindByIdAsync(string id, CancellationToken ct)
    {
        var profiles = await LoadProfilesAsync(ct);
        return profiles.FirstOrDefault(p =>
            p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ResponderProfileRecord?> UpdateAsync(
        string id,
        ResponderProfileRecord updated,
        CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            var profiles = await LoadUnsafeAsync(ct);
            var list = profiles.ToList();
            var existing = list.FirstOrDefault(p =>
                p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existing is null) return null;

            existing.Name = updated.Name;
            existing.AvatarUrl = updated.AvatarUrl;
            existing.Phone = updated.Phone;
            existing.Campus = updated.Campus;
            existing.Level = updated.Level;
            existing.QualificationStatus = updated.QualificationStatus;
            existing.Lat = updated.Lat;
            existing.Lng = updated.Lng;
            existing.CertificateNo = updated.CertificateNo;
            existing.IssuingAuthority = updated.IssuingAuthority;
            existing.IssueDate = updated.IssueDate;
            existing.WorkUnit = updated.WorkUnit;
            existing.SkillCpr = updated.SkillCpr;
            existing.SkillAed = updated.SkillAed;
            existing.SkillStb = updated.SkillStb;
            existing.IdNumber = updated.IdNumber;
            existing.TraumaAssessmentResult = updated.TraumaAssessmentResult;
            existing.CprAssessmentResult = updated.CprAssessmentResult;
            existing.AedAssessmentResult = updated.AedAssessmentResult;
            existing.CprAedCombinedResult = updated.CprAedCombinedResult;

            ComputeMaskedFields(existing);

            await WriteProfilesAsync(list, ct);
            return existing;
        }
        finally
        {
            fileLock.Release();
        }
    }

    private static void ComputeMaskedFields(ResponderProfileRecord record)
    {
        record.CertificateNoMasked = !string.IsNullOrWhiteSpace(record.CertificateNo)
            && record.CertificateNo.Length >= 8
            ? MaskCertificateNo(record.CertificateNo)
            : null;
    }

    private async Task<IReadOnlyList<ResponderProfileRecord>> LoadProfilesAsync(CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            if (!File.Exists(filePath))
            {
                var defaults = BuildDefaultProfiles();
                await WriteProfilesAsync(defaults, ct);
                return defaults;
            }

            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<IReadOnlyList<ResponderProfileRecord>>(
                stream, JsonOptions, ct) ?? [];
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<IReadOnlyList<ResponderProfileRecord>> LoadUnsafeAsync(CancellationToken ct)
    {
        if (!File.Exists(filePath))
        {
            return BuildDefaultProfiles();
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<IReadOnlyList<ResponderProfileRecord>>(
            stream, JsonOptions, ct) ?? [];
    }

    private async Task WriteProfilesAsync(
        IReadOnlyList<ResponderProfileRecord> profiles,
        CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, profiles, JsonOptions, ct);
    }

    private static IReadOnlyList<ResponderProfileRecord> BuildDefaultProfiles()
    {
        var rows = ResponderSeedData.Rows;
        var profiles = new List<ResponderProfileRecord>(rows.Length);

        var guilinyangCount = 0;
        var fuchengCount = 0;

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var campus = ResolveCampus(row.TrainingSessionName);
            var localIndex = campus == "guilinyang" ? guilinyangCount : fuchengCount;

            if (row.RecordId == FeaturedRecordId)
                localIndex = 0;

            var online = campus == "guilinyang"
                ? localIndex < GuilinyangOfflineStart
                : localIndex < FuchengOfflineStart;

            var coordinates = online
                ? ResolveOnlineCoordinates(campus, localIndex)
                : ResolveOfflineCoordinates(campus, localIndex);

            var cprAedPassed = row.CprAedCombinedResult == "合格";
            var level = row.RecordId == FeaturedRecordId ? FeaturedLevel : DefaultLevel;
            var status = cprAedPassed ? "active" : "inactive";
            var sensitiveFields = ResponderSeedData.GetSensitiveFields(row.RecordId);

            profiles.Add(new ResponderProfileRecord
            {
                Id = $"rescuer-{(i + 1):D3}",
                RecordId = row.RecordId,
                Name = row.Name,
                Gender = row.Gender,
                BirthDate = sensitiveFields.BirthDate,
                Phone = sensitiveFields.Phone,
                IdNumber = sensitiveFields.IdNumber,
                PhoneMasked = false,
                IdMasked = false,
                AvatarUrl = AvatarUrls[i % AvatarUrls.Length],
                Campus = campus,
                Level = level,
                QualificationStatus = status,
                Lat = coordinates.Lat,
                Lng = coordinates.Lng,
                CertificateNo = sensitiveFields.CertificateNo,
                CertificateNoMasked = MaskCertificateNo(sensitiveFields.CertificateNo),
                IssuingAuthority = row.IssuingAuthority,
                IssueDate = row.IssueDate,
                WorkUnit = string.IsNullOrWhiteSpace(row.WorkUnit) ? null : row.WorkUnit.Trim(),
                SkillCpr = row.CprAssessmentResult == "合格" || cprAedPassed,
                SkillAed = row.AedAssessmentResult == "合格" || cprAedPassed,
                SkillStb = row.TraumaAssessmentResult == "合格",
                TrainingYear = row.TrainingYear,
                TrainingProject = row.TrainingProject,
                TrainingSessionName = row.TrainingSessionName,
                TheoryScore = row.TheoryScore,
                TraumaAssessmentResult = NullIfEmpty(row.TraumaAssessmentResult),
                CprAssessmentResult = NullIfEmpty(row.CprAssessmentResult),
                AedAssessmentResult = NullIfEmpty(row.AedAssessmentResult),
                CprAedCombinedResult = NullIfEmpty(row.CprAedCombinedResult),
                SourceFile = row.SourceFile,
                SourceSheet = row.SourceSheet,
                SourceRowNo = row.SourceRowNo,
                SourceSeqNo = row.SourceSeqNo,
                Remark = NullIfEmpty(row.Remark)
            });

            if (campus == "guilinyang")
                guilinyangCount++;
            else
                fuchengCount++;
        }

        return profiles;
    }

    private static string ResolveCampus(string? sessionName)
    {
        return sessionName?.Contains("府城") == true ? "fucheng" : "guilinyang";
    }

    private static (double Lat, double Lng) ResolveOnlineCoordinates(string campus, int localIndex)
    {
        if (campus != "guilinyang")
        {
            return (
                Math.Round(FuchengBase.Lat + ((localIndex % 5) - 2) * 0.00031 + (localIndex / 5) * 0.00014, 8),
                Math.Round(FuchengBase.Lng + ((localIndex % 4) - 2) * 0.00027 + (localIndex / 4) * 0.00012, 8)
            );
        }

        return (
            Math.Round(GuilinyangBase.Lat + ((localIndex % 6) - 2) * 0.00032 + (localIndex / 6) * 0.00018, 8),
            Math.Round(GuilinyangBase.Lng + ((localIndex % 5) - 2) * 0.00029 + (localIndex / 5) * 0.00016, 8)
        );
    }

    private static (double Lat, double Lng) ResolveOfflineCoordinates(string campus, int localIndex)
    {
        if (campus != "guilinyang")
        {
            return (
                Math.Round(20.36 + (localIndex - FuchengOfflineStart) * 0.03, 8),
                Math.Round(110.69 + (localIndex - FuchengOfflineStart) * 0.02, 8)
            );
        }

        return (
            Math.Round(20.42 + (localIndex - GuilinyangOfflineStart) * 0.03, 8),
            Math.Round(110.82 + (localIndex - GuilinyangOfflineStart) * 0.02, 8)
        );
    }

    private static string MaskCertificateNo(string certificateNo)
    {
        return $"{certificateNo[..4]}****{certificateNo[^4..]}";
    }

    private static string? NullIfEmpty(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
