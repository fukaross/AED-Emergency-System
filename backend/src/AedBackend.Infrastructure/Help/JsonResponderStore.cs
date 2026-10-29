using System.Text.Json;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Services.Help;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Help;

/**
 * JSON provider 仅作为只读/回滚数据源：档案来自既有 JSON 文件，不再内置真实种子。
 * 文件缺失时返回空集合且不写盘；完整演示数据由 SQLite + init-demo-data 提供。
 */
public sealed class JsonResponderStore : IResponderStore
{
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

    public async Task<ResponderProfileRecord?> FindByBoundOpenIdAsync(string openId, CancellationToken ct)
    {
        var profiles = await LoadProfilesAsync(ct);
        return profiles.FirstOrDefault(p => p.BoundOpenId == openId);
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
            return await LoadUnsafeAsync(ct);
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<IReadOnlyList<ResponderProfileRecord>> LoadUnsafeAsync(CancellationToken ct)
    {
        if (!File.Exists(filePath)) return [];

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

    private static string MaskCertificateNo(string certificateNo)
    {
        return $"{certificateNo[..4]}****{certificateNo[^4..]}";
    }
}
