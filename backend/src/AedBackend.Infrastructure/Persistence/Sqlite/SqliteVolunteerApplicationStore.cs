using System.Text.Json;
using AedBackend.Core.Models.Volunteer;
using AedBackend.Core.Services.Volunteer;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class SqliteVolunteerApplicationStore : IVolunteerApplicationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<AedDbContext> factory;

    public SqliteVolunteerApplicationStore(IDbContextFactory<AedDbContext> factory)
    {
        this.factory = factory;
    }

    public async Task<VolunteerApplicationRecord> SubmitAsync(
        string openId,
        VolunteerApplicationPayloadRecord payload,
        int? derivedFromId,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var attempt = await ResolveAttemptNumberAsync(db, openId, derivedFromId, ct);
        var now = DateTimeOffset.UtcNow;
        var item = new VolunteerApplicationRecord
        {
            OpenId = openId,
            Status = VolunteerApplicationStatuses.Submitted,
            DerivedFromId = derivedFromId,
            AttemptNumber = attempt,
            Payload = payload,
            CreatedAt = now,
            UpdatedAt = now
        };
        var entity = SqliteMapper.ToEntity(item, SerializePayload(payload));
        db.VolunteerApplications.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToRecord(entity);
    }

    public async Task<VolunteerApplicationRecord?> GetByIdAsync(int id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.VolunteerApplications.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, ct);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<VolunteerApplicationPage> ListByUserAsync(
        string openId,
        VolunteerApplicationQuery query,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var items = await db.VolunteerApplications.AsNoTracking()
            .Where(item => item.OpenId == openId)
            .ToArrayAsync(ct);
        var ordered = items.Select(ToRecord)
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();
        var total = ordered.Length;
        var pageItems = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();
        var totalPages = total == 0
            ? 0
            : (int)Math.Ceiling(total / (double)query.PageSize);
        return new VolunteerApplicationPage(pageItems, total, query.Page, query.PageSize, totalPages);
    }

    public async Task<VolunteerApplicationPage> ListAllAsync(
        AdminVolunteerApplicationQuery query,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entities = await db.VolunteerApplications.AsNoTracking().ToArrayAsync(ct);
        var ordered = entities.Select(ToRecord)
            .Where(item => MatchesStatus(item, query.Status))
            .Where(item => MatchesKeyword(item, query.Keyword))
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();
        var total = ordered.Length;
        var pageItems = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();
        var totalPages = total == 0
            ? 0
            : (int)Math.Ceiling(total / (double)query.PageSize);
        return new VolunteerApplicationPage(pageItems, total, query.Page, query.PageSize, totalPages);
    }

    private static async Task<int> ResolveAttemptNumberAsync(
        AedDbContext db,
        string openId,
        int? derivedFromId,
        CancellationToken ct)
    {
        if (derivedFromId is null)
        {
            return 1;
        }

        var previous = await db.VolunteerApplications.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == derivedFromId && item.OpenId == openId, ct)
            ?? throw new InvalidOperationException("派生申请不存在");
        return previous.AttemptNumber + 1;
    }

    private static VolunteerApplicationRecord ToRecord(VolunteerApplicationEntity entity)
    {
        return SqliteMapper.ToRecord(entity, DeserializePayload(entity.PayloadJson));
    }

    private static bool MatchesStatus(VolunteerApplicationRecord item, string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            || status == "all"
            || item.Status.Equals(status, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesKeyword(VolunteerApplicationRecord item, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return true;
        var value = keyword.Trim();
        return item.Id.ToString().Contains(value, StringComparison.OrdinalIgnoreCase)
            || item.OpenId.Contains(value, StringComparison.OrdinalIgnoreCase)
            || item.Payload.Name.Contains(value, StringComparison.OrdinalIgnoreCase)
            || item.Payload.Phone.Contains(value, StringComparison.OrdinalIgnoreCase)
            || item.Payload.JoinReason.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static string SerializePayload(VolunteerApplicationPayloadRecord payload)
    {
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static VolunteerApplicationPayloadRecord DeserializePayload(string json)
    {
        return JsonSerializer.Deserialize<VolunteerApplicationPayloadRecord>(json, JsonOptions)
            ?? new VolunteerApplicationPayloadRecord();
    }
}
