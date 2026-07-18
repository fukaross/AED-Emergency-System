using System.Net.WebSockets;
using AedBackend.Api.WebSockets;
using AedBackend.Core.DTOs.Rescue;
using Microsoft.Extensions.Logging.Abstractions;

namespace AedBackend.Tests.Rescue;

public sealed class RescueSessionBroadcasterTests
{
    private static readonly TimeSpan TestSendTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan TestCloseGracePeriod = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan TestClosedHelpRetention = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan TestPruneInterval = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan ParallelEntryTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Broadcast_SerializesConcurrentSendsPerConnection()
    {
        var broadcaster = CreateBroadcaster();
        var socket = new TrackingWebSocket(ct => Task.Delay(30, ct));
        broadcaster.Register("help-1", Identity(), socket);

        await Task.WhenAll(
            broadcaster.BroadcastAsync("help-1", "message", new { seq = 1 }),
            broadcaster.BroadcastAsync("help-1", "message", new { seq = 2 }));

        Assert.Equal(2, socket.SendCount);
        Assert.Equal(1, socket.MaxConcurrentSends);
        Assert.Equal(0, socket.AbortCount);
    }

    [Fact]
    public async Task Broadcast_SendsConnectionsInParallelAndAbortsTimedOutSockets()
    {
        var entered = 0;
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task BlockUntilTimeout(CancellationToken ct)
        {
            if (Interlocked.Increment(ref entered) == 2) bothEntered.TrySetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }

        var broadcaster = CreateBroadcaster();
        var slowA = new TrackingWebSocket(BlockUntilTimeout);
        var slowB = new TrackingWebSocket(BlockUntilTimeout);
        var fast = new TrackingWebSocket(_ => Task.CompletedTask);
        broadcaster.Register("help-1", Identity("a"), slowA);
        broadcaster.Register("help-1", Identity("b"), slowB);
        broadcaster.Register("help-1", Identity("c"), fast);

        var broadcast = broadcaster.BroadcastAsync("help-1", "participant", new { id = "p-1" });
        await bothEntered.Task.WaitAsync(ParallelEntryTimeout);
        await broadcast;

        Assert.Equal(1, slowA.AbortCount);
        Assert.Equal(1, slowB.AbortCount);
        Assert.Equal(1, fast.SendCount);

        await broadcaster.BroadcastAsync("help-1", "participant", new { id = "p-2" });
        Assert.Equal(1, slowA.SendCount);
        Assert.Equal(1, slowB.SendCount);
        Assert.Equal(2, fast.SendCount);
    }

    [Fact]
    public async Task Register_AfterCloseAll_IsRejectedAndAborted()
    {
        var broadcaster = CreateBroadcaster();
        await broadcaster.CloseAllAsync("help-1", "help closed");
        var socket = new TrackingWebSocket(_ => Task.CompletedTask);

        var registered = broadcaster.Register("help-1", Identity(), socket);

        Assert.Null(registered);
        Assert.Equal(1, socket.AbortCount);
    }

    [Fact]
    public async Task Broadcast_AfterCloseAll_DoesNotSendLateEnvelope()
    {
        var broadcaster = CreateBroadcaster();
        var socket = new TrackingWebSocket(_ => Task.CompletedTask);
        broadcaster.Register("help-1", Identity(), socket);
        await broadcaster.CloseAllAsync("help-1", "help closed");

        await broadcaster.BroadcastAsync("help-1", "message", new { seq = 1 });

        Assert.Equal(0, socket.SendCount);
    }

    [Fact]
    public void Unregister_LastConnection_RemovesEmptyHelpBucket()
    {
        var broadcaster = CreateBroadcaster();
        var registration = broadcaster.Register(
            "help-1",
            Identity(),
            new TrackingWebSocket(_ => Task.CompletedTask));

        broadcaster.Unregister(registration!);

        Assert.Equal(0, broadcaster.ActiveHelpBucketCount);
    }

    [Fact]
    public async Task ClosedHelpIds_ExpireAfterRetentionAndArePruned()
    {
        var broadcaster = CreateBroadcaster();
        await broadcaster.CloseAllAsync("help-1", "help closed");
        Assert.Equal(1, broadcaster.ClosedHelpCount);

        await Task.Delay(TestClosedHelpRetention + TestPruneInterval + TestPruneInterval);
        var socket = new TrackingWebSocket(_ => Task.CompletedTask);
        var registered = broadcaster.Register("help-1", Identity(), socket);

        Assert.NotNull(registered);
        Assert.Equal(0, broadcaster.ClosedHelpCount);
        Assert.Equal(1, broadcaster.ActiveHelpBucketCount);
    }

