using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Rescue;

public sealed class JsonRescueSessionStore : IRescueSessionStore
{
    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonRescueSessionStore(IOptions<WeChatOptions> options)
    {
        filePath = Path.GetFullPath(options.Value.RescueSessionsFilePath);
    }

    public Task<RescueSessionRecord?> FindByHelpIdAsync(string helpRequestId, CancellationToken ct) =>
        WithReadAsync(database => JsonRescueSessionOperations.Find(database, helpRequestId), ct);

    public Task<RescueSessionParticipantAccess?> FindParticipantAccessAsync(
        RescueSessionParticipantQuery query,
        CancellationToken ct) =>
        WithReadAsync(
            database =>
            {
                var session = JsonRescueSessionOperations.Find(database, query.HelpRequestId);
                var participant = session?.Participants.FirstOrDefault(
                    item => item.OpenId == query.OpenId);
                return participant is null
                    ? null
                    : new RescueSessionParticipantAccess(session!.ClosedAt, participant);
            },
            ct);

    public Task<RescueSessionRecord> GetOrCreateAsync(
        string helpRequestId,
        DateTimeOffset now,
        CancellationToken ct) =>
        WithUpdateAsync(database => JsonRescueSessionOperations.GetOrCreate(database, helpRequestId, now), ct);

    public Task<RescueSessionJoinResult> JoinAsync(RescueSessionJoinCommand command, CancellationToken ct) =>
        WithUpdateAsync(database => JsonRescueSessionOperations.Join(database, command), ct);

    public Task<RescueSessionMessageRecord> AppendMessageAsync(
        RescueTextMessageCommand command,
        CancellationToken ct) =>
        WithUpdateAsync(database => JsonRescueSessionOperations.AppendMessage(database, command), ct);

    public Task<RescueSessionStatusAppendResult> AppendStatusEventAsync(
        RescueSessionStatusCommand command,
        CancellationToken ct) =>
        WithUpdateAsync(database => JsonRescueSessionOperations.AppendStatusEvent(database, command), ct);

    public Task<RescueSessionParticipantRecord> UpdateLocationAsync(
        RescueSessionLocationCommand command,
        CancellationToken ct) =>
        WithUpdateAsync(database => JsonRescueSessionOperations.UpdateLocation(database, command), ct);

    public Task<long> MarkReadAsync(RescueSessionReadCommand command, CancellationToken ct) =>
        WithUpdateAsync(
            database => JsonRescueSessionOperations.MarkRead(
                database,
                command),
            ct);

    public async Task CloseSessionAsync(
        string helpRequestId,
        DateTimeOffset closedAt,
        CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            var database = await JsonRescueSessionPersistence.ReadAsync(filePath, ct);
            if (JsonRescueSessionOperations.Close(database, helpRequestId, closedAt))
                await JsonRescueSessionPersistence.WriteAtomicallyAsync(filePath, database);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task BackfillClosedSessionsAsync(
        IReadOnlyCollection<string> helpRequestIds,
        CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            var database = await JsonRescueSessionPersistence.ReadAsync(filePath, ct);
            var closedAt = DateTimeOffset.UtcNow;
            var changed = false;
            foreach (var helpRequestId in helpRequestIds)
            {
                changed |= JsonRescueSessionOperations.Close(database, helpRequestId, closedAt);
            }
            if (changed) await JsonRescueSessionPersistence.WriteAtomicallyAsync(filePath, database);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public Task<IReadOnlyList<RescueSessionOverviewRecord>> ListOverviewsAsync(
        RescueSessionOverviewQuery query,
        CancellationToken ct) =>
        WithReadAsync<IReadOnlyList<RescueSessionOverviewRecord>>(
            database => FilterSessions(database, query)
                .Select(session => RescueSessionOverviewProjection.FromSession(
                    session,
                    query.ViewerOpenId))
                .OrderByDescending(item => item.CreatedAt)
                .ToArray(),
            ct);

    private static IEnumerable<RescueSessionRecord> FilterSessions(
        JsonRescueSessionDatabase database,
        RescueSessionOverviewQuery query)
    {
        if (query.HelpRequestIds.Count == 0) return [];
        var helpIds = query.HelpRequestIds.ToHashSet(StringComparer.Ordinal);
        return database.Sessions.Where(session => helpIds.Contains(session.HelpRequestId));
    }

    private async Task<T> WithReadAsync<T>(Func<JsonRescueSessionDatabase, T> read, CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            return read(await JsonRescueSessionPersistence.ReadAsync(filePath, ct));
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<T> WithUpdateAsync<T>(Func<JsonRescueSessionDatabase, T> update, CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            var database = await JsonRescueSessionPersistence.ReadAsync(filePath, ct);
            var result = update(database);
            await JsonRescueSessionPersistence.WriteAtomicallyAsync(filePath, database);
            return result;
        }
        finally
        {
            fileLock.Release();
        }
    }
}
