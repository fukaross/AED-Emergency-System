using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using AedBackend.Core.DTOs.Rescue;

namespace AedBackend.Api.WebSockets;

/**
 * 急救会话 WS 扇出注册表：每次敏感扇出重校验连接身份，同连接严格串行且终态信封永远最后。
 */
internal sealed class RescueSessionBroadcaster
{
    private static readonly RescueBroadcasterTiming DefaultTiming = new()
    {
        SendTimeout = TimeSpan.FromSeconds(5),
        CloseGracePeriod = TimeSpan.FromSeconds(2),
        ClosedHelpRetention = TimeSpan.FromMinutes(5),
        ClosedHelpPruneInterval = TimeSpan.FromMinutes(1)
    };

    private readonly IRescueConnectionAuthorizer authorizer;
    private readonly ConcurrentDictionary<string, long> closedHelpIds = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConnectionBucket> connections = new(StringComparer.Ordinal);
    private readonly ILogger<RescueSessionBroadcaster> logger;
    private readonly RescueBroadcasterTiming timing;
    private long nextPruneAtTimestamp;

    public RescueSessionBroadcaster(
        IRescueConnectionAuthorizer authorizer,
        ILogger<RescueSessionBroadcaster> logger)
        : this(authorizer, logger, DefaultTiming)
    {
    }

    internal RescueSessionBroadcaster(
        IRescueConnectionAuthorizer authorizer,
        ILogger<RescueSessionBroadcaster> logger,
        RescueBroadcasterTiming timing)
    {
        Validate(timing);
        this.authorizer = authorizer;
        this.logger = logger;
        this.timing = timing;
        nextPruneAtTimestamp = Stopwatch.GetTimestamp();
    }

    internal int ActiveHelpBucketCount => connections.Count;
    internal int ClosedHelpCount => closedHelpIds.Count;

    /** 连接注册与 closed gate 二次检查原子衔接，关单竞态只会拒绝而不会漏接。 */
    public RescueSocketRegistration? Register(
        string helpId,
        RescueSocketIdentity identity,
        WebSocket socket)
    {
        MaybePruneClosedHelpIds();
        if (IsHelpClosed(helpId)) return Reject(socket);

        var registration = new RescueSocketRegistration(
            helpId,
            Guid.NewGuid(),
            new RescueSocketConnection(socket, identity));
        while (true)
        {
            var bucket = connections.GetOrAdd(helpId, _ => new ConnectionBucket());
            if (!bucket.TryAdd(registration))
            {
                TryRemoveBucket(helpId, bucket);
                continue;
            }
            if (!IsHelpClosed(helpId)) return registration;

            bucket.TryRemove(registration.ConnectionId);
            TryRemoveBucket(helpId, bucket);
            return Reject(socket);
        }
    }

    /** endpoint finally 是唯一注销所有者，确保服务端关闭仍能由 receive pump 完成握手。 */
    public void Unregister(RescueSocketRegistration registration)
    {
        if (!connections.TryGetValue(registration.HelpId, out var bucket)) return;
        if (!bucket.TryRemove(registration.ConnectionId)) return;
        TryRemoveBucket(registration.HelpId, bucket);
    }

    public Task BroadcastAsync(
        string helpId,
        string type,
        object data)
    {
        return BroadcastAsync(new RescueBroadcastCommand
        {
            HelpId = helpId,
            Type = type,
            Data = data
        });
    }

    public async Task BroadcastAsync(RescueBroadcastCommand command)
    {
        MaybePruneClosedHelpIds();
        if (IsHelpClosed(command.HelpId)) return;
        var targets = Snapshot(command.HelpId);
        if (targets.Length == 0) return;

        var payload = RescueBroadcastPayload.Serialize(command.Type, command.Data);
        var stamp = RescueBroadcastPayload.CreateStamp(
            command.Data,
            command.ParticipantVersion);
        await Task.WhenAll(targets.Select(target => SendAuthorizedAsync(new RescueAuthorizedSendCommand
        {
            Target = target,
            Payload = payload,
            Stamp = stamp,
            AllowClosed = false
        })));
    }

    /** 设置终态门后发送唯一终态信封，再启动关闭握手；排队中的普通信封会在发送锁内被抑制。 */
    public async Task BroadcastTerminalAndCloseAsync(
        string helpId,
        RescueHelpStatusDto status,
        string reason)
    {
        MarkClosed(helpId);
        var targets = Snapshot(helpId);
        if (targets.Length == 0) return;

        var payload = RescueBroadcastPayload.Serialize("help_status", status);
        await Task.WhenAll(targets.Select(target => SendAuthorizedAsync(new RescueAuthorizedSendCommand
        {
            Target = target,
            Payload = payload,
            AllowClosed = true
        })));
        await CloseTargetsAsync(targets, WebSocketCloseStatus.NormalClosure, reason);
    }

