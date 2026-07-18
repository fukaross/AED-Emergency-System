using System.Data;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed partial class SqliteRescueSessionStore : IRescueSessionStore
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

    public async Task<RescueSessionParticipantAccess?> FindParticipantAccessAsync(
        RescueSessionParticipantQuery query,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await (
            from participant in db.RescueSessionParticipants.AsNoTracking()
            join session in db.RescueSessions.AsNoTracking()
                on participant.HelpRequestId equals session.HelpRequestId
            where participant.HelpRequestId == query.HelpRequestId
                && participant.OpenId == query.OpenId
            select new { session.ClosedAt, Participant = participant })
            .FirstOrDefaultAsync(ct);
        return row is null
            ? null
            : new RescueSessionParticipantAccess(
                row.ClosedAt,
                SqliteMapper.ToRecord(row.Participant));
    }

    public async Task<RescueSessionRecord> GetOrCreateAsync(string helpRequestId, DateTimeOffset now, CancellationToken ct)
    {
        return await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);
            var existing = await db.RescueSessions.FirstOrDefaultAsync(
                session => session.HelpRequestId == helpRequestId,
                ct);
            if (existing is not null)
            {
                SqliteRescueSessionWriteSupport.EnsureOpen(existing);
                await transaction.CommitAsync(ct);
                return await FindRequiredAsync(helpRequestId, ct);
            }
            var entity = new RescueSessionEntity { HelpRequestId = helpRequestId, CreatedAt = now };
            db.RescueSessions.Add(entity);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return await FindRequiredAsync(helpRequestId, ct);
        });
    }

    public async Task<RescueSessionParticipantRecord> UpdateLocationAsync(
        RescueSessionLocationCommand command,
        CancellationToken ct)
    {
        return await UpdateParticipantAsync(
            new ParticipantMutation
            {
                HelpRequestId = command.HelpRequestId,
                OpenId = command.OpenId,
                Apply = participant => ApplyLocation(participant, command)
            },
            ct);
    }

    public async Task<long> MarkReadAsync(RescueSessionReadCommand command, CancellationToken ct)
    {
        var participant = await UpdateParticipantAsync(
            new ParticipantMutation
            {
                HelpRequestId = command.HelpRequestId,
                OpenId = command.OpenId,
                Apply = item => item.LastReadSeq = Math.Max(item.LastReadSeq, command.LastReadSeq)
            },
            ct);
        return participant.LastReadSeq;
    }

    public async Task CloseSessionAsync(
        string helpRequestId,
        DateTimeOffset closedAt,
        CancellationToken ct)
    {
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

    public async Task<IReadOnlyList<RescueSessionOverviewRecord>> ListOverviewsAsync(
        RescueSessionOverviewQuery query,
        CancellationToken ct)
    {
        if (query.HelpRequestIds.Count == 0) return [];
        await using var db = await factory.CreateDbContextAsync(ct);
        return await SqliteRescueSessionOverviewReader.ReadAsync(db, query, ct);
    }

    private async Task<RescueSessionParticipantRecord> UpdateParticipantAsync(
        ParticipantMutation mutation,
        CancellationToken ct)
    {
        return await WithWriteLockAsync(ct, async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);
            var session = await db.RescueSessions.FirstOrDefaultAsync(
                item => item.HelpRequestId == mutation.HelpRequestId,
                ct);
            if (session is null) throw new KeyNotFoundException("急救会话不存在");
            SqliteRescueSessionWriteSupport.EnsureOpen(session);
            var participant = await db.RescueSessionParticipants.FirstOrDefaultAsync(
                item => item.HelpRequestId == mutation.HelpRequestId && item.OpenId == mutation.OpenId,
                ct);
            if (participant is null) throw new KeyNotFoundException("参与者不在急救会话中");
            mutation.Apply(participant);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return SqliteMapper.ToRecord(participant);
        });
    }

    private static void ApplyLocation(
        RescueSessionParticipantEntity participant,
        RescueSessionLocationCommand command)
    {
        if (participant.LocationReportedAt is not null
            && command.Now < participant.LocationReportedAt)
            return;
        participant.LastLat = command.Lat;
        participant.LastLng = command.Lng;
        participant.LocationReportedAt = command.Now;
        participant.LastSeenAt = command.Now;
    }

    private async Task<RescueSessionRecord> FindRequiredAsync(
        string helpRequestId,
        CancellationToken ct)
    {
        return await FindByHelpIdAsync(helpRequestId, ct)
            ?? throw new KeyNotFoundException("急救会话不存在");
    }

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

    private sealed record ParticipantMutation
    {
        public required string HelpRequestId { get; init; }
        public required string OpenId { get; init; }
        public required Action<RescueSessionParticipantEntity> Apply { get; init; }
    }
}
