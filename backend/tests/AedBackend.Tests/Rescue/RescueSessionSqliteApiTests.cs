using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.DTOs.Common;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Admin;
using AedBackend.Tests.Persistence;

namespace AedBackend.Tests.Rescue;

/** Sqlite provider 会话冒烟：生产数据源口味下跑通 join → 消息/状态 → 快照与未读闭环。 */
public sealed class RescueSessionSqliteApiTests
{
    private const string ResponderToken = "token-responder";

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
        var viewData = (await ReadAsync(view)).Data;
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        var participant = Assert.Single(viewData.GetProperty("participants").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, participant.GetProperty("location").ValueKind);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ResponderToken);
        var rejoin = await client.PostAsync("/api/help/help-01/session/join", null);
        Assert.Equal(HttpStatusCode.Conflict, rejoin.StatusCode);
    }

    private static long meSnapshotReadSeq(JsonElement snapshot)
    {
        return snapshot.GetProperty("me").GetProperty("lastReadSeq").GetInt64();
    }

    private static async Task<ApiResponse<JsonElement>> ReadAsync(HttpResponseMessage response)
    {
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        return result ?? throw new InvalidOperationException("Response body was empty.");
    }
}
