using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class SqliteRescueSessionStore : IRescueSessionStore
{
    // 单实例部署：写路径用静态锁串行化，seq 在锁内取 max+1，唯一索引兜底。
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private readonly IDbContextFactory<AedDbContext> factory;

    public SqliteRescueSessionStore(IDbContextFactory<AedDbContext> factory) => this.factory = factory;

    public async Task<RescueSessionRecord?> FindByHelpIdAsync(string helpRequestId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await SqliteStoreHelpers.WithRescueGraph(db.RescueSessions.AsNoTracking())
            .FirstOrDefaultAsync(s => s.HelpRequestId == helpRequestId, ct);
        return entity is null ? null : SqliteMapper.ToRecord(entity);
    }

    public async Task<RescueSessionRecord> GetOrCreateAsync(string helpRequestId, DateTimeOffset now, CancellationToken ct)
    {
        return await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var existing = await LoadAsync(db, helpRequestId, ct);
            if (existing is not null) return SqliteMapper.ToRecord(existing);
            var entity = new RescueSessionEntity { HelpRequestId = helpRequestId, CreatedAt = now };
            db.RescueSessions.Add(entity);
            await db.SaveChangesAsync(ct);
            return SqliteMapper.ToRecord(entity);
        });
    }

    public async Task<RescueSessionRecord> JoinAsync(RescueSessionJoinCommand command, CancellationToken ct)
    {
        return await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var session = await LoadAsync(db, command.HelpRequestId, ct) ?? CreateSession(db, command);
            var participant = session.Participants.FirstOrDefault(p => p.OpenId == command.OpenId);
            if (participant is null)
            {
                AddParticipant(session, command);
            }
            else
            {
                participant.LastSeenAt = command.Now;
            }

            await db.SaveChangesAsync(ct);
            return SqliteMapper.ToRecord(session);
        });
    }

    public async Task<RescueSessionRecord> AppendMessageAsync(RescueSessionAppendCommand command, CancellationToken ct)
    {
        return await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var session = await LoadAsync(db, command.HelpRequestId, ct);
            if (session is null) throw new KeyNotFoundException("急救会话不存在");
            var sender = session.Participants.FirstOrDefault(p => p.OpenId == command.SenderOpenId);
            if (sender is null) throw new KeyNotFoundException("参与者不在急救会话中");
            sender.LastSeenAt = command.Now;
            session.Messages.Add(new RescueSessionMessageEntity
            {
                Id = NewMessageId(),
                HelpRequestId = session.HelpRequestId,
                Seq = NextSeq(session),
                Kind = command.Kind,
                SenderOpenId = command.SenderOpenId,
                SenderName = command.SenderName,
                Text = command.Text,
                EventType = command.EventType,
                Action = command.Action,
                CreatedAt = command.Now
            });
            await db.SaveChangesAsync(ct);
            return SqliteMapper.ToRecord(session);
        });
    }

    public async Task<RescueSessionRecord> UpdateLocationAsync(RescueSessionLocationCommand command, CancellationToken ct)
    {
        return await UpdateParticipantAsync(command.HelpRequestId, command.OpenId, participant =>
        {
            participant.LastLat = command.Lat;
            participant.LastLng = command.Lng;
            participant.LocationReportedAt = command.Now;
            participant.LastSeenAt = command.Now;
        }, ct);
    }

    public async Task<RescueSessionRecord> MarkReadAsync(
        string helpRequestId, string openId, long lastReadSeq, CancellationToken ct)
    {
        return await UpdateParticipantAsync(helpRequestId, openId,
            participant => participant.LastReadSeq = lastReadSeq, ct);
    }

    public async Task CloseSessionAsync(string helpRequestId, CancellationToken ct)
    {
        await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var session = await db.RescueSessions
                .Include(s => s.Participants)
                .FirstOrDefaultAsync(s => s.HelpRequestId == helpRequestId, ct);
            // 关闭是幂等收尾：会话不存在即无位置可清，静默返回属于合理行为。
            if (session is null) return;
            foreach (var participant in session.Participants)
            {
                participant.LastLat = null;
                participant.LastLng = null;
                participant.LocationReportedAt = null;
            }

            await db.SaveChangesAsync(ct);
        });
    }

    public async Task<IReadOnlyList<RescueSessionRecord>> ListByParticipantAsync(string openId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var items = await SqliteStoreHelpers.WithRescueGraph(db.RescueSessions.AsNoTracking())
            .Where(s => s.Participants.Any(p => p.OpenId == openId))
            .ToArrayAsync(ct);
        return items.Select(SqliteMapper.ToRecord).OrderByDescending(s => s.CreatedAt).ToArray();
    }

    public async Task<IReadOnlyList<RescueSessionRecord>> ListAllAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var items = await SqliteStoreHelpers.WithRescueGraph(db.RescueSessions.AsNoTracking()).ToArrayAsync(ct);
        return items.Select(SqliteMapper.ToRecord).OrderByDescending(s => s.CreatedAt).ToArray();
    }

    private async Task<RescueSessionRecord> UpdateParticipantAsync(
        string helpRequestId,
        string openId,
        Action<RescueSessionParticipantEntity> update,
        CancellationToken ct)
    {
        return await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var session = await LoadAsync(db, helpRequestId, ct);
            if (session is null) throw new KeyNotFoundException("急救会话不存在");
            var participant = session.Participants.FirstOrDefault(p => p.OpenId == openId);
            if (participant is null) throw new KeyNotFoundException("参与者不在急救会话中");
            update(participant);
            await db.SaveChangesAsync(ct);
            return SqliteMapper.ToRecord(session);
        });
    }

    private static RescueSessionEntity CreateSession(AedDbContext db, RescueSessionJoinCommand command)
    {
        var entity = new RescueSessionEntity { HelpRequestId = command.HelpRequestId, CreatedAt = command.Now };
        db.RescueSessions.Add(entity);
        return entity;
    }

    private static void AddParticipant(RescueSessionEntity session, RescueSessionJoinCommand command)
    {
        session.Participants.Add(new RescueSessionParticipantEntity
        {
            HelpRequestId = session.HelpRequestId,
            OpenId = command.OpenId,
            DisplayName = command.DisplayName,
            JoinedAt = command.Now,
            LastSeenAt = command.Now
        });
        session.Messages.Add(new RescueSessionMessageEntity
        {
            Id = NewMessageId(),
            HelpRequestId = session.HelpRequestId,
            Seq = NextSeq(session),
            Kind = RescueMessageKinds.StatusEvent,
            SenderOpenId = command.OpenId,
            SenderName = command.DisplayName,
            EventType = RescueEventTypes.Joined,
            Action = RescueEventActions.Mark,
            CreatedAt = command.Now
        });
    }

    private static async Task<RescueSessionEntity?> LoadAsync(
        AedDbContext db,
        string helpRequestId,
        CancellationToken ct)
    {
        return await SqliteStoreHelpers.WithRescueGraph(db.RescueSessions)
            .FirstOrDefaultAsync(s => s.HelpRequestId == helpRequestId, ct);
    }

    private static long NextSeq(RescueSessionEntity session) =>
        session.Messages.Count == 0 ? 1 : session.Messages.Max(m => m.Seq) + 1;

    private static string NewMessageId() => Guid.NewGuid().ToString();

    private static async Task<T> WithWriteLockAsync<T>(CancellationToken ct, Func<Task<T>> action)
    {
        await WriteLock.WaitAsync(ct);
        try
        {
            return await action();
        }
        finally
        {
            WriteLock.Release();
        }
    }

    private static async Task WithWriteLockAsync(CancellationToken ct, Func<Task> action)
    {
        await WriteLock.WaitAsync(ct);
        try
        {
            await action();
        }
        finally
        {
            WriteLock.Release();
        }
    }
}
