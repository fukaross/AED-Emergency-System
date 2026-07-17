using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using AedBackend.Tests.Admin;

namespace AedBackend.Tests.Rescue;

/** WS 通道 smoke：仅覆盖连接/鉴权与扇出到达，业务断言仍以 HTTP 集成测试为准（ADR 0001）。 */
public sealed class RescueWebSocketSmokeTests : AdminApiTestBase
{
    private const string ResponderTokenA = "token-responder-a";

    [Fact]
    public async Task Connect_WithoutToken_IsRejected()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);

        var client = CreateWebSocketClient();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.ConnectAsync(new Uri("ws://localhost/ws/help/help-1/session"), CancellationToken.None));
    }

    [Fact]
    public async Task Connect_NonParticipant_IsRejected()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));

        var client = CreateWebSocketClient();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.ConnectAsync(
                new Uri($"ws://localhost/ws/help/help-1/session?token={ResponderTokenA}"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Participant_ReceivesMessageEnvelope()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        var join = await Client.PostAsync("/api/help/help-1/session/join", null);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);

        using var socket = await CreateWebSocketClient().ConnectAsync(
            new Uri($"ws://localhost/ws/help/help-1/session?token={ResponderTokenA}"),
            CancellationToken.None);
        Assert.Equal(WebSocketState.Open, socket.State);

        var posted = await Client.PostAsJsonAsync("/api/help/help-1/session/messages", new { text = "我到了" });
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, timeout.Token);
        var envelope = JsonDocument.Parse(buffer.AsMemory(0, result.Count)).RootElement;
        Assert.Equal("message", envelope.GetProperty("type").GetString());
        Assert.Equal("我到了", envelope.GetProperty("data").GetProperty("text").GetString());

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }

    private void SeedStandardCast()
    {
        SeedLoggedInExperienceUsers(null, ("openid-2", ResponderTokenA));
        SeedBoundResponders(("rescuer-t1", "openid-2", "active", "张急救"));
    }
}
