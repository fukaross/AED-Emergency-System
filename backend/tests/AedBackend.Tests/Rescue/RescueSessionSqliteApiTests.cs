using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Admin;
using AedBackend.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Tests.Rescue;

/** Sqlite provider 会话冒烟：生产数据源口味下跑通 join → 消息/状态 → 快照与未读闭环。 */
public sealed class RescueSessionSqliteApiTests
{
    private const string ResponderToken = "token-responder";
    private static readonly DateTimeOffset BaseTime = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StoreJoin_ConcurrentDuplicate_ReturnsExactlyOneNewParticipant()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
        using var app = CreateSqliteApplication(temp);
        var store = app.Services.GetRequiredService<IRescueSessionStore>();
        var joinedAt = DateTimeOffset.UtcNow;
        var command = new RescueSessionJoinCommand
        {
            HelpRequestId = "help-01",
            OpenId = "openid-1",
            DisplayName = "急救员甲",
            Now = joinedAt
        };

        var results = await Task.WhenAll(
            store.JoinAsync(command, CancellationToken.None),
            store.JoinAsync(command, CancellationToken.None));

        Assert.Single(results, result => result.IsNewParticipant);
        Assert.Single(results, result => result.JoinedEvent is not null);
        var session = await store.FindByHelpIdAsync("help-01", CancellationToken.None);
        Assert.NotNull(session);
        Assert.Single(session.Participants);
        Assert.Single(session.Messages, message =>
            message.Kind == RescueMessageKinds.StatusEvent
            && message.EventType == RescueEventTypes.Joined);
    }

    [Fact]
    public async Task StoreStatusEvents_ConcurrentDuplicate_AppendsOnce()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
        using var app = CreateSqliteApplication(temp);
        var store = app.Services.GetRequiredService<IRescueSessionStore>();
        await store.JoinAsync(new RescueSessionJoinCommand
        {
            HelpRequestId = "help-01",
            OpenId = "openid-1",
            DisplayName = "急救员甲",
            Now = BaseTime
        }, CancellationToken.None);
        var initialRevoke = await store.AppendStatusEventAsync(new RescueSessionStatusCommand
        {
            HelpRequestId = "help-01",
            SenderOpenId = "openid-1",
            SenderName = "急救员甲",
            EventType = RescueEventTypes.TakenAed,
            Action = RescueEventActions.Revoke,
            Now = BaseTime
        }, CancellationToken.None);
        var command = new RescueSessionStatusCommand
        {
            HelpRequestId = "help-01",
            SenderOpenId = "openid-1",
            SenderName = "急救员甲",
            EventType = RescueEventTypes.TakenAed,
            Action = RescueEventActions.Mark,
            Now = BaseTime.AddSeconds(1)
        };

        var results = await Task.WhenAll(
            store.AppendStatusEventAsync(command, CancellationToken.None),
            store.AppendStatusEventAsync(command, CancellationToken.None));

        Assert.False(initialRevoke.Appended);
        Assert.Single(results, result => result.Appended);
        var session = await store.FindByHelpIdAsync("help-01", CancellationToken.None);
        Assert.NotNull(session);
        Assert.Equal(2, session.Messages.Count);
        var overview = Assert.Single(await store.ListOverviewsAsync(
            new RescueSessionOverviewQuery
            {
                HelpRequestIds = ["help-01"],
                ViewerOpenId = "openid-1"
            },
            CancellationToken.None));
        Assert.Equal(1, overview.ParticipantCount);
        Assert.Equal(2, overview.MessageCount);
        Assert.NotNull(overview.Viewer);
        Assert.Equal(0, overview.Viewer.UnreadCount);
    }

    [Fact]
    public async Task StoreClose_BlocksLateWritesAndReadCursorNeverRegresses()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
        using var app = CreateSqliteApplication(temp);
        var store = app.Services.GetRequiredService<IRescueSessionStore>();
        await store.JoinAsync(new RescueSessionJoinCommand
        {
            HelpRequestId = "help-01",
            OpenId = "openid-1",
            DisplayName = "急救员甲",
            Now = BaseTime
        }, CancellationToken.None);
        await store.MarkReadAsync(Read("help-01", "openid-1", 5), CancellationToken.None);

        var cursor = await store.MarkReadAsync(Read("help-01", "openid-1", 1), CancellationToken.None);
        Assert.Equal(5, cursor);
        await store.CloseSessionAsync("help-01", DateTimeOffset.UtcNow, CancellationToken.None);
        await Assert.ThrowsAsync<RescueSessionClosedException>(() => store.UpdateLocationAsync(
            new RescueSessionLocationCommand
            {
                HelpRequestId = "help-01",
                OpenId = "openid-1",
                Lat = 20,
                Lng = 110,
                Now = BaseTime.AddSeconds(1)
            }, CancellationToken.None));
    }

    [Fact]
    public async Task SessionFlow_WorksOnSqliteProvider()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await temp.WriteUsersJsonAsync(("openid-1", "token-requester"), ("openid-2", ResponderToken));
        await temp.WriteBoundResponderJsonAsync("openid-2");
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);

        using var app = new AdminTestApplication(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ResponderToken);

        var join = await client.PostAsync("/api/help/help-01/session/join", null);
        var joinData = (await ReadAsync(join)).Data;
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        Assert.Equal("responding", joinData.GetProperty("helpStatus").GetString());
        var participant = Assert.Single(joinData.GetProperty("participants").EnumerateArray());
        Assert.Equal("openid-2", participant.GetProperty("openId").GetString());

        var marked = await client.PostAsJsonAsync(
            "/api/help/help-01/session/status-events",
            new { eventType = "taken_aed", action = "mark" });
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);

        var located = await client.PutAsJsonAsync(
            "/api/help/help-01/session/location",
            new { lat = 19.9745, lng = 110.5175 });
        Assert.Equal(HttpStatusCode.OK, located.StatusCode);

        var snapshot = await client.GetAsync("/api/help/help-01/session");
        var snapshotData = (await ReadAsync(snapshot)).Data;
        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);
        var me = Assert.Single(snapshotData.GetProperty("participants").EnumerateArray());
        Assert.True(me.GetProperty("takenAed").GetBoolean());
        Assert.Equal(19.9745, me.GetProperty("location").GetProperty("lat").GetDouble(), 4);
        Assert.Equal(snapshotData.GetProperty("latestSeq").GetInt64(), meSnapshotReadSeq(snapshotData));
    }

    [Fact]
    public async Task CloseCascade_WorksOnSqliteProvider()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await temp.WriteUsersJsonAsync(("openid-1", "token-requester"), ("openid-2", ResponderToken));
        await temp.WriteBoundResponderJsonAsync("openid-2");
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);

        using var app = new AdminTestApplication(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ResponderToken);

        var join = await client.PostAsync("/api/help/help-01/session/join", null);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        await client.PutAsJsonAsync("/api/help/help-01/session/location", new { lat = 19.97, lng = 110.51 });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token-requester");
        var closed = await client.PutAsJsonAsync("/api/help/help-01/close", new { status = "closed" });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        var view = await client.GetAsync("/api/help/help-01/rescue-view");
        var viewResponse = await ReadAsync(view);
        Assert.Equal(HttpStatusCode.Conflict, view.StatusCode);
        Assert.Equal(ErrorCodes.RescueSessionClosed, viewResponse.Code);

        var store = app.Services.GetRequiredService<IRescueSessionStore>();
        var session = await store.FindByHelpIdAsync("help-01", CancellationToken.None);
        var participant = Assert.Single(Assert.IsType<RescueSessionRecord>(session).Participants);
        Assert.Null(participant.LastLat);
        Assert.Null(participant.LastLng);
        Assert.Null(participant.LocationReportedAt);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ResponderToken);
        var rejoin = await client.PostAsync("/api/help/help-01/session/join", null);
        Assert.Equal(HttpStatusCode.Conflict, rejoin.StatusCode);
    }

    [Fact]
    public async Task StoreImageMessage_PersistsImageUrl()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
        using var app = CreateSqliteApplication(temp);
        var store = app.Services.GetRequiredService<IRescueSessionStore>();
        await store.JoinAsync(new RescueSessionJoinCommand
        {
            HelpRequestId = "help-01",
            OpenId = "openid-1",
            DisplayName = "急救员甲",
            AvatarUrl = "https://example.com/avatar.png",
            Now = BaseTime
        }, CancellationToken.None);

        var message = await store.AppendImageMessageAsync(
            new RescueImageMessageCommand
            {
                HelpRequestId = "help-01",
                SenderOpenId = "openid-1",
                SenderName = "急救员甲",
                ImageUrl = "https://example.com/photo.png",
                Now = BaseTime.AddSeconds(1)
            },
            CancellationToken.None);

        Assert.Equal(RescueMessageKinds.Image, message.Kind);
        Assert.Equal("https://example.com/photo.png", message.ImageUrl);
        var session = await store.FindByHelpIdAsync("help-01", CancellationToken.None);
        Assert.NotNull(session);
        Assert.Equal("https://example.com/avatar.png", Assert.Single(session.Participants).AvatarUrl);
        Assert.Equal(2, session.Messages.Count);
        Assert.Equal("https://example.com/photo.png", session.Messages[1].ImageUrl);
    }

    private static long meSnapshotReadSeq(JsonElement snapshot)
    {
        return snapshot.GetProperty("me").GetProperty("lastReadSeq").GetInt64();
    }

    private static RescueSessionReadCommand Read(string helpId, string openId, long lastReadSeq) => new()
    {
        HelpRequestId = helpId,
        OpenId = openId,
        LastReadSeq = lastReadSeq
    };

    private static AdminTestApplication CreateSqliteApplication(ProviderTempData temp) =>
        new(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });

    private static async Task<ApiResponse<JsonElement>> ReadAsync(HttpResponseMessage response)
    {
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        return result ?? throw new InvalidOperationException("Response body was empty.");
    }
}
