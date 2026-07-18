using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text.Json;
using AedBackend.Api.WebSockets;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Help;
using AedBackend.Tests.Admin;
using AedBackend.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Tests.Rescue;

public sealed class RescueSessionJoinFlowRegressionTests
{
    private const string ResponderToken = "token-responder";

    [Fact]
    public async Task Join_ConcurrentDuplicate_BroadcastsJoinedParticipantAndHelpStatusOnce()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await temp.WriteUsersJsonAsync(("openid-1", "token-requester"), ("openid-2", ResponderToken));
        await temp.WriteBoundResponderJsonAsync("openid-2");
        using var app = new AdminTestApplication(temp.Root);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ResponderToken);
        var broadcaster = app.Services.GetRequiredService<RescueSessionBroadcaster>();
        var socket = new RecordingWebSocket();
        var registration = Register(broadcaster, app, socket);

        var responses = await Task.WhenAll(
            client.PostAsync("/api/help/help-01/session/join", null),
            client.PostAsync("/api/help/help-01/session/join", null));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var envelopes = socket.Envelopes.ToArray();
        Assert.Single(envelopes, envelope =>
            envelope.GetProperty("type").GetString() == "message"
            && envelope.GetProperty("data").GetProperty("eventType").GetString() == "joined");
        Assert.Single(envelopes, envelope =>
            envelope.GetProperty("type").GetString() == "participant"
            && envelope.GetProperty("data").GetProperty("openId").GetString() == "openid-2");
        Assert.Single(envelopes, envelope =>
            envelope.GetProperty("type").GetString() == "help_status"
            && envelope.GetProperty("data").GetProperty("helpStatus").GetString() == "responding");

        broadcaster.Unregister(registration);
    }

    [Fact]
    public async Task Join_TransitionFailsOnce_RetryRepairsRespondingInvariantBeforeBroadcast()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await temp.WriteUsersJsonAsync(("openid-1", "token-requester"), ("openid-2", ResponderToken));
        await temp.WriteBoundResponderJsonAsync("openid-2");
        using var app = new AdminTestApplication(
            temp.Root,
            configureServices: services => services.AddSingleton<IHelpRequestStore>(provider =>
                new FailOnceTransitionHelpStore(provider.GetRequiredService<JsonHelpRequestStore>())));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ResponderToken);
        var broadcaster = app.Services.GetRequiredService<RescueSessionBroadcaster>();
        var socket = new RecordingWebSocket();
        var registration = Register(broadcaster, app, socket);

        var failed = await client.PostAsync("/api/help/help-01/session/join", null);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Empty(socket.Envelopes);
        var sessionStore = app.Services.GetRequiredService<IRescueSessionStore>();
        var afterFailure = await sessionStore.FindByHelpIdAsync("help-01", CancellationToken.None);
        Assert.Single(afterFailure!.Participants);
        Assert.Single(afterFailure.Messages, message => message.EventType == "joined");
        var helpStore = app.Services.GetRequiredService<IHelpRequestStore>();
        var published = await helpStore.FindByIdOrRequestNoAsync("help-01", CancellationToken.None);
        Assert.Equal(HelpStatuses.Published, published!.Status);

        var retried = await client.PostAsync("/api/help/help-01/session/join", null);

        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        var repaired = await helpStore.FindByIdOrRequestNoAsync("help-01", CancellationToken.None);
        Assert.Equal(HelpStatuses.Responding, repaired!.Status);
        var finalSession = await sessionStore.FindByHelpIdAsync("help-01", CancellationToken.None);
        Assert.Single(finalSession!.Participants);
        Assert.Single(finalSession.Messages, message => message.EventType == "joined");
        var envelope = Assert.Single(socket.Envelopes);
        Assert.Equal("help_status", envelope.GetProperty("type").GetString());
        Assert.Equal("responding", envelope.GetProperty("data").GetProperty("helpStatus").GetString());

        broadcaster.Unregister(registration);
    }

    private static RescueSocketRegistration Register(
        RescueSessionBroadcaster broadcaster,
        AdminTestApplication app,
        WebSocket socket)
    {
        var tokens = app.Services.GetRequiredService<ISessionTokenService>();
        var identity = new RescueSocketIdentity("openid-2", tokens.HashToken(ResponderToken));
        return broadcaster.Register("help-01", identity, socket)!;
    }

    private sealed class FailOnceTransitionHelpStore(JsonHelpRequestStore inner) : IHelpRequestStore
    {
        private int transitionAttempts;

        public Task<bool> TrySaveIfNoActiveAsync(
            HelpRequestRecord record,
            CancellationToken cancellationToken) =>
            inner.TrySaveIfNoActiveAsync(record, cancellationToken);

        public Task<HelpRequestRecord?> FindByIdOrRequestNoAsync(
            string idOrRequestNo,
            CancellationToken cancellationToken) =>
            inner.FindByIdOrRequestNoAsync(idOrRequestNo, cancellationToken);

        public Task<IReadOnlyList<HelpRequestRecord>> ListByUserAsync(
            string userId,
            CancellationToken cancellationToken) =>
            inner.ListByUserAsync(userId, cancellationToken);

        public Task<IReadOnlyList<HelpRequestRecord>> ListAllAsync(CancellationToken cancellationToken) =>
            inner.ListAllAsync(cancellationToken);

        public Task<HelpRequestOverviewPage> ListOverviewsAsync(
            HelpRequestOverviewQuery query,
            CancellationToken cancellationToken) =>
            inner.ListOverviewsAsync(query, cancellationToken);

        public Task<HelpRequestRecord> AddReportAsync(
            HelpReportUpdate update,
            CancellationToken cancellationToken) =>
            inner.AddReportAsync(update, cancellationToken);

        public Task<HelpRequestRecord> CloseAsync(
            HelpClosureUpdate update,
            CancellationToken cancellationToken) =>
            inner.CloseAsync(update, cancellationToken);

        public Task<HelpRequestRecord> TransitionToRespondingAsync(
            string id,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref transitionAttempts) == 1)
            {
                throw new InvalidOperationException("injected responding transition failure");
            }

            return inner.TransitionToRespondingAsync(id, now, cancellationToken);
        }
    }

    private sealed class RecordingWebSocket : WebSocket
    {
        private readonly ConcurrentQueue<JsonElement> envelopes = new();
        private WebSocketState state = WebSocketState.Open;

        public IEnumerable<JsonElement> Envelopes => envelopes;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => state;
        public override string? SubProtocol => null;

        public override void Abort()
        {
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

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            var envelope = JsonDocument.Parse(buffer.AsMemory()).RootElement.Clone();
            envelopes.Enqueue(envelope);
            return Task.CompletedTask;
        }
    }
}
