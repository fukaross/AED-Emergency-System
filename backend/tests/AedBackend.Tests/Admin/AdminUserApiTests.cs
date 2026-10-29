using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Responder;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Persistence;

namespace AedBackend.Tests.Admin;

public sealed class AdminUserApiTests : AdminApiTestBase
{
    [Fact]
    public async Task ListUsers_MergesExperienceUsersProfilesAndFilters()
    {
        SeedExperienceUsers(("openid-1", "13800138000"), ("openid-2", "13900139000"));
        UseAdminKey(await BootstrapAndLoginAsync());

        var update = await Client.PutAsJsonAsync(
            "/api/admin/users/openid-1",
            new { primaryRole = "teacher", adminRemark = "急救骨干" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var list = await Client.GetAsync("/api/admin/users?primaryRole=teacher&keyword=骨干");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(list)).Data;

        Assert.Equal(1, data.GetProperty("total").GetInt32());
        Assert.Equal("138****8000", data.GetProperty("items")[0].GetProperty("phone").GetString());
        Assert.False(data.GetProperty("items")[0].GetProperty("certified").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("items")[0].GetProperty("level").ValueKind);
    }

    [Fact]
    public async Task Users_ReadRescuerQualificationFromBoundResponder()
    {
        using var fixture = await UserProfileFixture.CreateAsync(
            [ProviderTempData.Responder("rescuer-core", "王小明", "core", "active", "openid-1")]);
        fixture.UseAdminKey();

        var list = await fixture.Client.GetAsync("/api/admin/users?responderStatus=active&level=core");
        var listData = (await ReadAsync<ApiResponse<JsonElement>>(list)).Data;
        var detail = await fixture.Client.GetAsync("/api/admin/users/openid-1");
        var detailData = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;

        Assert.Equal(1, listData.GetProperty("total").GetInt32());
        Assert.True(listData.GetProperty("items")[0].GetProperty("certified").GetBoolean());
        Assert.Equal("core", listData.GetProperty("items")[0].GetProperty("level").GetString());
        Assert.Equal("active", listData.GetProperty("items")[0].GetProperty("responderStatus").GetString());
        Assert.Equal("rescuer-core", detailData.GetProperty("rescuerSummary").GetProperty("responderProfileId").GetString());
        Assert.True(detailData.GetProperty("rescuerSummary").GetProperty("certified").GetBoolean());
    }

    [Fact]
    public async Task UserProfileNameAndAvatar_AreVisibleInAdminUsers()
    {
        SeedExperienceUsers(
            ("openid-1", "13800138000", "王小明", "/uploads/avatar/wang.jpg"),
            ("openid-2", "13900139000", null, null));
        UseAdminKey(await BootstrapAndLoginAsync());

        var list = await Client.GetAsync("/api/admin/users?keyword=王小明");
        var listData = (await ReadAsync<ApiResponse<JsonElement>>(list)).Data;
        var detail = await Client.GetAsync("/api/admin/users/openid-1");
        var detailData = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(1, listData.GetProperty("total").GetInt32());
        Assert.Equal("王小明", listData.GetProperty("items")[0].GetProperty("name").GetString());
        Assert.Equal(
            "/uploads/avatar/wang.jpg",
            listData.GetProperty("items")[0].GetProperty("avatarUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal("王小明", detailData.GetProperty("name").GetString());
        Assert.Equal("/uploads/avatar/wang.jpg", detailData.GetProperty("avatarUrl").GetString());
    }

    [Fact]
    public async Task UserProfileUpdate_IsImmediatelyVisibleInAdminUsers()
    {
        using var fixture = await UserProfileFixture.CreateAsync();
        fixture.UseUserToken();

        var updateProfile = await fixture.Client.PutAsJsonAsync(
            "/api/user/profile",
            new
            {
                name = "马希罗",
                phone = "13123231314",
                avatarUrl = "/uploads/avatar/new.jpg"
            });
        Assert.Equal(HttpStatusCode.OK, updateProfile.StatusCode);

        fixture.UseAdminKey();
        var list = await fixture.Client.GetAsync("/api/admin/users?keyword=13123231314");
        var listData = (await ReadAsync<ApiResponse<JsonElement>>(list)).Data;
        var detail = await fixture.Client.GetAsync("/api/admin/users/openid-1");
        var detailData = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(1, listData.GetProperty("total").GetInt32());
        Assert.Equal("马希罗", listData.GetProperty("items")[0].GetProperty("name").GetString());
        Assert.Equal("131****1314", listData.GetProperty("items")[0].GetProperty("phone").GetString());
        Assert.Equal(
            "/uploads/avatar/new.jpg",
            listData.GetProperty("items")[0].GetProperty("avatarUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal("马希罗", detailData.GetProperty("name").GetString());
        Assert.Equal("131****1314", detailData.GetProperty("phone").GetString());
        Assert.Equal("/uploads/avatar/new.jpg", detailData.GetProperty("avatarUrl").GetString());
    }

    [Fact]
    public async Task UpdateUser_OnlyChangesProvidedFields()
    {
        SeedExperienceUsers(("openid-1", "13800138000"));
        UseAdminKey(await BootstrapAndLoginAsync());

        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync(
            "/api/admin/users/openid-1",
            new { primaryRole = "teacher", accountStatus = "disabled" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync(
            "/api/admin/users/openid-1",
            new { adminRemark = "仅更新备注" })).StatusCode);

        var detail = await Client.GetAsync("/api/admin/users/openid-1");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;
        Assert.Equal("teacher", data.GetProperty("primaryRole").GetString());
        Assert.Equal("disabled", data.GetProperty("accountStatus").GetString());
        Assert.False(data.GetProperty("certified").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("level").ValueKind);
        Assert.Equal("仅更新备注", data.GetProperty("adminRemark").GetString());
    }

    [Fact]
    public async Task UpdateUser_RejectsRescuerQualificationFields()
    {
        SeedExperienceUsers(("openid-1", "13800138000"));
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.PutAsJsonAsync(
            "/api/admin/users/openid-1",
            new { certified = true, responderStatus = "active", level = "core" });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task Users_RejectInvalidEnumAndMissingUser()
    {
        SeedExperienceUsers(("openid-1", "13800138000"));
        UseAdminKey(await BootstrapAndLoginAsync());

        var invalidQuery = await Client.GetAsync("/api/admin/users?primaryRole=visitor");
        Assert.Equal(HttpStatusCode.BadRequest, invalidQuery.StatusCode);

        var invalidUpdate = await Client.PutAsJsonAsync(
            "/api/admin/users/openid-1",
            new { accountStatus = "locked" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);

        var missing = await Client.PutAsJsonAsync(
            "/api/admin/users/missing-openid",
            new { primaryRole = "teacher" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private sealed class UserProfileFixture : IDisposable
    {
        private readonly ProviderTempData temp;
        private readonly AdminTestApplication app;

        private UserProfileFixture(ProviderTempData temp, AdminTestApplication app)
        {
            this.temp = temp;
            this.app = app;
            Client = app.CreateClient();
        }

        public HttpClient Client { get; }

        public static async Task<UserProfileFixture> CreateAsync(
            ResponderProfileRecord[]? responders = null)
        {
            var temp = new ProviderTempData();
            await temp.WriteValidDataAsync();
            await temp.WriteUserJsonAsync("openid-1", "13800138000", DefaultUserToken);
            if (responders is not null) await temp.WriteResponderJsonAsync(responders);
            await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
            return new UserProfileFixture(temp, new AdminTestApplication(temp.Root, SqliteConfig(temp)));
        }

        public void UseAdminKey()
        {
            Client.DefaultRequestHeaders.Authorization = null;
            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
            Client.DefaultRequestHeaders.Add("X-Aed-Admin-Key", ProviderTempData.AdminSessionKey);
        }

        public void UseUserToken()
        {
            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", DefaultUserToken);
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
