using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.WeChat;
using AedBackend.Tests.Admin;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Tests.Rescue;

public sealed class RescueSessionAccessApiTests : AdminApiTestBase
{
    private const string SubscribeTemplateId = "test-template";
    private const string RequesterToken = "token-requester";
    private const string ResponderTokenA = "token-responder-a";
    private const string PlainToken = "token-plain";

    private FakeSubscribeMessageSender Sender { get; } = new();

    protected override Action<IServiceCollection>? ConfigureServices => services =>
    {
        services.Configure<WeChatOptions>(options => options.SubscribeTemplateId = SubscribeTemplateId);
        services.AddSingleton<IWeChatSubscribeMessageSender>(Sender);
    };

    [Fact]
    public async Task Publish_CreatesSessionAndNarrowsPushToCertifiedResponders()
    {
        SeedLoggedInExperienceUsers(
            SubscribeTemplateId,
            ("openid-1", RequesterToken),
            ("openid-2", ResponderTokenA),
            ("openid-3", PlainToken));
        // 求助人本人也是认证急救员（应被自我排除）；openid-3 已订阅但无急救资格
        SeedBoundResponders(
            ("rescuer-t0", "openid-1", "active", "求助人"),
            ("rescuer-t1", "openid-2", "active", "张急救"));

        var helpId = await PublishHelpAsync(RequesterToken);

        var sent = Assert.Single(Sender.Sent);
        Assert.Equal("openid-2", sent.OpenId);
        Assert.Equal($"pages-sub/rescue/session?id={helpId}", sent.Page);

        UseUserToken(ResponderTokenA);
        var join = await Client.PostAsync($"/api/help/{helpId}/session/join", null);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
    }

    [Fact]
    public async Task Publish_SkipsDisabledCertifiedResponderAccounts()
    {
        SeedLoggedInExperienceUsers(
            SubscribeTemplateId,
            ("openid-1", RequesterToken),
            ("openid-2", ResponderTokenA),
            ("openid-3", "token-disabled"),
            ("openid-4", PlainToken));
        SeedBoundResponders(
            ("rescuer-t0", "openid-1", "active", "求助人"),
            ("rescuer-t1", "openid-2", "active", "张急救"),
            ("rescuer-t2", "openid-3", "active", "李禁用"));
        UseAdminKey(await BootstrapAndLoginAsync());
        var disabled = await Client.PutAsJsonAsync(
            "/api/admin/users/openid-3",
            new { accountStatus = "disabled" });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);

        var helpId = await PublishHelpAsync(RequesterToken);