    [Fact]
    public async Task Broadcast_WhenAuthorizationWasRevoked_ClosesWithoutSendingPayload()
    {
        var authorizer = new MutableConnectionAuthorizer { IsAuthorized = false };
        var broadcaster = CreateBroadcaster(authorizer);
        var socket = new TrackingWebSocket(_ => Task.CompletedTask);
        broadcaster.Register("help-1", Identity(), socket);

        await broadcaster.BroadcastAsync("help-1", "message", new { seq = 1 });

        Assert.Equal(0, socket.SendCount);
        Assert.Equal(1, socket.CloseOutputCount);
        Assert.Equal(1, socket.AbortCount);
    }

    [Fact]
    public async Task TerminalBroadcast_IsLastAndAbortsClientThatDoesNotAcknowledgeClose()
    {
        var broadcaster = CreateBroadcaster();
        var socket = new TrackingWebSocket(_ => Task.CompletedTask);
        broadcaster.Register("help-1", Identity(), socket);

        await broadcaster.BroadcastTerminalAndCloseAsync(
            "help-1",
            new RescueHelpStatusDto("closed"),
            "help closed");
        await broadcaster.BroadcastAsync("help-1", "message", new { seq = 2 });

        Assert.Single(socket.SentPayloads);
        Assert.Contains("help_status", socket.SentPayloads[0]);
        Assert.Equal(1, socket.CloseOutputCount);
        Assert.Equal(1, socket.AbortCount);
    }

    [Fact]
    public async Task Broadcast_DropsMessageThatArrivesAfterANewerSequence()
    {
        var broadcaster = CreateBroadcaster();
        var socket = new TrackingWebSocket(_ => Task.CompletedTask);
        broadcaster.Register("help-1", Identity(), socket);
        var newer = new RescueMessageDto(
            "message-2", 2, "text", "openid-user", "急救员", "new", null, null, DateTimeOffset.UtcNow);
        var older = newer with { Id = "message-1", Seq = 1, Text = "old" };

        await broadcaster.BroadcastAsync("help-1", "message", newer);
        await broadcaster.BroadcastAsync("help-1", "message", older);

        Assert.Single(socket.SentPayloads);
        Assert.Contains("\"seq\":2", socket.SentPayloads[0]);
    }

    private static RescueSessionBroadcaster CreateBroadcaster(
        IRescueConnectionAuthorizer? authorizer = null)
    {
        return new RescueSessionBroadcaster(
            authorizer ?? new MutableConnectionAuthorizer(),
            NullLogger<RescueSessionBroadcaster>.Instance,
            new RescueBroadcasterTiming
            {
                SendTimeout = TestSendTimeout,
                CloseGracePeriod = TestCloseGracePeriod,
                ClosedHelpRetention = TestClosedHelpRetention,
                ClosedHelpPruneInterval = TestPruneInterval
            });
    }

    private static RescueSocketIdentity Identity(string suffix = "user") =>
        new($"openid-{suffix}", $"token-hash-{suffix}");

    private sealed class MutableConnectionAuthorizer : IRescueConnectionAuthorizer
    {
        public bool IsAuthorized { get; set; } = true;

        public Task<bool> IsAuthorizedAsync(RescueSocketAuthorization authorization) =>
            Task.FromResult(IsAuthorized);
    }

    private sealed class TrackingWebSocket(Func<CancellationToken, Task> send) : WebSocket
    {
        private int activeSends;
        private int abortCount;
        private int maxConcurrentSends;
        private int sendCount;
        private int closeOutputCount;
        private WebSocketState state = WebSocketState.Open;
        private readonly List<string> sentPayloads = [];

        public int AbortCount => abortCount;
        public int CloseOutputCount => closeOutputCount;
        public int MaxConcurrentSends => maxConcurrentSends;
        public int SendCount => sendCount;
        public IReadOnlyList<string> SentPayloads => sentPayloads;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => state;
        public override string? SubProtocol => null;

        public override void Abort()
        {
            Interlocked.Increment(ref abortCount);
            state = WebSocketState.Aborted;
        }

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref closeOutputCount);
            state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            state = WebSocketState.Closed;
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        }

        public override async Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref sendCount);
            lock (sentPayloads)
            {
                sentPayloads.Add(System.Text.Encoding.UTF8.GetString(buffer.AsSpan()));
            }
            var active = Interlocked.Increment(ref activeSends);
            SetMaxConcurrent(active);
            try
            {
                await send(cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref activeSends);
            }
        }

        private void SetMaxConcurrent(int active)
        {
            int observed;
            do
            {
                observed = maxConcurrentSends;
                if (observed >= active) return;
            }
            while (Interlocked.CompareExchange(ref maxConcurrentSends, active, observed) != observed);
        }
    }
}
