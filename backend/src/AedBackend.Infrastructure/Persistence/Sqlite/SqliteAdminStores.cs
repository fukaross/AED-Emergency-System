using AedBackend.Core.Models.Admin;
using AedBackend.Core.Services.Admin;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class SqliteAdminUserStore : IAdminUserStore
{
    private readonly IDbContextFactory<AedDbContext> factory;

    public SqliteAdminUserStore(IDbContextFactory<AedDbContext> factory)
    {
        this.factory = factory;
    }

    public async Task<AdminUserRecord?> FindByNormalizedUsernameAsync(string normalizedUsername, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await WithSessions(db.AdminUsers)
            .FirstOrDefaultAsync(u => u.NormalizedUsername == normalizedUsername, ct);
        return item is null ? null : SqliteMapper.ToRecord(item);
    }

    public async Task<AdminUserRecord?> FindByIdAsync(string id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await WithSessions(db.AdminUsers).FirstOrDefaultAsync(u => u.Id == id, ct);
        return item is null ? null : SqliteMapper.ToRecord(item);
    }

    public async Task<IReadOnlyList<AdminUserRecord>> ListAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var users = await WithSessions(db.AdminUsers).ToArrayAsync(ct);
        return users
            .Select(SqliteMapper.ToRecord)
            .OrderBy(u => u.CreatedAt)
            .ToArray();
    }

    public async Task<bool> HasSuperAdminAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.AdminUsers.AnyAsync(u => u.Role == AdminRoles.SuperAdmin, ct);
    }

    public async Task<AdminUserRecord> AddAsync(AdminUserRecord user, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.AdminUsers.AnyAsync(u => u.NormalizedUsername == user.NormalizedUsername, ct))
            throw new InvalidOperationException("管理员用户名已存在");

        db.AdminUsers.Add(SqliteMapper.ToEntity(user));
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<AdminUserRecord> UpdateAsync(string id, Func<AdminUserRecord, AdminUserRecord> update, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await WithSessions(db.AdminUsers).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (current is null) throw new KeyNotFoundException("管理员不存在");

        var next = update(SqliteMapper.ToRecord(current));
        db.AdminUsers.Remove(current);
        db.AdminUsers.Add(SqliteMapper.ToEntity(next));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return next;
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) throw new KeyNotFoundException("管理员不存在");

        db.AdminUsers.Remove(user);
        await db.SaveChangesAsync(ct);
    }

    private static IQueryable<AdminUserEntity> WithSessions(IQueryable<AdminUserEntity> query)
    {
        return query.Include(u => u.Sessions);
    }
}

internal sealed class SqliteAdminUserProfileStore : IAdminUserProfileStore
{
    private readonly IDbContextFactory<AedDbContext> factory;

    public SqliteAdminUserProfileStore(IDbContextFactory<AedDbContext> factory)
    {
        this.factory = factory;
    }

    public async Task<AdminUserProfileRecord?> FindAsync(string openId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await db.AdminUserProfiles.FindAsync([openId], ct);
        return item is null ? null : SqliteMapper.ToRecord(item);
    }

    public async Task<IReadOnlyDictionary<string, AdminUserProfileRecord>> ListAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var items = await db.AdminUserProfiles.AsNoTracking().ToArrayAsync(ct);
        return items.ToDictionary(x => x.OpenId, SqliteMapper.ToRecord);
    }

    public async Task<AdminUserProfileRecord> UpsertAsync(string openId, Func<AdminUserProfileRecord?, AdminUserProfileRecord> update, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await db.AdminUserProfiles.FindAsync([openId], ct);
        var next = update(current is null ? null : SqliteMapper.ToRecord(current));
        if (current is not null) db.AdminUserProfiles.Remove(current);
        db.AdminUserProfiles.Add(SqliteMapper.ToEntity(next));
        await db.SaveChangesAsync(ct);
        return next;
    }
}

internal sealed class SqliteAdminAuditLogStore : IAdminAuditLogStore
{
    private readonly IDbContextFactory<AedDbContext> factory;

    public SqliteAdminAuditLogStore(IDbContextFactory<AedDbContext> factory)
    {
        this.factory = factory;
    }

    public async Task AppendAsync(AdminAuditLogRecord record, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.AdminAuditLogs.AnyAsync(item => item.Id == record.Id, ct)) return;
        db.AdminAuditLogs.Add(SqliteMapper.ToEntity(record));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (!await db.AdminAuditLogs.AnyAsync(item => item.Id == record.Id, ct)) throw;
        }
    }

    public async Task<IReadOnlyList<AdminAuditLogRecord>> ListAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var logs = await db.AdminAuditLogs
            .AsNoTracking()
            .ToArrayAsync(ct);
        return logs
            .Select(SqliteMapper.ToRecord)
            .OrderByDescending(log => log.CreatedAt)
            .ToArray();
    }
}
