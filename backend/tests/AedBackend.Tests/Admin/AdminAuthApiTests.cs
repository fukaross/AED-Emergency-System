using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;

namespace AedBackend.Tests.Admin;

public sealed class AdminAuthApiTests : AdminApiTestBase
{
    [Fact]
    public async Task LoginBeforeBootstrap_ReturnsSystemNotInitialized()
    {
        var response = await LoginAsync("root", DefaultPassword);
        var body = await ReadRawAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SYSTEM_NOT_INITIALIZED", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Bootstrap_RejectsDuplicateWrongKeyAndAdminSession()
    {
        var bootstrap = await BootstrapAsync("root", DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, bootstrap.StatusCode);

        var duplicate = await BootstrapAsync("second", DefaultPassword);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var wrongKey = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/bootstrap")
        {
            Content = JsonContent.Create(new { username = "bad", password = DefaultPassword })
        };
        wrongKey.Headers.Add("X-Aed-Admin-Key", "wrong-key");
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.SendAsync(wrongKey)).StatusCode);

        var adminKey = await LoginAndGetKeyAsync("root");
        using var sessionKey = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/bootstrap")
        {
            Content = JsonContent.Create(new { username = "bad2", password = DefaultPassword })
        };
        sessionKey.Headers.Add("X-Aed-Admin-Key", adminKey);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.SendAsync(sessionKey)).StatusCode);
    }

    [Fact]
    public async Task LoginMeLogout_WorkflowSucceeds()
    {
        var adminKey = await BootstrapAndLoginAsync();
        Assert.Equal(43, adminKey.Length);

        UseAdminKey(adminKey);
        var me = await Client.GetAsync("/api/admin/auth/me");
        var body = await ReadAsync<ApiResponse<JsonElement>>(me);
        Assert.Equal("root", body.Data.GetProperty("username").GetString());
        Assert.Equal("super_admin", body.Data.GetProperty("role").GetString());

        var logout = await Client.PostAsync("/api/admin/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/admin/auth/me")).StatusCode);
    }

    [Fact]
    public async Task RevokeSession_InvalidatesTargetSessionOnly()
    {
        await BootstrapAsync("root", DefaultPassword);
        var firstKey = (await LoginAsync("root", DefaultPassword, "test-device-first")).Content;
        var firstBody = await firstKey.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        var firstAdminKey = firstBody!.Data.GetProperty("adminKey").GetString()!;
        var secondKey = await LoginAndGetKeyAsync("root");

        UseAdminKey(firstAdminKey);
        var sessions = await Client.GetAsync("/api/admin/auth/sessions");
        var items = (await ReadAsync<ApiResponse<JsonElement>>(sessions)).Data;
        var targetId = items.EnumerateArray()
            .First(item => !item.GetProperty("current").GetBoolean())
            .GetProperty("id")
            .GetString();

        var revoke = await Client.DeleteAsync($"/api/admin/auth/sessions/{targetId}");
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/admin/auth/me")).StatusCode);

        UseAdminKey(secondKey);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/admin/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ResetSuperAdmin_RequiresLegacyKeyAndRevokesSessions()
    {
        var oldKey = await BootstrapAndLoginAsync();
        UseAdminKey(oldKey);

        var denied = await Client.PostAsJsonAsync(
            "/api/admin/auth/reset-super-admin",
            new { username = "root", password = "NewPassword123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        UseLegacyKey();
        var reset = await Client.PostAsJsonAsync(
            "/api/admin/auth/reset-super-admin",
            new { username = "root", password = "NewPassword123!" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        UseAdminKey(oldKey);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/admin/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("root", DefaultPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("root", "NewPassword123!")).StatusCode);
    }

    [Fact]
    public async Task LoginLockout_UsesOperationRateLimitedCode()
    {
        await BootstrapAsync("root", DefaultPassword);

        for (var index = 0; index < 5; index++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("root", "bad-password")).StatusCode);
        }

        var locked = await LoginAsync("root", DefaultPassword);
        var body = await ReadAsync<ApiResponse>(locked);
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        Assert.Equal(ErrorCodes.OperationRateLimited, body.Code);
    }

    [Fact]
    public async Task SessionLimit_ReturnsSessionLimitCode()
    {
        await BootstrapAsync("root", DefaultPassword);

        for (var index = 0; index < 3; index++)
        {
            var deviceId = $"test-device-{index}-unique";
            Assert.Equal(HttpStatusCode.OK, (await LoginAsync("root", DefaultPassword, deviceId)).StatusCode);
        }

        var limited = await LoginAsync("root", DefaultPassword);
        var body = await ReadRawAsync(limited);
        Assert.Equal(HttpStatusCode.Conflict, limited.StatusCode);
        Assert.Equal("SESSION_LIMIT_REACHED", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task LoginSameDevice_ReplacesOldSessionWithoutConsumingLimit()
    {
        await BootstrapAsync("root", DefaultPassword);

        for (var index = 0; index < 4; index++)
        {
            Assert.Equal(
                HttpStatusCode.OK,
                (await LoginAsync("root", DefaultPassword, "test-device-same-browser")).StatusCode);
        }

        var thirdDevice = await LoginAsync("root", DefaultPassword, "test-device-third");
        Assert.Equal(HttpStatusCode.OK, thirdDevice.StatusCode);
    }

    [Fact]
    public async Task LoginSession_UsesForwardedIpAndSuperAdminCanViewFullIp()
    {
        await BootstrapAsync("root", DefaultPassword);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/login")
        {
            Content = JsonContent.Create(new
            {
                username = "root",
                password = DefaultPassword,
                deviceId = "test-device-forwarded"
            })
        };
        login.Headers.Add("X-Forwarded-For", "203.0.113.9, 128.0.0.1");
        var loginResponse = await Client.SendAsync(login);
        var loginBody = await ReadAsync<ApiResponse<JsonElement>>(loginResponse);
        UseAdminKey(loginBody.Data.GetProperty("adminKey").GetString()!);

        var detail = await Client.GetAsync("/api/admin/auth/sessions");
        var sessions = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;
        var session = sessions.EnumerateArray().Single();
        Assert.Equal("203.0.*.*", session.GetProperty("maskedIpAddress").GetString());
        Assert.Equal(JsonValueKind.Null, session.GetProperty("ipAddress").ValueKind);

        var adminId = (await ReadAsync<ApiResponse<JsonElement>>(
            await Client.GetAsync("/api/admin/auth/me"))).Data.GetProperty("id").GetString();
        var adminDetail = (await ReadAsync<ApiResponse<JsonElement>>(
            await Client.GetAsync($"/api/admin/admins/{adminId}"))).Data;
        var activeSession = adminDetail.GetProperty("activeSessions").EnumerateArray().Single();
        Assert.Equal("203.0.113.9", activeSession.GetProperty("ipAddress").GetString());
    }
}
