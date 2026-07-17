using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using AedBackend.Core.DTOs.Rescue;

namespace AedBackend.Api.WebSockets;

/**
 * 急救会话 WS 扇出注册表：不同连接并行发送，同一连接通过发送锁保持严格串行。
 * 超时或异常连接会被 Abort 并移出注册表，不能拖慢其余参与者或 HTTP 写请求。
 */
public sealed class RescueSessionBroadcaster
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan DefaultSendTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultClosedHelpRetention = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultClosedHelpPruneInterval = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, ConnectionBucket> connections =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> closedHelpIds = new(StringComparer.Ordinal);
    private readonly ILogger<RescueSessionBroadcaster> logger;
    private readonly TimeSpan closedHelpPruneInterval;
    private readonly TimeSpan closedHelpRetention;
    private readonly TimeSpan sendTimeout;
    private long nextPruneAtTimestamp;

    internal int ActiveHelpBucketCount => connections.Count;
    internal int ClosedHelpCount => closedHelpIds.Count;

    public RescueSessionBroadcaster(ILogger<RescueSessionBroadcaster> logger)
        : this(logger, DefaultSendTimeout)
    {
    }

    public RescueSessionBroadcaster(
        ILogger<RescueSessionBroadcaster> logger,
        TimeSpan sendTimeout)
        : this(logger, sendTimeout, DefaultClosedHelpRetention, DefaultClosedHelpPruneInterval)
    {
    }

    public RescueSessionBroadcaster(
        ILogger<RescueSessionBroadcaster> logger,
        TimeSpan sendTimeout,
        TimeSpan closedHelpRetention,
        TimeSpan closedHelpPruneInterval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sendTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(closedHelpRetention, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(closedHelpPruneInterval, TimeSpan.Zero);
        this.logger = logger;
        this.sendTimeout = sendTimeout;
        this.closedHelpRetention = closedHelpRetention;
        this.closedHelpPruneInterval = closedHelpPruneInterval;
        nextPruneAtTimestamp = Stopwatch.GetTimestamp();
    }

    public bool Register(string helpId, Guid connectionId, WebSocket socket)
    {
        MaybePruneClosedHelpIds();
        if (IsHelpClosed(helpId))
        {
            socket.Abort();
            return false;
        }

        var entry = new ConnectionEntry(socket, new SemaphoreSlim(1, 1));
        while (true)
        {
            var bucket = connections.GetOrAdd(helpId, _ => new ConnectionBucket());
            if (!bucket.TryAdd(connectionId, entry))
            {
                TryRemoveBucket(helpId, bucket);
                continue;
            }

            if (!IsHelpClosed(helpId)) return true;

            if (bucket.TryRemove(connectionId))
            {
                TryRemoveBucket(helpId, bucket);
            }
            socket.Abort();
            return false;
        }
    }

    public void Unregister(string helpId, Guid connectionId)
    {
        Remove(new ConnectionTarget(helpId, connectionId, null));
    }

    public async Task BroadcastAsync(string helpId, string type, object data)
    {
        MaybePruneClosedHelpIds();
        if (IsHelpClosed(helpId)) return;
        if (!connections.TryGetValue(helpId, out var bucket)) return;
        var targets = bucket.Snapshot(helpId);
        if (targets.Length == 0)
        {
            TryRemoveBucket(helpId, bucket);
            return;
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(new RescueSocketEnvelope(type, data), JsonOptions);
        var sends = targets.Select(target => SendAsync(target, payload));
        await Task.WhenAll(sends);
    }

    public async Task CloseAllAsync(string helpId, string reason)
    {
        MaybePruneClosedHelpIds();
        closedHelpIds[helpId] = Stopwatch.GetTimestamp();
        if (!connections.TryGetValue(helpId, out var bucket)) return;
        var targets = bucket.Snapshot(helpId);
        if (targets.Length == 0)
        {
            TryRemoveBucket(helpId, bucket);
            return;
        }

        var closes = targets.Select(target => CloseAsync(target, reason));
        await Task.WhenAll(closes);
    }

    private async Task SendAsync(ConnectionTarget target, byte[] payload)
    {
        var entry = target.Entry!;
        if (entry.Socket.State != WebSocketState.Open)
        {
            Remove(target);
            return;
        }

        using var timeout = new CancellationTokenSource(sendTimeout);
        var lockTaken = false;
        try
        {
            await entry.SendLock.WaitAsync(timeout.Token);
            lockTaken = true;
            if (entry.Socket.State != WebSocketState.Open) return;
            await entry.Socket.SendAsync(
                new ArraySegment<byte>(payload),
                WebSocketMessageType.Text,
                true,
                timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Rescue WS send failed for {HelpId}/{ConnectionId}", target.HelpId, target.Id);
            AbortAndRemove(target);
        }
        finally
        {
            if (lockTaken) entry.SendLock.Release();
        }
    }

    private async Task CloseAsync(ConnectionTarget target, string reason)
    {
        var entry = target.Entry!;
        using var timeout = new CancellationTokenSource(sendTimeout);
        var lockTaken = false;
        try
        {
            await entry.SendLock.WaitAsync(timeout.Token);
            lockTaken = true;
            if (entry.Socket.State == WebSocketState.Open)
            {
                await entry.Socket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    reason,
                    timeout.Token);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Rescue WS close failed for {HelpId}/{ConnectionId}", target.HelpId, target.Id);
            AbortAndRemove(target);
        }
        finally
        {
            if (lockTaken) entry.SendLock.Release();
            Remove(target);
        }
    }

    private void AbortAndRemove(ConnectionTarget target)
    {
        try
        {
            target.Entry?.Socket.Abort();
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Rescue WS abort failed for {HelpId}/{ConnectionId}", target.HelpId, target.Id);
        }
        Remove(target);
    }

    private void Remove(ConnectionTarget target)
    {
        if (!connections.TryGetValue(target.HelpId, out var bucket)) return;
        if (!bucket.TryRemove(target.Id)) return;
        TryRemoveBucket(target.HelpId, bucket);
    }

    private bool IsHelpClosed(string helpId)
    {
        if (!closedHelpIds.TryGetValue(helpId, out var closedAtTimestamp)) return false;
        if (Stopwatch.GetElapsedTime(closedAtTimestamp) < closedHelpRetention) return true;

        closedHelpIds.TryRemove(helpId, out _);
        return false;
    }

    private void MaybePruneClosedHelpIds()
    {
        var now = Stopwatch.GetTimestamp();
        var due = Volatile.Read(ref nextPruneAtTimestamp);
        if (now < due) return;

        var next = now + ToTimestampDelta(closedHelpPruneInterval);
        if (Interlocked.CompareExchange(ref nextPruneAtTimestamp, next, due) != due) return;

        foreach (var item in closedHelpIds)
        {
            if (Stopwatch.GetElapsedTime(item.Value, now) >= closedHelpRetention)
            {
                closedHelpIds.TryRemove(item.Key, out _);
            }
        }
    }

    private void TryRemoveBucket(string helpId, ConnectionBucket bucket)
    {
        if (!bucket.IsRetired) return;
        var pairs = (ICollection<KeyValuePair<string, ConnectionBucket>>)connections;
        pairs.Remove(new KeyValuePair<string, ConnectionBucket>(helpId, bucket));
    }

    private static long ToTimestampDelta(TimeSpan interval) =>
        (long)(interval.TotalSeconds * Stopwatch.Frequency);

    private sealed record ConnectionEntry(WebSocket Socket, SemaphoreSlim SendLock);

    private sealed record ConnectionTarget(string HelpId, Guid Id, ConnectionEntry? Entry);

    private sealed class ConnectionBucket
    {
        private readonly Dictionary<Guid, ConnectionEntry> entries = [];
        private readonly object gate = new();
        private bool retired;

        public bool IsRetired
        {
            get
            {
                lock (gate)
                {
                    return retired;
                }
            }
        }

        public bool TryAdd(Guid connectionId, ConnectionEntry entry)
        {
            lock (gate)
            {
                if (retired) return false;
                entries[connectionId] = entry;
                return true;
            }
        }

        public ConnectionTarget[] Snapshot(string helpId)
        {
            lock (gate)
            {
                return entries.Select(item => new ConnectionTarget(helpId, item.Key, item.Value)).ToArray();
            }
        }

        public bool TryRemove(Guid connectionId)
        {
            lock (gate)
            {
                if (!entries.Remove(connectionId) || entries.Count != 0) return false;
                retired = true;
                return true;
            }
        }
    }
}
