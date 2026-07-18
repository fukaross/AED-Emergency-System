using System.Data;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed partial class SqliteRescueSessionStore
{
    public async Task<RescueSessionJoinResult> JoinAsync(
        RescueSessionJoinCommand command,
        CancellationToken ct)
    {
        return await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);
            var session = await db.RescueSessions.FirstOrDefaultAsync(
                item => item.HelpRequestId == command.HelpRequestId,
                ct) ?? CreateSession(db, command);
            SqliteRescueSessionWriteSupport.EnsureOpen(session);
            var participant = await db.RescueSessionParticipants.FirstOrDefaultAsync(
                item => item.HelpRequestId == command.HelpRequestId
                    && item.OpenId == command.OpenId,
                ct);
            var joined = participant is null
                ? await AddParticipantAsync(db, command, ct)
                : null;
            if (participant is not null) participant.LastSeenAt = command.Now;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new RescueSessionJoinResult(
                await FindRequiredAsync(command.HelpRequestId, ct),
                joined is not null,
                joined is null ? null : SqliteMapper.ToRecord(joined));
        });
    }

    public async Task<RescueSessionMessageRecord> AppendMessageAsync(
        RescueTextMessageCommand command,
        CancellationToken ct)
    {
        return await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);
            var session = await RequireOpenSessionAsync(db, command.HelpRequestId, ct);
            var sender = await RequireParticipantAsync(
                db,
                new RescueSessionParticipantQuery
                {
                    HelpRequestId = command.HelpRequestId,
                    OpenId = command.SenderOpenId
                },
                ct);
            sender.LastSeenAt = command.Now;
            var message = new RescueSessionMessageEntity
            {
                Id = NewMessageId(),
                HelpRequestId = session.HelpRequestId,
                Seq = await NextSeqAsync(db, session.HelpRequestId, ct),
                Kind = RescueMessageKinds.Text,
                SenderOpenId = command.SenderOpenId,
                SenderName = command.SenderName,
                Text = command.Text,
                CreatedAt = command.Now
            };
            db.RescueSessionMessages.Add(message);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return SqliteMapper.ToRecord(message);
        });
    }

    public async Task<RescueSessionStatusAppendResult> AppendStatusEventAsync(
        RescueSessionStatusCommand command,
        CancellationToken ct)
    {
        SqliteRescueSessionWriteSupport.ValidateStatus(command);
        return await WithWriteLockAsync(ct, () => AppendStatusEventCoreAsync(command, ct));
    }

    private async Task<RescueSessionStatusAppendResult> AppendStatusEventCoreAsync(
        RescueSessionStatusCommand command,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var session = await RequireOpenSessionAsync(db, command.HelpRequestId, ct);
        var sender = await RequireParticipantAsync(
            db,
            new RescueSessionParticipantQuery
            {
                HelpRequestId = command.HelpRequestId,
                OpenId = command.SenderOpenId
            },
            ct);
        var latest = await LatestStatusEventAsync(db, command, ct);
        if ((latest?.Action == RescueEventActions.Mark) == (command.Action == RescueEventActions.Mark))
        {
            await transaction.CommitAsync(ct);
            return new RescueSessionStatusAppendResult(
                false,
                null,
                await ReadParticipantStateAsync(db, command, ct));
        }

        sender.LastSeenAt = command.Now;
        var seq = await NextSeqAsync(db, command.HelpRequestId, ct);
        var message = SqliteRescueSessionWriteSupport.CreateStatusMessage(session, command, seq);
        db.RescueSessionMessages.Add(message);
        await db.SaveChangesAsync(ct);
        var state = await ReadParticipantStateAsync(db, command, ct);
        await transaction.CommitAsync(ct);
        return new RescueSessionStatusAppendResult(true, SqliteMapper.ToRecord(message), state);
    }

    private static RescueSessionEntity CreateSession(AedDbContext db, RescueSessionJoinCommand command)
    {
        var entity = new RescueSessionEntity { HelpRequestId = command.HelpRequestId, CreatedAt = command.Now };
        db.RescueSessions.Add(entity);
        return entity;
    }

    private static async Task<RescueSessionMessageEntity> AddParticipantAsync(
        AedDbContext db,
        RescueSessionJoinCommand command,
        CancellationToken ct)
    {
        db.RescueSessionParticipants.Add(new RescueSessionParticipantEntity
        {
            HelpRequestId = command.HelpRequestId,
            OpenId = command.OpenId,
            DisplayName = command.DisplayName,
            JoinedAt = command.Now,
            LastSeenAt = command.Now
        });
        var joined = new RescueSessionMessageEntity
        {
            Id = NewMessageId(),
            HelpRequestId = command.HelpRequestId,
            Seq = await NextSeqAsync(db, command.HelpRequestId, ct),
            Kind = RescueMessageKinds.StatusEvent,
            SenderOpenId = command.OpenId,
            SenderName = command.DisplayName,
            EventType = RescueEventTypes.Joined,
            Action = RescueEventActions.Mark,
            CreatedAt = command.Now
        };
        db.RescueSessionMessages.Add(joined);
        return joined;
    }

    private static async Task<RescueSessionEntity> RequireOpenSessionAsync(
        AedDbContext db,
        string helpRequestId,
        CancellationToken ct)
    {
        var session = await db.RescueSessions.FirstOrDefaultAsync(
            item => item.HelpRequestId == helpRequestId,
            ct) ?? throw new KeyNotFoundException("急救会话不存在");
        SqliteRescueSessionWriteSupport.EnsureOpen(session);
        return session;
    }

    private static async Task<RescueSessionParticipantEntity> RequireParticipantAsync(
        AedDbContext db,
        RescueSessionParticipantQuery query,
        CancellationToken ct)
    {
        return await db.RescueSessionParticipants.FirstOrDefaultAsync(
            item => item.HelpRequestId == query.HelpRequestId && item.OpenId == query.OpenId,
            ct) ?? throw new KeyNotFoundException("参与者不在急救会话中");
    }

    private static Task<RescueSessionMessageEntity?> LatestStatusEventAsync(
        AedDbContext db,
        RescueSessionStatusCommand command,
        CancellationToken ct)
    {
        return db.RescueSessionMessages.AsNoTracking()
            .Where(item => item.HelpRequestId == command.HelpRequestId
                && item.Kind == RescueMessageKinds.StatusEvent
                && item.SenderOpenId == command.SenderOpenId
                && item.EventType == command.EventType)
            .OrderByDescending(item => item.Seq)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<long> NextSeqAsync(
        AedDbContext db,
        string helpRequestId,
        CancellationToken ct)
    {
        var latest = await db.RescueSessionMessages
            .Where(message => message.HelpRequestId == helpRequestId)
            .MaxAsync(message => (long?)message.Seq, ct);
        return (latest ?? 0) + 1;
    }

    private static async Task<RescueParticipantState> ReadParticipantStateAsync(
        AedDbContext db,
        RescueSessionStatusCommand command,
        CancellationToken ct)
    {
        var events = await db.RescueSessionMessages.AsNoTracking()
            .Where(message => message.HelpRequestId == command.HelpRequestId
                && message.SenderOpenId == command.SenderOpenId
                && (message.EventType == RescueEventTypes.TakenAed
                    || message.EventType == RescueEventTypes.Arrived))
            .OrderByDescending(message => message.Seq)
            .Select(message => new StatusStateRow(message.EventType!, message.Action!))
            .ToArrayAsync(ct);
        return new RescueParticipantState(
            IsMarked(events, RescueEventTypes.TakenAed),
            IsMarked(events, RescueEventTypes.Arrived));
    }

    private static bool IsMarked(IEnumerable<StatusStateRow> events, string eventType) =>
        events.FirstOrDefault(item => item.EventType == eventType)?.Action == RescueEventActions.Mark;

    private sealed record StatusStateRow(string EventType, string Action);

    private static string NewMessageId() => Guid.NewGuid().ToString();
}
