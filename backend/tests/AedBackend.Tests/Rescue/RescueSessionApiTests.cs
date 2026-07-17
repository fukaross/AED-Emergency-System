using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Help;
using AedBackend.Tests.Admin;

namespace AedBackend.Tests.Rescue;

public sealed class RescueSessionApiTests : AdminApiTestBase
{
    private const string RequesterToken = "token-requester";
    private const string ResponderTokenA = "token-responder-a";
    private const string ResponderTokenB = "token-responder-b";
    private const string PlainToken = "token-plain";

    [Fact]
    public async Task Join_RequiresLogin()
    {
        SeedHelpRequests(Help("help-1"));

        var response = await Client.PostAsync("/api/help/help-1/session/join", null);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task Join_RejectsNonCertifiedUser()
    {
        SeedLoggedInExperienceUser();
        SeedHelpRequests(Help("help-1"));

        var response = await Client.PostAsync("/api/help/help-1/session/join", null);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task Join_ReturnsSnapshotWithJoinedEvent()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));

        UseUserToken(ResponderTokenA);
        var response = await Client.PostAsync("/api/help/help-1/session/join", null);
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("responding", data.GetProperty("helpStatus").GetString());
        Assert.Equal("help-1", data.GetProperty("helpId").GetString());
        var participant = Assert.Single(data.GetProperty("participants").EnumerateArray());
        Assert.Equal("openid-2", participant.GetProperty("openId").GetString());
        Assert.Equal("张急救", participant.GetProperty("displayName").GetString());
        Assert.False(participant.GetProperty("takenAed").GetBoolean());
        Assert.Equal("gone", participant.GetProperty("signal").GetString());
        var message = Assert.Single(data.GetProperty("messages").EnumerateArray());
        Assert.Equal("status_event", message.GetProperty("kind").GetString());
        Assert.Equal("joined", message.GetProperty("eventType").GetString());
        Assert.Equal("mark", message.GetProperty("action").GetString());
        Assert.Equal(1, data.GetProperty("latestSeq").GetInt64());
        Assert.False(data.GetProperty("me").GetProperty("takenAed").GetBoolean());
    }

    [Fact]
    public async Task Join_IsIdempotentWithoutDuplicateJoinedEvent()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));

        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);
        var second = await Client.PostAsync("/api/help/help-1/session/join", null);
        var data = (await ReadAsync<ApiResponse<JsonElement>>(second)).Data;

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Single(data.GetProperty("participants").EnumerateArray());
        Assert.Single(data.GetProperty("messages").EnumerateArray());
    }

    [Fact]
    public async Task Join_TransitionsPublishedHelpToResponding()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));

        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);

        UseUserToken(RequesterToken);
        var detail = await Client.GetAsync("/api/help/help-1");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;
        Assert.Equal("responding", data.GetProperty("status").GetString());

        UseUserToken(ResponderTokenB);
        await Client.PostAsync("/api/help/help-1/session/join", null);
        UseUserToken(RequesterToken);
        var after = await Client.GetAsync("/api/help/help-1");
        var afterData = (await ReadAsync<ApiResponse<JsonElement>>(after)).Data;
        Assert.Equal("responding", afterData.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Join_RejectsClosedHelp()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1", HelpStatuses.Closed));

        UseUserToken(ResponderTokenA);
        var response = await Client.PostAsync("/api/help/help-1/session/join", null);

        await AssertErrorAsync(response, HttpStatusCode.Conflict, ErrorCodes.RescueSessionClosed);
    }

    [Fact]
    public async Task GetSession_RequiresParticipant()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));

        UseUserToken(ResponderTokenA);
        var response = await Client.GetAsync("/api/help/help-1/session");

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, ErrorCodes.RescueNotParticipant);
    }

    [Fact]
    public async Task PostMessage_AppendsAndAfterSeqFilters()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);
        UseUserToken(ResponderTokenB);
        await Client.PostAsync("/api/help/help-1/session/join", null);

        var posted = await Client.PostAsJsonAsync(
            "/api/help/help-1/session/messages",
            new { text = "我从东门进" });
        var postedData = (await ReadAsync<ApiResponse<JsonElement>>(posted)).Data;
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        Assert.Equal("text", postedData.GetProperty("kind").GetString());
        Assert.Equal("我从东门进", postedData.GetProperty("text").GetString());
        Assert.Equal(3, postedData.GetProperty("seq").GetInt64());

        UseUserToken(ResponderTokenA);
        var full = await Client.GetAsync("/api/help/help-1/session");
        var fullData = (await ReadAsync<ApiResponse<JsonElement>>(full)).Data;
        Assert.Equal(3, fullData.GetProperty("messages").GetArrayLength());

        var incremental = await Client.GetAsync("/api/help/help-1/session?afterSeq=2");
        var incrementalData = (await ReadAsync<ApiResponse<JsonElement>>(incremental)).Data;
        var messages = incrementalData.GetProperty("messages").EnumerateArray();
        var only = Assert.Single(messages);
        Assert.Equal("我从东门进", only.GetProperty("text").GetString());
        Assert.Equal(3, incrementalData.GetProperty("latestSeq").GetInt64());
    }

    [Fact]
    public async Task PostMessage_ValidatesText()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);

        var empty = await Client.PostAsJsonAsync("/api/help/help-1/session/messages", new { text = "  " });
        await AssertErrorAsync(empty, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var tooLong = await Client.PostAsJsonAsync(
            "/api/help/help-1/session/messages",
            new { text = new string('长', 501) });
        await AssertErrorAsync(tooLong, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task StatusEvents_MarkRevokeAndDebounce()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);

        var marked = await PostStatusEventAsync("taken_aed", "mark");
        Assert.True(marked.GetProperty("appended").GetBoolean());
        Assert.True(marked.GetProperty("me").GetProperty("takenAed").GetBoolean());
        Assert.Equal("taken_aed", marked.GetProperty("event").GetProperty("eventType").GetString());

        var repeated = await PostStatusEventAsync("taken_aed", "mark");
        Assert.False(repeated.GetProperty("appended").GetBoolean());
        Assert.Equal(JsonValueKind.Null, repeated.GetProperty("event").ValueKind);
        Assert.True(repeated.GetProperty("me").GetProperty("takenAed").GetBoolean());

        var revoked = await PostStatusEventAsync("taken_aed", "revoke");
        Assert.True(revoked.GetProperty("appended").GetBoolean());
        Assert.False(revoked.GetProperty("me").GetProperty("takenAed").GetBoolean());

        var reRevoked = await PostStatusEventAsync("taken_aed", "revoke");
        Assert.False(reRevoked.GetProperty("appended").GetBoolean());
    }

    [Fact]
    public async Task StatusEvents_ConcurrentDuplicate_AppendsExactlyOnce()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);

        var responses = await Task.WhenAll(
            Client.PostAsJsonAsync(
                "/api/help/help-1/session/status-events",
                new { eventType = "taken_aed", action = "mark" }),
            Client.PostAsJsonAsync(
                "/api/help/help-1/session/status-events",
                new { eventType = "taken_aed", action = "mark" }));
        var results = await Task.WhenAll(responses.Select(ReadAsync<ApiResponse<JsonElement>>));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Single(results, result => result.Data.GetProperty("appended").GetBoolean());
        var snapshot = await Client.GetAsync("/api/help/help-1/session");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(snapshot)).Data;
        Assert.Single(
            data.GetProperty("messages").EnumerateArray(),
            message => message.GetProperty("eventType").GetString() == "taken_aed");
    }

    [Fact]
    public async Task StatusEvents_RejectsJoinedEventType()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);

        var response = await Client.PostAsJsonAsync(
            "/api/help/help-1/session/status-events",
            new { eventType = "joined", action = "mark" });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task ReportLocation_UpdatesSnapshot()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);

        var invalid = await Client.PutAsJsonAsync(
            "/api/help/help-1/session/location",
            new { lat = 91, lng = 110.5 });
        await AssertErrorAsync(invalid, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var reported = await Client.PutAsJsonAsync(
            "/api/help/help-1/session/location",
            new { lat = 19.9745, lng = 110.5175 });
        Assert.Equal(HttpStatusCode.OK, reported.StatusCode);

        var snapshot = await Client.GetAsync("/api/help/help-1/session");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(snapshot)).Data;
        var me = data.GetProperty("participants").EnumerateArray().First();
        var location = me.GetProperty("location");
        Assert.Equal(19.9745, location.GetProperty("lat").GetDouble(), 4);
        Assert.Equal(110.5175, location.GetProperty("lng").GetDouble(), 4);
        Assert.Equal("ok", me.GetProperty("signal").GetString());
    }

    [Fact]
    public async Task Mine_TracksUnreadAcrossParticipants()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);
        UseUserToken(ResponderTokenB);
        await Client.PostAsync("/api/help/help-1/session/join", null);

        UseUserToken(ResponderTokenA);
        var beforeRead = await GetMineAsync();
        Assert.True(beforeRead.GetProperty("joined").GetBoolean());
        Assert.Equal(1, beforeRead.GetProperty("unreadCount").GetInt32());

        await Client.GetAsync("/api/help/help-1/session");
        var afterRead = await GetMineAsync();
        Assert.Equal(0, afterRead.GetProperty("unreadCount").GetInt32());

        UseUserToken(ResponderTokenB);
        await Client.PostAsJsonAsync("/api/help/help-1/session/messages", new { text = "拿到 AED 了" });
        UseUserToken(ResponderTokenA);
        var afterMessage = await GetMineAsync();
        Assert.Equal(1, afterMessage.GetProperty("unreadCount").GetInt32());
        Assert.Equal("responding", afterMessage.GetProperty("helpStatus").GetString());
    }

    [Fact]
    public async Task Mine_RejectsNonCertifiedUser()
    {
        SeedLoggedInExperienceUser();

        var response = await Client.GetAsync("/api/rescue-sessions/mine");

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task Mine_IncludesActiveSessionBeforeResponderJoins()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);

        var beforeJoin = await GetMineAsync();
        Assert.False(beforeJoin.GetProperty("joined").GetBoolean());
        Assert.Equal(JsonValueKind.Null, beforeJoin.GetProperty("joinedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, beforeJoin.GetProperty("lastActivityAt").ValueKind);
        Assert.Equal(0, beforeJoin.GetProperty("unreadCount").GetInt32());

        await Client.PostAsync("/api/help/help-1/session/join", null);
        var afterJoin = await GetMineAsync();
        Assert.True(afterJoin.GetProperty("joined").GetBoolean());
        Assert.Equal(JsonValueKind.String, afterJoin.GetProperty("joinedAt").ValueKind);
    }

    [Fact]
    public async Task ClosedHelp_RejectsSessionWritesAndRescueView()
    {
        SeedStandardCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderTokenA);
        await Client.PostAsync("/api/help/help-1/session/join", null);
        await Client.PutAsJsonAsync("/api/help/help-1/session/location", new { lat = 19.97, lng = 110.51 });

        UseUserToken(RequesterToken);
        var closed = await Client.PutAsJsonAsync("/api/help/help-1/close", new { status = "closed" });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        UseUserToken(ResponderTokenA);
        var join = await Client.PostAsync("/api/help/help-1/session/join", null);
        await AssertErrorAsync(join, HttpStatusCode.Conflict, ErrorCodes.RescueSessionClosed);
        var snapshot = await Client.GetAsync("/api/help/help-1/session");
        await AssertErrorAsync(snapshot, HttpStatusCode.Conflict, ErrorCodes.RescueSessionClosed);
        var message = await Client.PostAsJsonAsync("/api/help/help-1/session/messages", new { text = "还能发吗" });
        await AssertErrorAsync(message, HttpStatusCode.Conflict, ErrorCodes.RescueSessionClosed);

        UseUserToken(RequesterToken);
        var view = await Client.GetAsync("/api/help/help-1/rescue-view");
        await AssertErrorAsync(view, HttpStatusCode.Conflict, ErrorCodes.RescueSessionClosed);
    }

    private async Task<JsonElement> PostStatusEventAsync(string eventType, string action)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/help/help-1/session/status-events",
            new { eventType, action });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
    }

    private async Task<JsonElement> GetMineAsync()
    {
        var response = await Client.GetAsync("/api/rescue-sessions/mine");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.Single(data.GetProperty("items").EnumerateArray());
    }

    /** 标准角色：求助人 openid-1 + 认证急救员 A(openid-2)/B(openid-3) + 普通用户 openid-4。 */
    private void SeedStandardCast()
    {
        SeedLoggedInExperienceUsers(
            null,
            ("openid-1", RequesterToken),
            ("openid-2", ResponderTokenA),
            ("openid-3", ResponderTokenB),
            ("openid-4", PlainToken));
        SeedBoundResponders(
            ("rescuer-t1", "openid-2", "active", "张急救"),
            ("rescuer-t2", "openid-3", "active", "李急救"));
    }
}
