using System.Text.Json;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Help;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Help;

// [Transitional: JSON -> MySQL] Keep schema close to BackEnd.md tables.
public sealed class JsonHelpRequestStore : IHelpRequestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonHelpRequestStore(IOptions<WeChatOptions> options)
    {
        filePath = Path.GetFullPath(options.Value.HelpRequestsFilePath);
    }

    public async Task SaveAsync(
        HelpRequestRecord record,
        CancellationToken cancellationToken)
    {
        await WithWriteAsync(database => database.Requests.Insert(0, record), cancellationToken);
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

    public async Task<bool> HasActiveRequestAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await WithReadAsync(
            database => database.Requests.Any(item =>
                item.UserId == userId && HelpStatuses.Active.Contains(item.Status)),
            cancellationToken);
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
        string id,
        string status,
        string? note,
        DateTimeOffset closedAt,
        CancellationToken cancellationToken)
    {
        return await WithUpdateAsync(
            database => ReplaceRequest(database, id, item =>
                CloseRequest(item, status, note, closedAt)),
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
        if (HelpStatuses.IsClosed(item.Status))
        {
            throw new InvalidOperationException("求助已结束");
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

    private async Task WithWriteAsync(
        Action<HelpRequestDatabase> write,
        CancellationToken cancellationToken)
    {
        await WithUpdateAsync(
            database =>
            {
                write(database);
                return true;
            },
            cancellationToken);
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
            await WriteDatabaseAsync(database, cancellationToken);
            return result;
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<HelpRequestDatabase> ReadDatabaseAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new HelpRequestDatabase();
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<HelpRequestDatabase>(
            stream,
            JsonOptions,
            cancellationToken) ?? new HelpRequestDatabase();
    }

    private async Task WriteDatabaseAsync(
        HelpRequestDatabase database,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, database, JsonOptions, cancellationToken);
    }

    private sealed class HelpRequestDatabase
    {
        public List<HelpRequestRecord> Requests { get; init; } = [];
    }
}
