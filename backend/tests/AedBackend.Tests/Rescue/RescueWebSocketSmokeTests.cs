using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Tests.Admin;
using Microsoft.AspNetCore.TestHost;

namespace AedBackend.Tests.Rescue;

/** WS 通道 smoke：仅覆盖连接/鉴权与扇出到达，业务断言仍以 HTTP 集成测试为准（ADR 0001）。 */
public sealed class RescueWebSocketSmokeTests : AdminApiTestBase
{
    private const string RequesterToken = "token-requester";
    private const string ResponderTokenA = "token-responder-a";
    private const string ResponderTokenB = "token-responder-b";

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
    public async Task Connect_QueryTokenIsRejected()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateWebSocketClient().ConnectAsync(
                new Uri($"ws://localhost/ws/help/help-1/session?token={ResponderTokenA}"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Connect_NonParticipant_IsRejected()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));

        var client = CreateAuthorizedWebSocketClient(ResponderTokenA);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.ConnectAsync(
                new Uri("ws://localhost/ws/help/help-1/session"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Connect_DisabledParticipant_IsRejected()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);

        UseAdminKey(await BootstrapAndLoginAsync());
        var disabled = await Client.PutAsJsonAsync(
            "/api/admin/users/openid-2",
            new { accountStatus = "disabled" });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateAuthorizedWebSocketClient(ResponderTokenA).ConnectAsync(
                new Uri("ws://localhost/ws/help/help-1/session"),
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

        using var socket = await CreateAuthorizedWebSocketClient(ResponderTokenA).ConnectAsync(
            new Uri("ws://localhost/ws/help/help-1/session"),
            CancellationToken.None);
        Assert.Equal(WebSocketState.Open, socket.State);

        var posted = await Client.PostAsJsonAsync("/api/help/help-1/session/messages", new { text = "我到了" });
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);

        var envelope = await ReceiveEnvelopeAsync(socket);
        Assert.Equal("message", envelope.GetProperty("type").GetString());
        Assert.Equal("我到了", envelope.GetProperty("data").GetProperty("text").GetString());

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }

    [Fact]
    public async Task Participant_ReceivesParticipantEnvelope()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);
        using var socket = await CreateAuthorizedWebSocketClient(ResponderTokenA).ConnectAsync(
            new Uri("ws://localhost/ws/help/help-1/session"),
            CancellationToken.None);

        var reported = await Client.PutAsJsonAsync(
            "/api/help/help-1/session/location",
            new { lat = 19.974, lng = 110.517 });
        Assert.Equal(HttpStatusCode.OK, reported.StatusCode);

        var envelope = await ReceiveEnvelopeAsync(socket);
        Assert.Equal("participant", envelope.GetProperty("type").GetString());
        Assert.Equal("openid-2", envelope.GetProperty("data").GetProperty("openId").GetString());
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }

    [Fact]
    public async Task HelpClose_BroadcastsStatusAndClosesSocket()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);
        using var socket = await CreateAuthorizedWebSocketClient(ResponderTokenA).ConnectAsync(
            new Uri("ws://localhost/ws/help/help-1/session"),
            CancellationToken.None);

        UseUserToken(RequesterToken);
        var closeTask = Client.PutAsJsonAsync(
            "/api/help/help-1/close",
            new { status = "closed", note = "已结束" });
        var envelope = await ReceiveEnvelopeAsync(socket);
        Assert.Equal("help_status", envelope.GetProperty("type").GetString());
        Assert.Equal("closed", envelope.GetProperty("data").GetProperty("helpStatus").GetString());

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await socket.ReceiveAsync(new byte[128], timeout.Token);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ack", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, (await closeTask).StatusCode);
    }

    [Fact]
    public async Task EstablishedConnection_AccountDisabled_ClosesBeforeNextSensitiveFanout()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        await JoinBothRespondersAsync();
        using var socket = await ConnectResponderAAsync();

        UseAdminKey(await BootstrapAndLoginAsync());
        var disabled = await Client.PutAsJsonAsync(
            "/api/admin/users/openid-2",
            new { accountStatus = "disabled" });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);

        await AssertRevokedConnectionClosesAsync(socket);
    }

    [Fact]
    public async Task EstablishedConnection_OldTokenRotated_ClosesBeforeNextSensitiveFanout()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        await JoinBothRespondersAsync();
        using var socket = await ConnectResponderAAsync();

        UseUserToken(ResponderTokenA);
        var refreshed = await Client.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        await AssertRevokedConnectionClosesAsync(socket);
    }

    [Fact]
    public async Task CurrentQualificationRequired_ForHttpAndEstablishedConnection()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        await JoinBothRespondersAsync();
        using var socket = await ConnectResponderAAsync();

        UseAdminKey(await BootstrapAndLoginAsync());
        var inactive = await Client.PutAsJsonAsync(
            "/api/admin/rescuers/rescuer-t1",
            new { qualificationStatus = "inactive" });
        Assert.Equal(HttpStatusCode.OK, inactive.StatusCode);

        UseUserToken(ResponderTokenA);
        await AssertErrorAsync(
            await Client.GetAsync("/api/help/help-1/session"),
            HttpStatusCode.Forbidden,
            ErrorCodes.Forbidden);
        await AssertErrorAsync(
            await Client.GetAsync("/api/help/help-1/rescue-view"),
            HttpStatusCode.Forbidden,
            ErrorCodes.Forbidden);
        await AssertRevokedConnectionClosesAsync(socket);
    }

    private WebSocketClient CreateAuthorizedWebSocketClient(string token)
    {
        var client = CreateWebSocketClient();
        client.ConfigureRequest = request =>
            request.Headers.Authorization = $"Bearer {token}";
        return client;
    }

    private static async Task<JsonElement> ReceiveEnvelopeAsync(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, timeout.Token);
        return JsonDocument.Parse(buffer.AsMemory(0, result.Count)).RootElement.Clone();
    }

    private async Task JoinBothRespondersAsync()
    {
        UseUserToken(ResponderTokenA);
        Assert.Equal(HttpStatusCode.OK,
            (await Client.PostAsync("/api/help/help-1/session/join", null)).StatusCode);
        UseUserToken(ResponderTokenB);
        Assert.Equal(HttpStatusCode.OK,
            (await Client.PostAsync("/api/help/help-1/session/join", null)).StatusCode);
    }

    private Task<WebSocket> ConnectResponderAAsync()
    {
        return CreateAuthorizedWebSocketClient(ResponderTokenA).ConnectAsync(
            new Uri("ws://localhost/ws/help/help-1/session"),
            CancellationToken.None);
    }

    private async Task AssertRevokedConnectionClosesAsync(WebSocket socket)
    {
        UseUserToken(ResponderTokenB);
        var postTask = Client.PostAsJsonAsync(
            "/api/help/help-1/session/messages",
            new { text = "资格变更后的消息" });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await socket.ReceiveAsync(new byte[256], timeout.Token);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ack", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, (await postTask).StatusCode);
    }

    private void SeedStandardCast()
    {
        SeedLoggedInExperienceUsers(
            null,
            ("openid-1", RequesterToken),
            ("openid-2", ResponderTokenA),
            ("openid-3", ResponderTokenB));
        SeedBoundResponders(
            ("rescuer-t1", "openid-2", "active", "张急救"),
            ("rescuer-t2", "openid-3", "active", "李急救"));
    }
}
