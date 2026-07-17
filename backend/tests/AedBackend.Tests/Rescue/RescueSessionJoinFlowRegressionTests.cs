using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text.Json;
using AedBackend.Api.WebSockets;
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
        var connectionId = Guid.NewGuid();
        var socket = new RecordingWebSocket();
        Assert.True(broadcaster.Register("help-01", connectionId, socket));

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

        broadcaster.Unregister("help-01", connectionId);
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
