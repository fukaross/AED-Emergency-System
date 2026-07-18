using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Help;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Help;

// [Transitional: JSON -> MySQL] Keep schema close to BackEnd.md tables.
public sealed class JsonHelpRequestStore : IHelpRequestStore
{
    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonHelpRequestStore(IOptions<WeChatOptions> options)
    {
        filePath = Path.GetFullPath(options.Value.HelpRequestsFilePath);
    }

    public async Task<bool> TrySaveIfNoActiveAsync(
        HelpRequestRecord record,
        CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            var database = await ReadDatabaseAsync(cancellationToken);
            if (record.UserId is not null && database.Requests.Any(item =>
                item.UserId == record.UserId && HelpStatuses.Active.Contains(item.Status)))
            {
                return false;
            }

            EnsureUniqueIdentity(database, record);
            database.Requests.Insert(0, record);
            await WriteDatabaseAsync(database);
            return true;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<HelpRequestRecord?> FindByIdOrRequestNoAsync(
        string idOrRequestNo,
        CancellationToken cancellationToken)
    {
        return await WithReadAsync(
            database => FindRequest(database, idOrRequestNo),
            cancellationToken);
    }

    public async Task<IReadOnlyList<HelpRequestRecord>> ListByUserAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await WithReadAsync(
            database => database.Requests
                .Where(item => item.UserId == userId
                    || item.Reports.Any(r => r.ReporterOpenId == userId))
                .OrderByDescending(item => item.CreatedAt)
                .ToArray(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<HelpRequestRecord>> ListAllAsync(
        CancellationToken cancellationToken)
    {
        return await WithReadAsync(
            database => database.Requests
                .OrderByDescending(item => item.CreatedAt)
                .ToArray(),
            cancellationToken);
    }

    public async Task<HelpRequestOverviewPage> ListOverviewsAsync(
        HelpRequestOverviewQuery query,
        CancellationToken cancellationToken)
    {
        query.Validate();
        return await WithReadAsync(
            database => CreateOverviewPage(database.Requests, query),
            cancellationToken);
    }

    private static void EnsureUniqueIdentity(
        HelpRequestDatabase database,
        HelpRequestRecord record)
    {
        if (database.Requests.Any(item => item.Id == record.Id
            || item.RequestNo.Equals(record.RequestNo, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("求助 ID 或 requestNo 重复");
        }
    }

    public async Task<HelpRequestRecord> AddReportAsync(
        HelpReportUpdate update,
        CancellationToken cancellationToken)
    {
        return await WithUpdateAsync(
            database => ReplaceRequest(database, update.HelpRequestId, item =>
                AddReportToRequest(item, update)),
            cancellationToken);
    }

    public async Task<HelpRequestRecord> CloseAsync(
        HelpClosureUpdate update,
        CancellationToken cancellationToken)
    {
        return await WithUpdateAsync(
            database => ReplaceRequest(database, update.HelpRequestId, item =>
                CloseRequest(item, update)),
            cancellationToken);
    }

    public async Task<HelpRequestRecord> TransitionToRespondingAsync(
        string id,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await WithUpdateAsync(
            database => ReplaceRequest(database, id, item => ToResponding(item, now)),
            cancellationToken);
    }

    private static HelpRequestRecord? FindRequest(HelpRequestDatabase database, string id)
    {
        return database.Requests.FirstOrDefault(item =>
            item.Id == id || item.RequestNo.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    private static HelpRequestOverviewPage CreateOverviewPage(
        IReadOnlyCollection<HelpRequestRecord> records,
        HelpRequestOverviewQuery query)
    {
        var filtered = records
            .Where(record => query.Scope == HelpRequestOverviewScope.History
                ? HelpStatuses.IsClosed(record.Status)
                : HelpStatuses.Active.Contains(record.Status))
            .OrderByDescending(record => record.CreatedAt)
            .ToArray();
        var page = filtered.Skip(query.Offset);
        if (query.Limit is not null) page = page.Take(query.Limit.Value);
        return new HelpRequestOverviewPage(
            page.Select(ToOverview).ToArray(),
            filtered.Length);
    }

    private static HelpRequestOverviewRecord ToOverview(HelpRequestRecord record) => new()
    {
        Id = record.Id,
        RequestNo = record.RequestNo,
        Status = record.Status,
        Type = record.Type,
        Address = record.Address,
        CreatedAt = record.CreatedAt
    };

    private static HelpRequestRecord AddReportToRequest(
        HelpRequestRecord item,
        HelpReportUpdate update)
    {
        EnsureOpen(item);
        var timeline = AppendTimeline(item, update.NextStatus, update.UpdatedAt, null);
        return item with
        {
            Status = update.NextStatus,
            ClosedAt = update.NextStatus == HelpStatuses.FalseAlarm ? update.UpdatedAt : item.ClosedAt,
            Reports = [.. item.Reports, update.Report],
            Timeline = timeline
        };
    }

    private static HelpRequestRecord CloseRequest(
        HelpRequestRecord item,
        HelpClosureUpdate update)
    {
        EnsureOpen(item);
        return item with
        {
            Status = update.Status,
            ClosedAt = update.ClosedAt,
            CloseNote = update.Note,
            Timeline = AppendTimeline(item, update.Status, update.ClosedAt, update.Note)
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
        if (HelpStatuses.IsClosed(item.Status))
        {
            throw new HelpAlreadyClosedException();
        }
    }

    private static IReadOnlyList<HelpTimelineRecord> AppendTimeline(
        HelpRequestRecord item,
        string action,
        DateTimeOffset timestamp,
        string? note)
    {
        return [.. item.Timeline, new HelpTimelineRecord(action, timestamp, note)];
    }

    private static HelpRequestRecord ReplaceRequest(
        HelpRequestDatabase database,
        string id,
        Func<HelpRequestRecord, HelpRequestRecord> update)
    {
        var index = database.Requests.FindIndex(item =>
            item.Id == id || item.RequestNo.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new KeyNotFoundException("求助不存在");
        }

        var next = update(database.Requests[index]);
        database.Requests[index] = next;
        return next;
    }

    private async Task<T> WithReadAsync<T>(
        Func<HelpRequestDatabase, T> read,
        CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            return read(await ReadDatabaseAsync(cancellationToken));
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<T> WithUpdateAsync<T>(
        Func<HelpRequestDatabase, T> update,
        CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            var database = await ReadDatabaseAsync(cancellationToken);
            var result = update(database);
            await WriteDatabaseAsync(database);
            return result;
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<HelpRequestDatabase> ReadDatabaseAsync(
        CancellationToken cancellationToken) =>
        await JsonHelpRequestPersistence.ReadAsync(filePath, cancellationToken);

    private async Task WriteDatabaseAsync(HelpRequestDatabase database) =>
        await JsonHelpRequestPersistence.WriteAtomicallyAsync(filePath, database);
}