        var sent = Assert.Single(Sender.Sent);
        Assert.Equal("openid-2", sent.OpenId);
        Assert.Equal($"pages-sub/rescue/session?id={helpId}", sent.Page);
    }

    [Fact]
    public async Task Detail_TightenedToRequesterOrCertifiedResponder()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));

        UseUserToken(PlainToken);
        var forbidden = await Client.GetAsync("/api/help/help-1");
        await AssertErrorAsync(forbidden, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);

        UseUserToken(RequesterToken);
        var requester = await Client.GetAsync("/api/help/help-1");
        var requesterData = (await ReadAsync<ApiResponse<JsonElement>>(requester)).Data;
        Assert.Equal(HttpStatusCode.OK, requester.StatusCode);
        Assert.Equal("requester", requesterData.GetProperty("viewerRole").GetString());
        Assert.False(requesterData.GetProperty("canSubmitReport").GetBoolean());

        UseUserToken(ResponderTokenA);
        var responder = await Client.GetAsync("/api/help/help-1");
        var responderData = (await ReadAsync<ApiResponse<JsonElement>>(responder)).Data;
        Assert.Equal(HttpStatusCode.OK, responder.StatusCode);
        Assert.Equal("responder", responderData.GetProperty("viewerRole").GetString());
        Assert.True(responderData.GetProperty("canSubmitReport").GetBoolean());
    }

    [Fact]
    public async Task Report_RequiresCertifiedResponder()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));

        UseUserToken(PlainToken);
        var response = await Client.PostAsJsonAsync(
            "/api/help/help-1/report",
            new { images = new[] { "https://example.com/uploads/help/a.jpg" }, recordText = "已处理", isFalseAlarm = false });

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task RescueView_RequesterSeesAnonymizedRealtimeView()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);
        await Client.PostAsJsonAsync(
            "/api/help/help-1/session/status-events",
            new { eventType = "taken_aed", action = "mark" });

        UseUserToken(RequesterToken);
        var view = await Client.GetAsync("/api/help/help-1/rescue-view");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(view)).Data;

        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        Assert.Equal("responding", data.GetProperty("helpStatus").GetString());
        Assert.Equal(1, data.GetProperty("respondersCount").GetInt32());
        var participant = Assert.Single(data.GetProperty("participants").EnumerateArray());
        Assert.False(participant.TryGetProperty("openId", out _));
        Assert.Equal("p-1", participant.GetProperty("participantId").GetString());
        Assert.Equal("张急救", participant.GetProperty("displayName").GetString());
        Assert.True(participant.GetProperty("takenAed").GetBoolean());
        Assert.Equal(2, data.GetProperty("events").GetArrayLength());
        Assert.Equal("joined", data.GetProperty("events")[0].GetProperty("eventType").GetString());
        Assert.Equal("taken_aed", data.GetProperty("events")[1].GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task RescueView_RequiresRequesterOrJoinedParticipant()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));

        UseUserToken(PlainToken);
        var forbidden = await Client.GetAsync("/api/help/help-1/rescue-view");
        await AssertErrorAsync(forbidden, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);

        UseUserToken(ResponderTokenA);
        var unjoined = await Client.GetAsync("/api/help/help-1/rescue-view");
        await AssertErrorAsync(unjoined, HttpStatusCode.Forbidden, ErrorCodes.RescueNotParticipant);

        await Client.PostAsync("/api/help/help-1/session/join", null);
        var allowed = await Client.GetAsync("/api/help/help-1/rescue-view");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task OpsClose_CascadesToSessionAndClearsLocation()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);
        await Client.PutAsJsonAsync(
            "/api/help/help-1/session/location",
            new { lat = 19.974, lng = 110.517 });

        UseLegacyKey();
        var closed = await Client.PutAsJsonAsync(
            "/api/ops/help-requests/help-1/close",
            new { status = HelpStatuses.Closed, note = "兼容入口关闭" });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        UseAdminKey(await BootstrapAndLoginAsync());
        var detail = await Client.GetAsync("/api/admin/rescue-sessions/help-1");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;
        var participant = Assert.Single(data.GetProperty("participants").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, participant.GetProperty("location").ValueKind);

        var logs = await Client.GetAsync("/api/admin/audit-logs?eventName=help.close");
        var logData = (await ReadAsync<ApiResponse<JsonElement>>(logs)).Data;
        Assert.Equal(1, logData.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task GuestClose_CascadesToSession()
    {
        SeedLoggedInExperienceUsers(
            SubscribeTemplateId,
            ("openid-1", RequesterToken),
            ("openid-2", ResponderTokenA));
        SeedBoundResponders(("rescuer-t1", "openid-2", "active", "张急救"));

        UseUserToken(RequesterToken);
        var publish = await Client.PostAsJsonAsync("/api/help/request", new
        {
            lat = 19.974,
            lng = 110.517,
            address = "教学楼A一层大厅",
            content = "有人晕倒需要协助",
            type = "help_hand",
            requestChannel = "guest",
            guestName = "路人甲",
            callbackAllowed = false
        });
        var publishData = (await ReadAsync<ApiResponse<JsonElement>>(publish)).Data;
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var helpId = publishData.GetProperty("id").GetString()!;
        var requestNo = publishData.GetProperty("requestNo").GetString()!;
        var closeToken = publishData.GetProperty("closeToken").GetString()!;

        UseUserToken(ResponderTokenA);
        var join = await Client.PostAsync($"/api/help/{helpId}/session/join", null);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);

        var guestClose = await Client.PostAsJsonAsync(
            "/api/help/guest/close",
            new { requestNo, closeToken });
        Assert.Equal(HttpStatusCode.OK, guestClose.StatusCode);

        var rejoin = await Client.PostAsync($"/api/help/{helpId}/session/join", null);
        await AssertErrorAsync(rejoin, HttpStatusCode.Conflict, ErrorCodes.RescueSessionClosed);
    }

    [Fact]
    public async Task Close_RejectsNonOwner()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));

        UseUserToken(PlainToken);
        var response = await Client.PutAsJsonAsync("/api/help/help-1/close", new { status = "closed" });
        await AssertErrorAsync(response, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);

        UseUserToken(RequesterToken);
        var allowed = await Client.PutAsJsonAsync("/api/help/help-1/close", new { status = "closed" });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    private async Task<string> PublishHelpAsync(string token)
    {
        UseUserToken(token);
        var response = await Client.PostAsJsonAsync("/api/help/request", new
        {
            lat = 19.974,
            lng = 110.517,
            address = "教学楼A一层大厅",
            content = "有人晕倒需要协助",
            type = "help_hand"
        });
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return data.GetProperty("id").GetString()!;
    }

    /** 标准角色：求助人 openid-1 + 认证急救员 A(openid-2) + 普通用户 openid-3。 */
    private void SeedStandardCast()
    {
        SeedLoggedInExperienceUsers(
            null,
            ("openid-1", RequesterToken),
            ("openid-2", ResponderTokenA),
            ("openid-3", PlainToken));
        SeedBoundResponders(("rescuer-t1", "openid-2", "active", "张急救"));
    }
}
