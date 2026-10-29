using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Responder;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Admin;
using AedBackend.Tests.Persistence;

namespace AedBackend.Tests.User;

public sealed class UserProfileResponderSyncApiTests
{
    private const string UserToken = "user-test-token";
    private const string OpenId = "openid-abc123";

    [Fact]
    public async Task Profile_UpdateSyncsNonEmptyFieldsToBoundResponder()
    {
        using var fixture = await Fixture.CreateAsync([BoundResponder()]);
        fixture.UseUserToken();

        var update = await fixture.Client.PutAsJsonAsync("/api/user/profile", new
        {
            name = "  王小明  ",
            avatarUrl = "/uploads/avatar/a.jpg",
            gender = "male",
            phone = "13900139000"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        fixture.UseAdminKey();
        var detail = await fixture.Client.GetAsync("/api/admin/rescuers/rescuer-bound");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;

        Assert.Equal("王小明", data.GetProperty("name").GetString());
        Assert.Equal("/uploads/avatar/a.jpg", data.GetProperty("avatarUrl").GetString());
        Assert.Equal("13900139000", data.GetProperty("phone").GetString());
        Assert.NotEqual("male", data.GetProperty("gender").GetString());
    }

    [Fact]
    public async Task Profile_UpdateDoesNotClearBoundResponderFields()
    {
        using var fixture = await Fixture.CreateAsync([BoundResponder()]);
        fixture.UseUserToken();

        var update = await fixture.Client.PutAsJsonAsync("/api/user/profile", new
        {
            name = " ",
            avatarUrl = "",
            phone = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var profile = await fixture.Client.GetAsync("/api/user/profile");
        var profileData = (await ReadAsync<ApiResponse<JsonElement>>(profile)).Data;
        Assert.Equal("微信用户 abc123", profileData.GetProperty("name").GetString());
        Assert.Equal(string.Empty, profileData.GetProperty("avatarUrl").GetString());
        Assert.Equal(string.Empty, profileData.GetProperty("phone").GetString());

        fixture.UseAdminKey();
        var detail = await fixture.Client.GetAsync("/api/admin/rescuers/rescuer-bound");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;
        Assert.Equal("档案旧名", data.GetProperty("name").GetString());
        Assert.Equal("/uploads/avatar/old.jpg", data.GetProperty("avatarUrl").GetString());
        Assert.Equal("13800138000", data.GetProperty("phone").GetString());
    }

    [Fact]
    public async Task Profile_UpdateWithoutBoundResponderStillSucceeds()
    {
        using var fixture = await Fixture.CreateAsync([]);
        fixture.UseUserToken();

        var update = await fixture.Client.PutAsJsonAsync("/api/user/profile", new
        {
            name = "未绑定用户",
            phone = "13900139000"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var profile = await fixture.Client.GetAsync("/api/user/profile");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(profile)).Data;
        Assert.Equal("未绑定用户", data.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("rescuerSummary").ValueKind);
    }

    private static ResponderProfileRecord BoundResponder()
    {
        var responder = ProviderTempData.Responder(
            "rescuer-bound",
            "档案旧名",
            "trainee",
            "active",
            OpenId);
        responder.AvatarUrl = "/uploads/avatar/old.jpg";
        responder.Phone = "13800138000";
        return responder;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var result = await response.Content.ReadFromJsonAsync<T>();
        return result ?? throw new InvalidOperationException("Response body was empty.");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ProviderTempData temp;
        private readonly AdminTestApplication app;

        private Fixture(ProviderTempData temp, AdminTestApplication app)
        {
            this.temp = temp;
            this.app = app;
            Client = app.CreateClient();
        }

        public HttpClient Client { get; }

        public static async Task<Fixture> CreateAsync(ResponderProfileRecord[] responders)
        {
            var temp = new ProviderTempData();
            await temp.WriteValidDataAsync();
            await temp.WriteUserJsonAsync(OpenId, "13800138000", UserToken);
            await temp.WriteResponderJsonAsync(responders);
            await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
            return new Fixture(temp, new AdminTestApplication(temp.Root, SqliteConfig(temp)));
        }

        public void UseUserToken()
        {
            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", UserToken);
        }

        public void UseAdminKey()
        {
            Client.DefaultRequestHeaders.Authorization = null;
            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
            Client.DefaultRequestHeaders.Add("X-Aed-Admin-Key", ProviderTempData.AdminSessionKey);
        }

        public void Dispose()
        {
            Client.Dispose();
            app.Dispose();
            temp.Dispose();
        }

        private static Dictionary<string, string?> SqliteConfig(ProviderTempData temp)
        {
            return new Dictionary<string, string?>
            {
                ["Data:Provider"] = "Sqlite",
                ["Data:SqlitePath"] = temp.DbPath
            };
        }
    }
}
