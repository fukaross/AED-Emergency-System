using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Help;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class SqliteHelpRequestStore : IHelpRequestStore
{
    private readonly IDbContextFactory<AedDbContext> factory;
    public SqliteHelpRequestStore(IDbContextFactory<AedDbContext> factory) => this.factory = factory;

    public async Task SaveAsync(HelpRequestRecord record, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.HelpRequests.Add(SqliteMapper.ToEntity(record));
        await db.SaveChangesAsync(ct);
    }

    public async Task<HelpRequestRecord?> FindByIdOrRequestNoAsync(string idOrRequestNo, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await SqliteStoreHelpers.WithHelpGraph(db.HelpRequests.AsNoTracking())
            .FirstOrDefaultAsync(h => h.Id == idOrRequestNo || h.RequestNo == idOrRequestNo, ct);
        return item is null ? null : SqliteMapper.ToRecord(item);
    }

    public async Task<IReadOnlyList<HelpRequestRecord>> ListByUserAsync(string userId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var items = await SqliteStoreHelpers.WithHelpGraph(db.HelpRequests.AsNoTracking())
            .Where(h => h.UserId == userId || h.Reports.Any(r => r.ReporterOpenId == userId))
            .ToArrayAsync(ct);
        return items
            .Select(SqliteMapper.ToRecord)
            .OrderByDescending(h => h.CreatedAt)
            .ToArray();
    }

    public async Task<IReadOnlyList<HelpRequestRecord>> ListAllAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var items = await SqliteStoreHelpers.WithHelpGraph(db.HelpRequests.AsNoTracking()).ToArrayAsync(ct);
        return items
            .Select(SqliteMapper.ToRecord)
            .OrderByDescending(h => h.CreatedAt)
            .ToArray();
    }

    public async Task<bool> HasActiveRequestAsync(string userId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.HelpRequests.AnyAsync(h =>
            h.UserId == userId && HelpStatuses.Active.Contains(h.Status), ct);
    }

    public async Task<HelpRequestRecord> AddReportAsync(HelpReportUpdate update, CancellationToken ct)
    {
        return await ReplaceAsync(update.HelpRequestId, item => AddReportToRequest(item, update), ct);
    }

    public async Task<HelpRequestRecord> CloseAsync(string id, string status, string? note, DateTimeOffset closedAt, CancellationToken ct)
    {
        return await ReplaceAsync(id, item => CloseRequest(item, status, note, closedAt), ct);
    }

    public async Task<HelpRequestRecord> TransitionToRespondingAsync(string id, DateTimeOffset now, CancellationToken ct)
    {
        return await ReplaceAsync(id, item => ToResponding(item, now), ct);
    }

    private async Task<HelpRequestRecord> ReplaceAsync(
        string id,
        Func<HelpRequestRecord, HelpRequestRecord> update,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await SqliteStoreHelpers.WithHelpGraph(db.HelpRequests)
            .FirstOrDefaultAsync(h => h.Id == id || h.RequestNo == id, ct);
        if (current is null) throw new KeyNotFoundException("求助不存在");

        var next = update(SqliteMapper.ToRecord(current));
        db.HelpRequests.Remove(current);
        db.HelpRequests.Add(SqliteMapper.ToEntity(next));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return next;
    }

    private static HelpRequestRecord AddReportToRequest(HelpRequestRecord item, HelpReportUpdate update)
    {
        EnsureOpen(item);
        return item with
        {
            Status = update.NextStatus,
            ClosedAt = update.NextStatus == HelpStatuses.FalseAlarm ? update.UpdatedAt : item.ClosedAt,
            Reports = [.. item.Reports, update.Report],
            Timeline = AppendTimeline(item, update.NextStatus, update.UpdatedAt, null)
        };
    }

    private static HelpRequestRecord CloseRequest(
        HelpRequestRecord item,
        string status,
        string? note,
        DateTimeOffset closedAt)
    {
        EnsureOpen(item);
        return item with
        {
            Status = status,
            ClosedAt = closedAt,
            CloseNote = note,
            Timeline = AppendTimeline(item, status, closedAt, note)
        };
    }

    /** 首个急救员进入会话时 published → responding；其余状态原样返回（BackEnd.md §4.5A 状态机约定）。 */
    private static HelpRequestRecord ToResponding(HelpRequestRecord item, DateTimeOffset now)
    {
        return item.Status == HelpStatuses.Published
            ? item with
            {
                Status = HelpStatuses.Responding,
                Timeline = AppendTimeline(item, HelpStatuses.Responding, now, null)
            }
            : item;
    }

    private static void EnsureOpen(HelpRequestRecord item)
    {
        if (HelpStatuses.IsClosed(item.Status)) throw new InvalidOperationException("求助已结束");
    }

    private static IReadOnlyList<HelpTimelineRecord> AppendTimeline(
        HelpRequestRecord item,
        string action,
        DateTimeOffset timestamp,
        string? note)
    {
        return [.. item.Timeline, new HelpTimelineRecord(action, timestamp, note)];
    }
}
