using System.Net.WebSockets;

namespace AedBackend.Api.WebSockets;

internal sealed record RescueSocketIdentity(string OpenId, string TokenHash);

internal interface IRescueConnectionAuthorizer
{
    Task<bool> IsAuthorizedAsync(RescueSocketAuthorization authorization);
}

internal sealed record RescueSocketAuthorization
{
    public required string HelpId { get; init; }
    public required RescueSocketIdentity Identity { get; init; }
    public required bool AllowClosedSession { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}

internal sealed record RescueBroadcastStamp(
    long? MessageSequence = null,
    string? ParticipantOpenId = null,
    DateTimeOffset? ParticipantVersion = null);

internal enum RescueSocketSendResult
{
    Sent,
    Suppressed,
    Closed,
}

/** 单条 WS 连接的发送串行化、陈旧投影抑制与关闭握手状态。 */
internal sealed class RescueSocketConnection
{
    private readonly TaskCompletionSource peerCloseReceived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<string, DateTimeOffset> participantVersions =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private long lastMessageSequence;
    private int serverCloseStarted;

    public RescueSocketConnection(WebSocket socket, RescueSocketIdentity identity)
    {
        Socket = socket;
        Identity = identity;
    }

    public RescueSocketIdentity Identity { get; }
    public WebSocket Socket { get; }

    /** 发送锁内再次检查终态门并抑制乱序到达的旧消息或参与者投影。 */
    public async Task<RescueSocketSendResult> SendAsync(
        RescueSocketSendCommand command)
    {
        await sendLock.WaitAsync(command.CancellationToken);
        try
        {
            if (Socket.State != WebSocketState.Open) return RescueSocketSendResult.Closed;
            if (!command.CanSend() || !IsFresh(command.Stamp)) return RescueSocketSendResult.Suppressed;
            await Socket.SendAsync(
                command.Payload,
                WebSocketMessageType.Text,
                true,
                command.CancellationToken);
            MarkSent(command.Stamp);
            return RescueSocketSendResult.Sent;
        }
        finally
        {
            sendLock.Release();
        }
    }

    /** 服务端发起有限时关闭握手；客户端不确认时 Abort 以释放 receive pump。 */
    public async Task RequestCloseAsync(
        WebSocketCloseStatus status,
        string reason,
        TimeSpan gracePeriod)
    {
        if (Interlocked.Exchange(ref serverCloseStarted, 1) != 0) return;
        using var timeout = new CancellationTokenSource(gracePeriod);
        try
        {
            await SendCloseOutputAsync(status, reason, timeout.Token);
            if (Socket.State is WebSocketState.Closed or WebSocketState.Aborted) return;
            await peerCloseReceived.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            Socket.Abort();
        }
    }

    /** receive pump 收到客户端 Close 时完成服务端关闭等待。 */
    public void AcknowledgePeerClose()
    {
        peerCloseReceived.TrySetResult();
    }

    /** 客户端主动关闭时用同一发送锁回送 Close。 */
    public Task ReplyToPeerCloseAsync(CancellationToken cancellationToken)
    {
        return SendCloseOutputAsync(
            Socket.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
            Socket.CloseStatusDescription,
            cancellationToken);
    }

    private async Task SendCloseOutputAsync(
        WebSocketCloseStatus status,
        string? reason,
        CancellationToken cancellationToken)
    {
        await sendLock.WaitAsync(cancellationToken);
        try
        {
            if (Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await Socket.CloseOutputAsync(status, reason, cancellationToken);
            }
        }
        finally
        {
            sendLock.Release();
        }
    }

    private bool IsFresh(RescueBroadcastStamp? stamp)
    {
        if (stamp?.MessageSequence is long sequence && sequence <= lastMessageSequence) return false;
        if (stamp?.ParticipantOpenId is not { } openId
            || stamp.ParticipantVersion is not { } version)
        {
            return true;
        }
        return !participantVersions.TryGetValue(openId, out var current) || version > current;
    }

    private void MarkSent(RescueBroadcastStamp? stamp)
    {
        if (stamp?.MessageSequence is long sequence) lastMessageSequence = sequence;
        if (stamp?.ParticipantOpenId is { } openId
            && stamp.ParticipantVersion is { } version)
        {
            participantVersions[openId] = version;
        }
    }
}

internal sealed record RescueSocketSendCommand
{
    public required ReadOnlyMemory<byte> Payload { get; init; }
    public required RescueBroadcastStamp? Stamp { get; init; }
    public required Func<bool> CanSend { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}

internal sealed record RescueSocketRegistration(
    string HelpId,
    Guid ConnectionId,
    RescueSocketConnection Connection);
