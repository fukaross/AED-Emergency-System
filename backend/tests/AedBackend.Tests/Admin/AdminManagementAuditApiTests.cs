using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.DTOs.Common;

namespace AedBackend.Tests.Admin;

public sealed class AdminManagementAuditApiTests : AdminApiTestBase
{
    [Fact]
    public async Task OrdinaryAdmin_CannotAccessAdminsOrAuditLogs()
    {
        UseAdminKey(await BootstrapAndLoginAsync());
        await CreateAdminAsync("operator");
        var admins = await Client.GetAsync("/api/admin/admins");
        var operatorId = FindAdminId((await ReadAsync<ApiResponse<JsonElement>>(admins)).Data, "operator");

        UseAdminKey(await LoginAndGetKeyAsync("operator"));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync("/api/admin/admins")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync($"/api/admin/admins/{operatorId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await Client.PostAsJsonAsync($"/api/admin/admins/{operatorId}/password-reset", new { password = "NewPassword123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync("/api/admin/audit-logs")).StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanListCreateDeleteAdminButCannotDeleteSelf()
    {
        UseAdminKey(await BootstrapAndLoginAsync());
        await CreateAdminAsync("operator");

        var admins = await Client.GetAsync("/api/admin/admins");
        var items = (await ReadAsync<ApiResponse<JsonElement>>(admins)).Data;
        var rootId = FindAdminId(items, "root");
        var operatorId = FindAdminId(items, "operator");

        Assert.Equal(HttpStatusCode.BadRequest, (await Client.DeleteAsync($"/api/admin/admins/{rootId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.DeleteAsync($"/api/admin/admins/{operatorId}")).StatusCode);

        var afterDelete = await Client.GetAsync("/api/admin/admins");
        var remaining = (await ReadAsync<ApiResponse<JsonElement>>(afterDelete)).Data;
        Assert.DoesNotContain(remaining.EnumerateArray(), item => item.GetProperty("username").GetString() == "operator");
    }

    [Fact]
    public async Task SuperAdmin_CanGetAdminDetail()
    {
        UseAdminKey(await BootstrapAndLoginAsync());
        await CreateAdminAsync("operator");
        var operatorKey = await LoginAndGetKeyAsync("operator");

        var admins = await Client.GetAsync("/api/admin/admins");
        var operatorId = FindAdminId((await ReadAsync<ApiResponse<JsonElement>>(admins)).Data, "operator");
        var detail = await Client.GetAsync($"/api/admin/admins/{operatorId}");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;

        Assert.Equal("operator", data.GetProperty("username").GetString());
        Assert.Equal("admin", data.GetProperty("role").GetString());
        Assert.Single(data.GetProperty("activeSessions").EnumerateArray());
        Assert.DoesNotContain(operatorKey, data.ToString());
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await Client.GetAsync("/api/admin/admins/not-found")).StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanResetOrdinaryAdminPasswordOnce()
    {
        UseAdminKey(await BootstrapAndLoginAsync());
        await CreateAdminAsync("operator");
        var operatorKey = await LoginAndGetKeyAsync("operator");

        var admins = await Client.GetAsync("/api/admin/admins");
        var items = (await ReadAsync<ApiResponse<JsonElement>>(admins)).Data;
        var rootId = FindAdminId(items, "root");
        var operatorId = FindAdminId(items, "operator");
        const string resetPassword = "OperatorNew123!";
        var reset = await Client.PostAsJsonAsync(
            $"/api/admin/admins/{operatorId}/password-reset",
            new { password = resetPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await Client.PostAsJsonAsync($"/api/admin/admins/{rootId}/password-reset", new { password = resetPassword })).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await Client.PostAsJsonAsync("/api/admin/admins/not-found/password-reset", new { password = resetPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("operator", DefaultPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("operator", resetPassword)).StatusCode);

        UseAdminKey(operatorKey);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/admin/auth/me")).StatusCode);
        Assert.DoesNotContain(resetPassword, File.ReadAllText(AdminUsersPath));
        Assert.DoesNotContain(resetPassword, File.ReadAllText(AdminAuditLogsPath));
    }

    [Fact]
    public async Task DeleteAdmin_InvalidatesDeletedAdminSession()
    {
        UseAdminKey(await BootstrapAndLoginAsync());
        await CreateAdminAsync("operator");
        var operatorKey = await LoginAndGetKeyAsync("operator");

        var admins = await Client.GetAsync("/api/admin/admins");
        var operatorId = FindAdminId((await ReadAsync<ApiResponse<JsonElement>>(admins)).Data, "operator");
        Assert.Equal(HttpStatusCode.OK, (await Client.DeleteAsync($"/api/admin/admins/{operatorId}")).StatusCode);

        UseAdminKey(operatorKey);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/admin/auth/me")).StatusCode);
    }

    [Fact]
    public async Task AuditLogs_FilterAndMetadataExcludeSensitiveValues()
    {
        SeedExperienceUsers(("openid-1", "REDACTED"));
        UseAdminKey(await BootstrapAndLoginAsync());

        await CreateAdminAsync("operator");
        var update = await Client.PutAsJsonAsync(
            "/api/admin/users/openid-1",
            new { adminRemark = "敏感字段检查", certified = true });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var logs = await Client.GetAsync("/api/admin/audit-logs?eventType=admin&eventName=admin.user_profile_update&actor=root&keyword=openid-1");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(logs)).Data;
        var metadata = data.GetProperty("items")[0].GetProperty("metadata").ToString();

        Assert.Equal(1, data.GetProperty("total").GetInt32());
        Assert.Contains("changedFields", metadata);
        Assert.DoesNotContain("Password123", metadata);
        Assert.DoesNotContain("REDACTED", metadata);
        Assert.DoesNotContain("legacy-test-key", metadata);
    }

    private static string FindAdminId(JsonElement admins, string username)
    {
        return admins.EnumerateArray()
            .First(item => item.GetProperty("username").GetString() == username)
            .GetProperty("id")
            .GetString()!;
    }
}
