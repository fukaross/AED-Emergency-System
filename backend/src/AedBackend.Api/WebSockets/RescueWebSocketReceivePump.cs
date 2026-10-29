using System.Net.WebSockets;

namespace AedBackend.Api.WebSockets;

/** 保持连接读取活跃，并与服务端有限时关闭握手协作。 */
internal sealed class RescueWebSocketReceivePump
{
    private const int ReceiveBufferSize = 1024;
    private static readonly TimeSpan CloseReplyTimeout = TimeSpan.FromSeconds(2);
    private readonly ILogger<RescueWebSocketReceivePump> logger;

    public RescueWebSocketReceivePump(ILogger<RescueWebSocketReceivePump> logger)
    {
        this.logger = logger;
    }

    /** 客户端业务帧被明确忽略；客户端 Close 会被确认，服务端 Close 超时会 Abort 解除读取。 */
    public async Task RunAsync(
        RescueSocketRegistration registration,
        CancellationToken requestCancellation)
    {
        var buffer = new byte[ReceiveBufferSize];
        try
        {
            while (!requestCancellation.IsCancellationRequested)
            {
                var result = await registration.Connection.Socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    requestCancellation);
                if (result.MessageType != WebSocketMessageType.Close) continue;
                await CompletePeerCloseAsync(registration.Connection);
                return;
            }
        }
        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
        {
            logger.LogDebug(
                "Rescue WS request ended for {HelpId}/{ConnectionId}",
                registration.HelpId,
                registration.ConnectionId);
        }
        catch (OperationCanceledException exception)
        {
            logger.LogWarning(
                exception,
                "Rescue WS close reply timed out for {HelpId}/{ConnectionId}",
                registration.HelpId,
                registration.ConnectionId);
            registration.Connection.Socket.Abort();
        }
        catch (WebSocketException exception)
        {
            logger.LogDebug(
                exception,
                "Rescue WS receive ended for {HelpId}/{ConnectionId}",
                registration.HelpId,
                registration.ConnectionId);
        }
    }

    private static async Task CompletePeerCloseAsync(RescueSocketConnection connection)
    {
        connection.AcknowledgePeerClose();
        if (connection.Socket.State != WebSocketState.CloseReceived) return;
        using var timeout = new CancellationTokenSource(CloseReplyTimeout);
        await connection.ReplyToPeerCloseAsync(timeout.Token);
    }
}
