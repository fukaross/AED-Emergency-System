using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using AedBackend.Core.DTOs.Rescue;

namespace AedBackend.Api.WebSockets;

/**
 * 急救会话 WS 扇出注册表（ADR 0001）：仅维护连接与广播，不承载任何业务逻辑与权威数据。
 * 每条连接的发送按锁串行化（WebSocket 不允许并发 SendAsync），断开的连接按生命周期事件注销。
 */
public sealed class RescueSessionBroadcaster
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, ConnectionEntry>> connections =
        new(StringComparer.Ordinal);

    public void Register(string helpId, Guid connectionId, WebSocket socket)
    {
        connections.GetOrAdd(helpId, _ => new ConcurrentDictionary<Guid, ConnectionEntry>())[connectionId] =
            new ConnectionEntry(socket, new SemaphoreSlim(1, 1));
    }

    public void Unregister(string helpId, Guid connectionId)
    {
        if (!connections.TryGetValue(helpId, out var sockets)) return;
        if (sockets.TryRemove(connectionId, out var entry)) entry.SendLock.Dispose();
        if (sockets.IsEmpty) connections.TryRemove(helpId, out _);
    }

    public async Task BroadcastAsync(string helpId, string type, object data)
    {
        if (!connections.TryGetValue(helpId, out var sockets) || sockets.IsEmpty) return;

        var payload = JsonSerializer.SerializeToUtf8Bytes(new RescueSocketEnvelope(type, data), JsonOptions);
        foreach (var (connectionId, entry) in sockets)
        {
            if (entry.Socket.State != WebSocketState.Open)
            {
                Unregister(helpId, connectionId);
                continue;
            }

            try
            {
                using var timeout = new CancellationTokenSource(SendTimeout);
                await entry.SendLock.WaitAsync(timeout.Token);
                try
                {
                    await entry.Socket.SendAsync(payload, WebSocketMessageType.Text, true, timeout.Token);
                }
                finally
                {
                    entry.SendLock.Release();
                }
            }
            catch (Exception)
            {
                // 断连/超时/并发冲突一律按连接生命周期事件注销，不影响发起广播的 HTTP 请求
                Unregister(helpId, connectionId);
            }
        }
    }

    public async Task CloseAllAsync(string helpId, string reason)
    {
        if (!connections.TryGetValue(helpId, out var sockets)) return;

        foreach (var (connectionId, entry) in sockets)
        {
            try
            {
                if (entry.Socket.State == WebSocketState.Open)
                {
                    using var timeout = new CancellationTokenSource(SendTimeout);
                    await entry.SendLock.WaitAsync(timeout.Token);
                    try
                    {
                        await entry.Socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            reason,
                            timeout.Token);
                    }
                    finally
                    {
                        entry.SendLock.Release();
                    }
                }
            }
            catch (Exception)
            {
                // 连接已断开，按注销处理
            }

            Unregister(helpId, connectionId);
        }
    }

    private sealed record ConnectionEntry(WebSocket Socket, SemaphoreSlim SendLock);
}
