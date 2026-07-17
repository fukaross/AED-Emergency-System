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

    public Task<RescueSessionRecord> GetOrCreateAsync(
        string helpRequestId,
        DateTimeOffset now,
        CancellationToken ct) =>
        WithUpdateAsync(database => JsonRescueSessionOperations.GetOrCreate(database, helpRequestId, now), ct);

    public Task<RescueSessionRecord> JoinAsync(RescueSessionJoinCommand command, CancellationToken ct) =>
        WithUpdateAsync(database => JsonRescueSessionOperations.Join(database, command), ct);

    public Task<RescueSessionRecord> AppendMessageAsync(
        RescueSessionAppendCommand command,
        CancellationToken ct) =>
        WithUpdateAsync(database => JsonRescueSessionOperations.AppendMessage(database, command), ct);

    public Task<RescueSessionStatusAppendResult> AppendStatusEventAsync(
        RescueSessionStatusCommand command,
        CancellationToken ct) =>
        WithUpdateAsync(database => JsonRescueSessionOperations.AppendStatusEvent(database, command), ct);

    public Task<RescueSessionRecord> UpdateLocationAsync(
        RescueSessionLocationCommand command,
        CancellationToken ct) =>
        WithUpdateAsync(database => JsonRescueSessionOperations.UpdateLocation(database, command), ct);

    public Task<RescueSessionRecord> MarkReadAsync(
        string helpRequestId,
        string openId,
        long lastReadSeq,
        CancellationToken ct) =>
        WithUpdateAsync(
            database => JsonRescueSessionOperations.MarkRead(database, helpRequestId, openId, lastReadSeq),
            ct);

    public async Task CloseSessionAsync(string helpRequestId, CancellationToken ct)
    {
        await BackfillClosedSessionsAsync([helpRequestId], ct);
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

    public Task<IReadOnlyList<RescueSessionRecord>> ListByParticipantAsync(
        string openId,
        CancellationToken ct) =>
        WithReadAsync<IReadOnlyList<RescueSessionRecord>>(
            database => database.Sessions
                .Where(item => item.Participants.Any(participant => participant.OpenId == openId))
                .OrderByDescending(item => item.CreatedAt)
                .ToArray(),
            ct);

    public Task<IReadOnlyList<RescueSessionRecord>> ListAllAsync(CancellationToken ct) =>
        WithReadAsync<IReadOnlyList<RescueSessionRecord>>(
            database => database.Sessions.OrderByDescending(item => item.CreatedAt).ToArray(),
            ct);

    public Task<IReadOnlyList<RescueSessionOverviewRecord>> ListOverviewsAsync(
        string? viewerOpenId,
        CancellationToken ct) =>
        WithReadAsync<IReadOnlyList<RescueSessionOverviewRecord>>(
            database => database.Sessions
                .Select(session => RescueSessionOverviewProjection.FromSession(session, viewerOpenId))
                .OrderByDescending(item => item.CreatedAt)
                .ToArray(),
            ct);

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
