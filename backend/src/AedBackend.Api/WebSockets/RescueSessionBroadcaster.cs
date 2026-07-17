using System.Collections.Concurrent;
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

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, ConnectionEntry>> connections =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> closedHelpIds = new(StringComparer.Ordinal);
    private readonly ILogger<RescueSessionBroadcaster> logger;
    private readonly TimeSpan sendTimeout;

    public RescueSessionBroadcaster(ILogger<RescueSessionBroadcaster> logger)
        : this(logger, DefaultSendTimeout)
    {
    }

    public RescueSessionBroadcaster(
        ILogger<RescueSessionBroadcaster> logger,
        TimeSpan sendTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sendTimeout, TimeSpan.Zero);
        this.logger = logger;
        this.sendTimeout = sendTimeout;
    }

    public bool Register(string helpId, Guid connectionId, WebSocket socket)
    {
        if (closedHelpIds.ContainsKey(helpId))
        {
            socket.Abort();
            return false;
        }
        connections.GetOrAdd(helpId, _ => new ConcurrentDictionary<Guid, ConnectionEntry>())[connectionId] =
            new ConnectionEntry(socket, new SemaphoreSlim(1, 1));
        if (!closedHelpIds.ContainsKey(helpId)) return true;

        Unregister(helpId, connectionId);
        socket.Abort();
        return false;
    }

    public void Unregister(string helpId, Guid connectionId)
    {
        Remove(new ConnectionTarget(helpId, connectionId, null));
    }

    public async Task BroadcastAsync(string helpId, string type, object data)
    {
        if (!connections.TryGetValue(helpId, out var sockets) || sockets.IsEmpty) return;

        var payload = JsonSerializer.SerializeToUtf8Bytes(new RescueSocketEnvelope(type, data), JsonOptions);
        var sends = sockets.Select(item => SendAsync(
            new ConnectionTarget(helpId, item.Key, item.Value),
            payload));
        await Task.WhenAll(sends);
    }

    public async Task CloseAllAsync(string helpId, string reason)
    {
        closedHelpIds.TryAdd(helpId, 0);
        if (!connections.TryGetValue(helpId, out var sockets) || sockets.IsEmpty) return;

        var closes = sockets.Select(item => CloseAsync(
            new ConnectionTarget(helpId, item.Key, item.Value),
            reason));
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
        if (!connections.TryGetValue(target.HelpId, out var sockets)) return;
        sockets.TryRemove(target.Id, out _);
    }

    private sealed record ConnectionEntry(WebSocket Socket, SemaphoreSlim SendLock);

    private sealed record ConnectionTarget(string HelpId, Guid Id, ConnectionEntry? Entry);
}
