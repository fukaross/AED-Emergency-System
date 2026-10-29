using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.Models.User;
using AedBackend.Core.Services.User;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class SqliteSchoolPersonProfileStore : ISchoolPersonProfileStore
{
    private readonly IDbContextFactory<AedDbContext> factory;
    public SqliteSchoolPersonProfileStore(IDbContextFactory<AedDbContext> factory) => this.factory = factory;

    public async Task<SchoolPersonProfileRecord> CreateAsync(SchoolPersonProfileRecord profile, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var entity = SqliteMapper.ToEntity(profile with { CreatedAt = now, UpdatedAt = now });
        db.SchoolPersonProfiles.Add(entity);
        await db.SaveChangesAsync(ct);
        return SqliteMapper.ToRecord(entity);
    }

    public async Task<SchoolPersonProfileRecord?> GetByIdAsync(int id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.SchoolPersonProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? null : SqliteMapper.ToRecord(entity);
    }

    public async Task<SchoolPersonProfileRecord?> UpdateAsync(int id, SchoolPersonProfileRecord profile, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await db.SchoolPersonProfiles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (current is null) return null;
        Apply(current, profile);
        await db.SaveChangesAsync(ct);
        return SqliteMapper.ToRecord(current);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await db.SchoolPersonProfiles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (current is null) return false;

        var bindings = await db.SchoolBindingApplications
            .Where(x => x.SchoolProfileId == id)
            .ToArrayAsync(ct);
        foreach (var binding in bindings)
        {
            binding.SchoolProfileId = null;
            binding.UpdatedAt = DateTimeOffset.UtcNow;
        }

        db.SchoolPersonProfiles.Remove(current);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<UserIdentityPage<SchoolPersonProfileRecord>> ListAsync(SchoolPersonProfileQuery query, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entities = await db.SchoolPersonProfiles.AsNoTracking().ToArrayAsync(ct);
        var items = entities.Select(SqliteMapper.ToRecord)
            .Where(x => MatchesPersonType(x, query.PersonType))
            .Where(x => MatchesKeyword(x, query.Keyword))
            .OrderByDescending(x => x.UpdatedAt)
            .ToArray();
        return Page(items, query.Page, query.PageSize);
    }

    private static void Apply(SchoolPersonProfileEntity target, SchoolPersonProfileRecord source)
    {
        target.PersonType = source.PersonType;
        target.Name = source.Name;
        target.College = source.College;
        target.ClassName = source.ClassName;
        target.StudentId = source.StudentId;
        target.StaffId = source.StaffId;
        target.Phone = source.Phone;
        target.Status = source.Status;
        target.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static bool MatchesPersonType(SchoolPersonProfileRecord item, string? personType)
    {
        return string.IsNullOrWhiteSpace(personType) || personType == "all"
            || item.PersonType.Equals(personType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesKeyword(SchoolPersonProfileRecord item, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return true;
        var value = keyword.Trim();
        return item.Name.Contains(value, StringComparison.OrdinalIgnoreCase)
            || item.College.Contains(value, StringComparison.OrdinalIgnoreCase)
            || (item.ClassName?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.StudentId?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.StaffId?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static UserIdentityPage<SchoolPersonProfileRecord> Page(
        IReadOnlyList<SchoolPersonProfileRecord> items,
        int page,
        int pageSize)
    {
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var totalPages = items.Count == 0 ? 0 : (int)Math.Ceiling(items.Count / (double)pageSize);
        return new UserIdentityPage<SchoolPersonProfileRecord>(pageItems, items.Count, page, pageSize, totalPages);
    }
}

internal sealed class SqliteSchoolBindingApplicationStore : ISchoolBindingApplicationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<AedDbContext> factory;
    public SqliteSchoolBindingApplicationStore(IDbContextFactory<AedDbContext> factory) => this.factory = factory;

    public async Task<SchoolBindingApplicationRecord> SubmitAsync(
        string openId,
        SchoolBindingPayloadRecord payload,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await EnsureNoPendingAsync(db, openId, ct);
        var now = DateTimeOffset.UtcNow;
        var record = new SchoolBindingApplicationRecord
        {
            OpenId = openId,
            Status = SchoolBindingStatuses.Submitted,
            Payload = payload,
            CreatedAt = now,
            UpdatedAt = now
        };
        var entity = SqliteMapper.ToEntity(record, Serialize(payload));
        db.SchoolBindingApplications.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToRecord(entity);
    }

    public async Task<SchoolBindingApplicationRecord?> GetByIdAsync(int id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.SchoolBindingApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<UserIdentityPage<SchoolBindingApplicationRecord>> ListByUserAsync(
        string openId,
        UserIdentityQuery query,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entities = await db.SchoolBindingApplications.AsNoTracking()
            .Where(x => x.OpenId == openId).ToArrayAsync(ct);
        var items = entities.Select(ToRecord).OrderByDescending(x => x.CreatedAt).ToArray();
        return Page(items, query.Page, query.PageSize);
    }

    public async Task<UserIdentityPage<SchoolBindingApplicationRecord>> ListAllAsync(
        UserIdentityAdminQuery query,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entities = await db.SchoolBindingApplications.AsNoTracking().ToArrayAsync(ct);
        var items = entities.Select(ToRecord).Where(x => MatchesStatus(x.Status, query.Status))
            .Where(x => MatchesKeyword(x, query.Keyword)).OrderByDescending(x => x.CreatedAt).ToArray();
        return Page(items, query.Page, query.PageSize);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.SchoolBindingApplications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return false;

        db.SchoolBindingApplications.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static SchoolBindingApplicationRecord ToRecord(SchoolBindingApplicationEntity entity)
    {
        return SqliteMapper.ToRecord(entity, Deserialize<SchoolBindingPayloadRecord>(entity.PayloadJson));
    }

    private static bool MatchesKeyword(SchoolBindingApplicationRecord item, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return true;
        var value = keyword.Trim();
        return item.Id.ToString().Contains(value, StringComparison.OrdinalIgnoreCase)
            || item.OpenId.Contains(value, StringComparison.OrdinalIgnoreCase)
            || item.Payload.Name.Contains(value, StringComparison.OrdinalIgnoreCase)
            || item.Payload.College.Contains(value, StringComparison.OrdinalIgnoreCase)
            || (item.Payload.StudentId?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false)
            || (item.Payload.StaffId?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    internal static string Serialize<T>(T item) => JsonSerializer.Serialize(item, JsonOptions);
    internal static T Deserialize<T>(string json) where T : new() => JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();

    private static async Task EnsureNoPendingAsync(AedDbContext db, string openId, CancellationToken ct)
    {
        var exists = await db.SchoolBindingApplications
            .AnyAsync(x => x.OpenId == openId && x.Status == SchoolBindingStatuses.Submitted, ct);
        if (exists) throw new InvalidOperationException("已有待审核申请");
    }

    internal static bool MatchesStatus(string status, string? queryStatus)
    {
        return string.IsNullOrWhiteSpace(queryStatus) || queryStatus == "all"
            || status.Equals(queryStatus, StringComparison.OrdinalIgnoreCase);
    }

    internal static UserIdentityPage<T> Page<T>(IReadOnlyList<T> items, int page, int pageSize)
    {
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var totalPages = items.Count == 0 ? 0 : (int)Math.Ceiling(items.Count / (double)pageSize);
        return new UserIdentityPage<T>(pageItems, items.Count, page, pageSize, totalPages);
    }
}

internal sealed class SqliteResponderClaimApplicationStore : IResponderClaimApplicationStore
{
    private readonly IDbContextFactory<AedDbContext> factory;
    public SqliteResponderClaimApplicationStore(IDbContextFactory<AedDbContext> factory) => this.factory = factory;

    public async Task<ResponderClaimApplicationRecord> SubmitAsync(
        string openId,
        ResponderClaimPayloadRecord payload,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await EnsureNoPendingAsync(db, openId, ct);
        var now = DateTimeOffset.UtcNow;
        var record = new ResponderClaimApplicationRecord
        {
            OpenId = openId,
            Status = ResponderClaimStatuses.Submitted,
            Payload = payload,
            CreatedAt = now,
            UpdatedAt = now
        };
        var entity = SqliteMapper.ToEntity(record, SqliteSchoolBindingApplicationStore.Serialize(payload));
        db.ResponderClaimApplications.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToRecord(entity);
    }

    public async Task<ResponderClaimApplicationRecord?> GetByIdAsync(int id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.ResponderClaimApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<UserIdentityPage<ResponderClaimApplicationRecord>> ListByUserAsync(
        string openId,
        UserIdentityQuery query,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entities = await db.ResponderClaimApplications.AsNoTracking()
            .Where(x => x.OpenId == openId).ToArrayAsync(ct);
        var items = entities.Select(ToRecord).OrderByDescending(x => x.CreatedAt).ToArray();
        return SqliteSchoolBindingApplicationStore.Page(items, query.Page, query.PageSize);
    }

    public async Task<UserIdentityPage<ResponderClaimApplicationRecord>> ListAllAsync(
        UserIdentityAdminQuery query,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entities = await db.ResponderClaimApplications.AsNoTracking().ToArrayAsync(ct);
        var items = entities.Select(ToRecord).Where(x => SqliteSchoolBindingApplicationStore.MatchesStatus(x.Status, query.Status))
            .Where(x => MatchesKeyword(x, query.Keyword)).OrderByDescending(x => x.CreatedAt).ToArray();
        return SqliteSchoolBindingApplicationStore.Page(items, query.Page, query.PageSize);
    }

    private async Task EnsureNoPendingAsync(AedDbContext db, string openId, CancellationToken ct)
    {
        var exists = await db.ResponderClaimApplications
            .AnyAsync(x => x.OpenId == openId && x.Status == ResponderClaimStatuses.Submitted, ct);
        if (exists) throw new InvalidOperationException("已有待审核申请");
    }

    private static ResponderClaimApplicationRecord ToRecord(ResponderClaimApplicationEntity entity)
    {
        return SqliteMapper.ToRecord(
            entity,
            SqliteSchoolBindingApplicationStore.Deserialize<ResponderClaimPayloadRecord>(entity.PayloadJson));
    }

    private static bool MatchesKeyword(ResponderClaimApplicationRecord item, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return true;
        var value = keyword.Trim();
        return item.Id.ToString().Contains(value, StringComparison.OrdinalIgnoreCase)
            || item.OpenId.Contains(value, StringComparison.OrdinalIgnoreCase)
            || item.Payload.Name.Contains(value, StringComparison.OrdinalIgnoreCase)
            || item.Payload.Phone.Contains(value, StringComparison.OrdinalIgnoreCase)
            || (item.Payload.CertificateNo?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