    /** 无终态载荷的关闭入口，仅供明确不需要状态信封的生命周期收口。 */
    public async Task CloseAllAsync(string helpId, string reason)
    {
        MarkClosed(helpId);
        var targets = Snapshot(helpId);
        await CloseTargetsAsync(targets, WebSocketCloseStatus.NormalClosure, reason);
    }

    private async Task SendAuthorizedAsync(RescueAuthorizedSendCommand command)
    {
        using var timeout = new CancellationTokenSource(timing.SendTimeout);
        try
        {
            var target = command.Target;
            var authorized = await authorizer.IsAuthorizedAsync(new RescueSocketAuthorization
            {
                HelpId = target.HelpId,
                Identity = target.Connection.Identity,
                AllowClosedSession = command.AllowClosed,
                CancellationToken = timeout.Token
            });
            if (!authorized)
            {
                await target.Connection.RequestCloseAsync(
                    WebSocketCloseStatus.PolicyViolation,
                    "authorization revoked",
                    timing.CloseGracePeriod);
                return;
            }

            await target.Connection.SendAsync(new RescueSocketSendCommand
            {
                Payload = command.Payload,
                Stamp = command.Stamp,
                CanSend = () => command.AllowClosed || !IsHelpClosed(target.HelpId),
                CancellationToken = timeout.Token
            });
        }
        catch (Exception exception)
        {
            var target = command.Target;
            logger.LogWarning(
                exception,
                "Rescue WS fanout failed for {HelpId}/{ConnectionId}",
                target.HelpId,
                target.ConnectionId);
            Abort(target);
        }
    }

    private async Task CloseTargetsAsync(
        RescueSocketRegistration[] targets,
        WebSocketCloseStatus status,
        string reason)
    {
        var closes = targets.Select(target => CloseAsync(target, status, reason));
        await Task.WhenAll(closes);
    }

    private async Task CloseAsync(
        RescueSocketRegistration target,
        WebSocketCloseStatus status,
        string reason)
    {
        try
        {
            await target.Connection.RequestCloseAsync(status, reason, timing.CloseGracePeriod);
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "Rescue WS close handshake failed for {HelpId}/{ConnectionId}",
                target.HelpId,
                target.ConnectionId);
            Abort(target);
        }
    }

    private RescueSocketRegistration[] Snapshot(string helpId)
    {
        return connections.TryGetValue(helpId, out var bucket)
            ? bucket.Snapshot()
            : [];
    }

    private void MarkClosed(string helpId)
    {
        MaybePruneClosedHelpIds();
        closedHelpIds[helpId] = Stopwatch.GetTimestamp();
    }

    private bool IsHelpClosed(string helpId)
    {
        if (!closedHelpIds.TryGetValue(helpId, out var closedAt)) return false;
        if (Stopwatch.GetElapsedTime(closedAt) < timing.ClosedHelpRetention) return true;
        closedHelpIds.TryRemove(helpId, out _);
        return false;
    }

    private void MaybePruneClosedHelpIds()
    {
        var now = Stopwatch.GetTimestamp();
        var due = Volatile.Read(ref nextPruneAtTimestamp);
        if (now < due) return;
        var next = now + ToTimestampDelta(timing.ClosedHelpPruneInterval);
        if (Interlocked.CompareExchange(ref nextPruneAtTimestamp, next, due) != due) return;

        foreach (var item in closedHelpIds)
        {
            if (Stopwatch.GetElapsedTime(item.Value, now) >= timing.ClosedHelpRetention)
                closedHelpIds.TryRemove(item.Key, out _);
        }
    }

    private void TryRemoveBucket(string helpId, ConnectionBucket bucket)
    {
        if (!bucket.IsRetired) return;
        var pairs = (ICollection<KeyValuePair<string, ConnectionBucket>>)connections;
        pairs.Remove(new KeyValuePair<string, ConnectionBucket>(helpId, bucket));
    }

    private void Abort(RescueSocketRegistration target)
    {
        try
        {
            target.Connection.Socket.Abort();
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "Rescue WS abort failed for {HelpId}/{ConnectionId}",
                target.HelpId,
                target.ConnectionId);
        }
    }

    private static RescueSocketRegistration? Reject(WebSocket socket)
    {
        socket.Abort();
        return null;
    }

    private static long ToTimestampDelta(TimeSpan interval) =>
        (long)(interval.TotalSeconds * Stopwatch.Frequency);

    private static void Validate(RescueBroadcasterTiming timing)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timing.SendTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timing.CloseGracePeriod, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timing.ClosedHelpRetention, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timing.ClosedHelpPruneInterval, TimeSpan.Zero);
    }

}
