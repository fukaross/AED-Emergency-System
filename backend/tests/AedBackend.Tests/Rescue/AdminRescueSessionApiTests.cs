using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Help;
using AedBackend.Tests.Admin;

namespace AedBackend.Tests.Rescue;

public sealed class AdminRescueSessionApiTests : AdminApiTestBase
{
    private const string RequesterToken = "token-requester";
    private const string ResponderTokenA = "token-responder-a";

    [Fact]
    public async Task ListAndDetail_RequireAdminSession()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/admin/rescue-sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/admin/rescue-sessions/help-1")).StatusCode);
    }

    [Fact]
    public async Task List_SplitsActiveAndHistory()
    {
        SeedStandardCast();
        SeedHelpRequests(
            Help("help-active", HelpStatuses.Published, requestNo: "HELP-A"),
            Help("help-closed", HelpStatuses.Closed, requestNo: "HELP-C"));
        UseAdminKey(await BootstrapAndLoginAsync());

        var active = await Client.GetAsync("/api/admin/rescue-sessions");
        var activeData = (await ReadAsync<ApiResponse<JsonElement>>(active)).Data;
        var activeItem = Assert.Single(activeData.GetProperty("items").EnumerateArray());
        Assert.Equal("help-active", activeItem.GetProperty("helpId").GetString());
        Assert.Equal(0, activeItem.GetProperty("participantsCount").GetInt32());

        var history = await Client.GetAsync("/api/admin/rescue-sessions?status=history");
        var historyData = (await ReadAsync<ApiResponse<JsonElement>>(history)).Data;
        var historyItem = Assert.Single(historyData.GetProperty("items").EnumerateArray());
        Assert.Equal("help-closed", historyItem.GetProperty("helpId").GetString());
    }

    [Fact]
    public async Task Detail_ReturnsSnapshotWithRequesterAndArchive()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));

        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);
        await Client.PostAsJsonAsync(
            "/api/help/help-1/session/status-events",
            new { eventType = "taken_aed", action = "mark" });
        await Client.PostAsJsonAsync("/api/help/help-1/session/messages", new { text = "设备已拿到" });

        UseAdminKey(await BootstrapAndLoginAsync());
        var detail = await Client.GetAsync("/api/admin/rescue-sessions/help-1");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal("help-1", data.GetProperty("helpId").GetString());
        Assert.Equal("responding", data.GetProperty("helpStatus").GetString());
        Assert.Equal("openid-1", data.GetProperty("requester").GetProperty("openId").GetString());
        var participant = Assert.Single(data.GetProperty("participants").EnumerateArray());
        Assert.Equal("openid-2", participant.GetProperty("openId").GetString());
        Assert.True(participant.GetProperty("takenAed").GetBoolean());
        Assert.Equal(3, data.GetProperty("messages").GetArrayLength());
        Assert.Equal("设备已拿到", data.GetProperty("messages")[2].GetProperty("text").GetString());

        var repeatedViews = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => Client.GetAsync("/api/admin/rescue-sessions/help-1")));
        Assert.All(repeatedViews, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var logs = await Client.GetAsync("/api/admin/audit-logs?eventName=rescue_session.viewed&keyword=help-1");
        var logData = (await ReadAsync<ApiResponse<JsonElement>>(logs)).Data;
        Assert.Equal(5, logData.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Detail_ReturnsEmptySessionForHelpWithoutActivity()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseAdminKey(await BootstrapAndLoginAsync());

        var detail = await Client.GetAsync("/api/admin/rescue-sessions/help-1");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Empty(data.GetProperty("participants").EnumerateArray());
        Assert.Empty(data.GetProperty("messages").EnumerateArray());
    }

    [Fact]
    public async Task Detail_Returns404ForMissingHelp()
    {
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.GetAsync("/api/admin/rescue-sessions/help-999");

        await AssertErrorAsync(response, HttpStatusCode.NotFound, ErrorCodes.ResourceNotFound);
    }

    private void SeedStandardCast()
    {
        SeedLoggedInExperienceUsers(
            null,
            ("openid-1", RequesterToken),
            ("openid-2", ResponderTokenA));
        SeedBoundResponders(("rescuer-t1", "openid-2", "active", "张急救"));
    }
}
