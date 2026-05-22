using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.DTOs.Common;

namespace AedBackend.Tests.Admin;

public sealed class AdminUserApiTests : AdminApiTestBase
{
    [Fact]
    public async Task ListUsers_MergesExperienceUsersProfilesAndFilters()
    {
        SeedExperienceUsers(("openid-1", "REDACTED"), ("openid-2", "REDACTED"));
        UseAdminKey(await BootstrapAndLoginAsync());

        var update = await Client.PutAsJsonAsync(
            "/api/admin/users/openid-1",
            new { primaryRole = "teacher", responderStatus = "active", level = "core", adminRemark = "急救骨干" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var list = await Client.GetAsync("/api/admin/users?primaryRole=teacher&responderStatus=active&level=core&keyword=骨干");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(list)).Data;

        Assert.Equal(1, data.GetProperty("total").GetInt32());
        Assert.Equal("138****8000", data.GetProperty("items")[0].GetProperty("phone").GetString());
    }

    [Fact]
    public async Task UpdateUser_OnlyChangesProvidedFields()
    {
        SeedExperienceUsers(("openid-1", "REDACTED"));
        UseAdminKey(await BootstrapAndLoginAsync());

        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync(
            "/api/admin/users/openid-1",
            new { primaryRole = "teacher", certified = true, level = "core" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync(
            "/api/admin/users/openid-1",
            new { adminRemark = "仅更新备注" })).StatusCode);

        var detail = await Client.GetAsync("/api/admin/users/openid-1");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;
        Assert.Equal("teacher", data.GetProperty("primaryRole").GetString());
        Assert.True(data.GetProperty("certified").GetBoolean());
        Assert.Equal("core", data.GetProperty("level").GetString());
        Assert.Equal("仅更新备注", data.GetProperty("adminRemark").GetString());
    }

    [Fact]
    public async Task Users_RejectInvalidEnumAndMissingUser()
    {
        SeedExperienceUsers(("openid-1", "REDACTED"));
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
}
