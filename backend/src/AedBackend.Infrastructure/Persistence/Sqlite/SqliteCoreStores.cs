using AedBackend.Core.Models.Aed;
using AedBackend.Core.Models.Knowledge;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class SqliteAedDeviceStore : IAedDeviceStore
{
    private const double EarthRadiusMeters = 6371000;
    private readonly IDbContextFactory<AedDbContext> factory;

    public SqliteAedDeviceStore(IDbContextFactory<AedDbContext> factory) => this.factory = factory;

    public async Task<IReadOnlyList<NearbyAedDevice>> GetNearbyAsync(double lat, double lng, int? radiusMeters, CancellationToken ct)
    {
        var devices = await GetAllAsync(ct);
        var nearby = devices.Select(item => ToNearby(item, lat, lng));
        if (radiusMeters is > 0) nearby = nearby.Where(item => item.Distance <= radiusMeters.Value);
        return nearby.OrderByDescending(item => item.Device.Status == "online").ThenBy(item => item.Distance).ToArray();
    }

    public async Task<AedDeviceRecord?> FindByIdAsync(string id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await db.AedDevices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        return item is null ? null : SqliteMapper.ToRecord(item);
    }

    public async Task<IReadOnlyList<AedDeviceRecord>> GetAllAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var devices = await db.AedDevices.AsNoTracking().ToArrayAsync(ct);
        return devices.Select(SqliteMapper.ToRecord).ToArray();
    }

    public async Task<AedDeviceRecord> AddAsync(AedDeviceRecord device, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.AedDevices.AnyAsync(d => d.Name == device.Name, ct))
            throw new InvalidOperationException("设备名称已存在");

        device.Id = await GenerateNextIdAsync(db, ct);
        if (string.IsNullOrWhiteSpace(device.Status)) device.Status = "online";
        db.AedDevices.Add(SqliteMapper.ToEntity(device));
        await db.SaveChangesAsync(ct);
        return device;
    }

    public async Task<AedDeviceRecord?> UpdateAsync(string id, AedDeviceRecord updated, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await db.AedDevices.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (current is null) return null;
        await EnsureUpdateAllowedAsync(db, current, updated, ct);
        Apply(current, updated);
        await db.SaveChangesAsync(ct);
        return SqliteMapper.ToRecord(current);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await db.AedDevices.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (current is null) return false;
        db.AedDevices.Remove(current);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ExistsByNameAsync(string name, string? excludeId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AedDevices.AnyAsync(d => d.Name == name && (excludeId == null || d.Id != excludeId), ct);
    }

    private static void Apply(AedDeviceEntity target, AedDeviceRecord source)
    {
        target.Name = source.Name; target.Lat = source.Lat; target.Lng = source.Lng; target.Status = source.Status;
        target.StatusNote = source.StatusNote; target.Image = source.Image; target.Address = source.Address;
        target.LastMaintenance = source.LastMaintenance; target.NextMaintenance = source.NextMaintenance;
        target.Maintainer = source.Maintainer;
    }

    private static double CalculateDistance(double fromLat, double fromLng, double toLat, double toLng)
    {
        var deltaLat = ToRadians(toLat - fromLat); var deltaLng = ToRadians(toLng - fromLng);
        var a = Math.Pow(Math.Sin(deltaLat / 2), 2) + Math.Cos(ToRadians(fromLat)) * Math.Cos(ToRadians(toLat)) * Math.Pow(Math.Sin(deltaLng / 2), 2);
        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static async Task EnsureUpdateAllowedAsync(AedDbContext db, AedDeviceEntity current, AedDeviceRecord updated, CancellationToken ct)
    {
        if (updated.Status == "offline" && string.IsNullOrWhiteSpace(updated.StatusNote))
            throw new InvalidOperationException("设备离线时必须填写状态备注");
        var duplicate = await db.AedDevices.AnyAsync(d => d.Id != current.Id && d.Name == updated.Name, ct);
        if (duplicate) throw new InvalidOperationException("设备名称已存在");
    }

    private static async Task<string> GenerateNextIdAsync(AedDbContext db, CancellationToken ct)
    {
        var ids = await db.AedDevices.Select(d => d.Id).ToArrayAsync(ct);
        var max = ids.Where(id => id.StartsWith("AED-", StringComparison.OrdinalIgnoreCase))
            .Select(id => int.TryParse(id.AsSpan(4), out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"AED-{max + 1:D2}";
    }

    private static NearbyAedDevice ToNearby(AedDeviceRecord device, double lat, double lng)
    {
        return new NearbyAedDevice(device, (int)Math.Round(CalculateDistance(lat, lng, device.Lat, device.Lng)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

internal sealed class SqliteResponderStore : IResponderStore
{
    private readonly IDbContextFactory<AedDbContext> factory;
    public SqliteResponderStore(IDbContextFactory<AedDbContext> factory) => this.factory = factory;

    public async Task<IReadOnlyList<ResponderProfileRecord>> GetAllAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var profiles = await db.ResponderProfiles.AsNoTracking().ToArrayAsync(ct);
        return profiles.Select(SqliteMapper.ToRecord).ToArray();
    }

    public async Task<ResponderProfileRecord?> FindByIdAsync(string id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await db.ResponderProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return item is null ? null : SqliteMapper.ToRecord(item);
    }

    public async Task<ResponderProfileRecord?> FindByBoundOpenIdAsync(string openId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await db.ResponderProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.BoundOpenId == openId, ct);
        return item is null ? null : SqliteMapper.ToRecord(item);
    }

    public async Task<ResponderProfileRecord?> UpdateAsync(string id, ResponderProfileRecord updated, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await db.ResponderProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (current is null) return null;
        Apply(current, updated);
        await db.SaveChangesAsync(ct);
        return SqliteMapper.ToRecord(current);
    }

    private static void Apply(ResponderProfileEntity target, ResponderProfileRecord source)
    {
        var entity = SqliteMapper.ToEntity(source);
        target.Name = entity.Name; target.AvatarUrl = entity.AvatarUrl; target.Phone = entity.Phone;
        target.Campus = entity.Campus; target.Level = entity.Level; target.QualificationStatus = entity.QualificationStatus;
        target.Lat = entity.Lat; target.Lng = entity.Lng; target.CertificateNo = entity.CertificateNo;
        target.CertificateNoMasked = MaskCertificateNo(entity.CertificateNo); target.IssuingAuthority = entity.IssuingAuthority;
        target.IssueDate = entity.IssueDate; target.WorkUnit = entity.WorkUnit; target.SkillCpr = entity.SkillCpr;
        target.SkillAed = entity.SkillAed; target.SkillStb = entity.SkillStb; target.IdNumber = entity.IdNumber;
        target.TraumaAssessmentResult = entity.TraumaAssessmentResult; target.CprAssessmentResult = entity.CprAssessmentResult;
        target.AedAssessmentResult = entity.AedAssessmentResult; target.CprAedCombinedResult = entity.CprAedCombinedResult;
    }

    private static string? MaskCertificateNo(string? certificateNo)
    {
        return !string.IsNullOrWhiteSpace(certificateNo) && certificateNo.Length >= 8
            ? $"{certificateNo[..4]}****{certificateNo[^4..]}"
            : null;
    }
}

internal sealed class SqliteKnowledgeStore : IKnowledgeStore
{
    private const int SummaryId = 1;
    private readonly IDbContextFactory<AedDbContext> factory;
    public SqliteKnowledgeStore(IDbContextFactory<AedDbContext> factory) => this.factory = factory;

    public async Task<KnowledgeSummary> GetSummaryAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await db.KnowledgeSummary.Include(s => s.Items).AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == SummaryId, ct);
        return item is null ? new KnowledgeSummary() : SqliteMapper.ToRecord(item);
    }

    public async Task SaveSummaryAsync(KnowledgeSummary summary, CancellationToken ct)
    {
        summary.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await db.KnowledgeSummary.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == SummaryId, ct);
        if (current is not null) db.KnowledgeSummary.Remove(current);
        db.KnowledgeSummary.Add(SqliteMapper.ToEntity(summary));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
