using AedBackend.Core.DTOs.WeChat;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.WeChat;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class SqliteExperienceUserStore : IExperienceUserStore
{
    private const int TokenLifetimeSeconds = 30 * 24 * 3600;
    private readonly IDbContextFactory<AedDbContext> factory;

    public SqliteExperienceUserStore(IDbContextFactory<AedDbContext> factory)
    {
        this.factory = factory;
    }

    public async Task<SaveLoginResult> SaveLoginAsync(WeChatLoginSession session, string tokenHash, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await WithSubscriptions(db.ExperienceUsers)
            .FirstOrDefaultAsync(u => u.OpenId == session.OpenId, ct);
        var now = DateTimeOffset.UtcNow;
        var next = current is null ? CreateUser(session, tokenHash, now) : UpdateLogin(current, session, tokenHash, now);

        if (current is not null) db.ExperienceUsers.Remove(current);
        db.ExperienceUsers.Add(SqliteMapper.ToEntity(next));
        await db.SaveChangesAsync(ct);
        return new SaveLoginResult(next, current is null);
    }

    public async Task<ExperienceUserRecord?> FindByTokenHashAsync(string tokenHash, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await WithSubscriptions(db.ExperienceUsers)
            .FirstOrDefaultAsync(u => u.SessionTokenHash == tokenHash, ct);
        if (item is null || DateTimeOffset.UtcNow > item.LastLoginAt.AddSeconds(TokenLifetimeSeconds))
            return null;
        return SqliteMapper.ToRecord(item);
    }

    public async Task<SaveLoginResult?> RefreshTokenAsync(string oldTokenHash, string newTokenHash, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await WithSubscriptions(db.ExperienceUsers)
            .FirstOrDefaultAsync(u => u.SessionTokenHash == oldTokenHash, ct);
        if (current is null || DateTimeOffset.UtcNow > current.LastLoginAt.AddSeconds(TokenLifetimeSeconds))
            return null;

        var updated = SqliteMapper.ToRecord(current) with
        {
            SessionTokenHash = newTokenHash,
            LastLoginAt = DateTimeOffset.UtcNow
        };
        db.ExperienceUsers.Remove(current);
        db.ExperienceUsers.Add(SqliteMapper.ToEntity(updated));
        await db.SaveChangesAsync(ct);
        return new SaveLoginResult(updated, false);
    }

    public async Task SaveSubscriptionAsync(string tokenHash, SaveSubscribeDecisionRequest request, CancellationToken ct)
    {
        var user = await FindByTokenHashAsync(tokenHash, ct)
            ?? throw new InvalidOperationException("体验用户登录态不存在");
        var subscriptions = user.Subscriptions.ToDictionary();
        subscriptions[request.TemplateId] = new SubscriptionRecord(request.TemplateId, request.Status, DateTimeOffset.UtcNow);
        await ReplaceUserAsync(user with { Subscriptions = subscriptions }, ct);
    }

    public async Task<ExperienceUserRecord> SavePhoneAsync(string tokenHash, string phone, CancellationToken ct)
    {
        var user = await FindByTokenHashAsync(tokenHash, ct)
            ?? throw new InvalidOperationException("体验用户登录态不存在");
        var updated = user with { Phone = phone };
        await ReplaceUserAsync(updated, ct);
        return updated;
    }

    public async Task<IReadOnlyList<ExperienceUserRecord>> ListUsersAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var users = await WithSubscriptions(db.ExperienceUsers).ToArrayAsync(ct);
        return users.Select(SqliteMapper.ToRecord).ToArray();
    }

    private async Task ReplaceUserAsync(ExperienceUserRecord user, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await WithSubscriptions(db.ExperienceUsers).FirstAsync(u => u.OpenId == user.OpenId, ct);
        db.ExperienceUsers.Remove(current);
        db.ExperienceUsers.Add(SqliteMapper.ToEntity(user));
        await db.SaveChangesAsync(ct);
    }

    private static ExperienceUserRecord CreateUser(WeChatLoginSession session, string tokenHash, DateTimeOffset now)
    {
        return new ExperienceUserRecord(session.OpenId, session.UnionId, null, tokenHash, now, now, new Dictionary<string, SubscriptionRecord>());
    }

    private static ExperienceUserRecord UpdateLogin(ExperienceUserEntity current, WeChatLoginSession session, string tokenHash, DateTimeOffset now)
    {
        return SqliteMapper.ToRecord(current) with { UnionId = session.UnionId, SessionTokenHash = tokenHash, LastLoginAt = now };
    }

    private static IQueryable<ExperienceUserEntity> WithSubscriptions(IQueryable<ExperienceUserEntity> query)
    {
        return query.Include(u => u.Subscriptions);
    }
}
