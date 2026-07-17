using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class SqliteRescueSessionStore : IRescueSessionStore
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private readonly IDbContextFactory<AedDbContext> factory;

    public SqliteRescueSessionStore(IDbContextFactory<AedDbContext> factory) => this.factory = factory;

    public async Task<RescueSessionRecord?> FindByHelpIdAsync(string helpRequestId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await SqliteStoreHelpers.WithRescueGraph(db.RescueSessions.AsNoTracking())
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.HelpRequestId == helpRequestId, ct);
        return entity is null ? null : SqliteMapper.ToRecord(entity);
    }

    public async Task<RescueSessionRecord> GetOrCreateAsync(string helpRequestId, DateTimeOffset now, CancellationToken ct)
    {
        return await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var existing = await LoadAsync(db, helpRequestId, ct);
            if (existing is not null)
            {
                SqliteRescueSessionWriteSupport.EnsureOpen(existing);
                return SqliteMapper.ToRecord(existing);
            }
            var entity = new RescueSessionEntity { HelpRequestId = helpRequestId, CreatedAt = now };
            db.RescueSessions.Add(entity);
            await db.SaveChangesAsync(ct);
            return SqliteMapper.ToRecord(entity);
        });
    }

    public async Task<RescueSessionJoinResult> JoinAsync(RescueSessionJoinCommand command, CancellationToken ct)
    {
        return await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var session = await LoadAsync(db, command.HelpRequestId, ct) ?? CreateSession(db, command);
            SqliteRescueSessionWriteSupport.EnsureOpen(session);
            var participant = session.Participants.FirstOrDefault(p => p.OpenId == command.OpenId);
            if (participant is null)
            {
                var joined = AddParticipant(session, command);
                await db.SaveChangesAsync(ct);
                return new RescueSessionJoinResult(
                    SqliteMapper.ToRecord(session),
                    true,
                    SqliteMapper.ToRecord(joined));
            }

            participant.LastSeenAt = command.Now;
            await db.SaveChangesAsync(ct);
            return new RescueSessionJoinResult(SqliteMapper.ToRecord(session), false, null);
        });
    }

    public async Task<RescueSessionRecord> AppendMessageAsync(RescueSessionAppendCommand command, CancellationToken ct)
    {
        SqliteRescueSessionWriteSupport.EnsureTextMessage(command);
        return await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var session = await LoadAsync(db, command.HelpRequestId, ct);
            if (session is null) throw new KeyNotFoundException("急救会话不存在");
            SqliteRescueSessionWriteSupport.EnsureOpen(session);
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

    public async Task<RescueSessionStatusAppendResult> AppendStatusEventAsync(
        RescueSessionStatusCommand command,
        CancellationToken ct)
    {
        SqliteRescueSessionWriteSupport.ValidateStatus(command);
        return await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var session = await LoadAsync(db, command.HelpRequestId, ct);
            if (session is null) throw new KeyNotFoundException("急救会话不存在");
            SqliteRescueSessionWriteSupport.EnsureOpen(session);
            var sender = session.Participants.FirstOrDefault(item => item.OpenId == command.SenderOpenId);
            if (sender is null) throw new KeyNotFoundException("参与者不在急救会话中");

            var latest = session.Messages
                .Where(item => item.Kind == RescueMessageKinds.StatusEvent
                    && item.SenderOpenId == command.SenderOpenId
                    && item.EventType == command.EventType)
                .MaxBy(item => item.Seq);
            var current = latest?.Action == RescueEventActions.Mark;
            var desired = command.Action == RescueEventActions.Mark;
            if (current == desired)
                return new RescueSessionStatusAppendResult(SqliteMapper.ToRecord(session), false, null);

            sender.LastSeenAt = command.Now;
            var message = SqliteRescueSessionWriteSupport.CreateStatusMessage(session, command, NextSeq(session));
            session.Messages.Add(message);
            await db.SaveChangesAsync(ct);
            return new RescueSessionStatusAppendResult(
                SqliteMapper.ToRecord(session),
                true,
                SqliteMapper.ToRecord(message));
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
            participant => participant.LastReadSeq = Math.Max(participant.LastReadSeq, lastReadSeq), ct);
    }

    public async Task CloseSessionAsync(string helpRequestId, CancellationToken ct)
    {
        var closedAt = DateTimeOffset.UtcNow;
        await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var session = await db.RescueSessions
                .Include(s => s.Participants)
                .FirstOrDefaultAsync(s => s.HelpRequestId == helpRequestId, ct);
            if (session is null)
            {
                db.RescueSessions.Add(new RescueSessionEntity
                {
                    HelpRequestId = helpRequestId,
                    CreatedAt = closedAt,
                    ClosedAt = closedAt
                });
                await db.SaveChangesAsync(ct);
                return;
            }
            session.ClosedAt ??= closedAt;
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
            .AsSplitQuery()
            .Where(s => s.Participants.Any(p => p.OpenId == openId))
            .ToArrayAsync(ct);
        return items.Select(SqliteMapper.ToRecord).OrderByDescending(s => s.CreatedAt).ToArray();
    }

    public async Task<IReadOnlyList<RescueSessionRecord>> ListAllAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var items = await SqliteStoreHelpers.WithRescueGraph(db.RescueSessions.AsNoTracking())
            .AsSplitQuery()
            .ToArrayAsync(ct);
        return items.Select(SqliteMapper.ToRecord).OrderByDescending(item => item.CreatedAt).ToArray();
    }

    public async Task<IReadOnlyList<RescueSessionOverviewRecord>> ListOverviewsAsync(
        string? viewerOpenId,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await SqliteRescueSessionOverviewReader.ReadAllAsync(db, viewerOpenId, ct);
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
            SqliteRescueSessionWriteSupport.EnsureOpen(session);
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

    private static RescueSessionMessageEntity AddParticipant(RescueSessionEntity session, RescueSessionJoinCommand command)
    {
        session.Participants.Add(new RescueSessionParticipantEntity
        {
            HelpRequestId = session.HelpRequestId,
            OpenId = command.OpenId,
            DisplayName = command.DisplayName,
            JoinedAt = command.Now,
            LastSeenAt = command.Now
        });
        var joined = new RescueSessionMessageEntity
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
        };
        session.Messages.Add(joined);
        return joined;
    }
    private static async Task<RescueSessionEntity?> LoadAsync(
        AedDbContext db,
        string helpRequestId,
        CancellationToken ct)
    {
        return await SqliteStoreHelpers.WithRescueGraph(db.RescueSessions)
            .AsSplitQuery()
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
