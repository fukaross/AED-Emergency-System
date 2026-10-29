using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Infrastructure.Demo;
using AedBackend.Tests.Admin;
using Microsoft.Data.Sqlite;

namespace AedBackend.Tests.Demo;

/**
 * CONTRACT-003 受限本地演示的 API 级验收：
 * 固定 code 映射真实用户、未知 code 参数错误、账号禁用 403/1018、资格/加入/读取权限不绕过、
 * 演示通知“未发送”、外部发送路由与非回环/非 Development 启动拒绝。
 */
public sealed class DemoModeApiTests : IAsyncLifetime
{
    private const string SubscribeTemplateId = "demo-subscribe-template";
    private const string AdminPassword = "DemoAdmin#2026x";

    private string dataRoot = string.Empty;
    private AdminTestApplication? application;
    private HttpClient? client;

    public async Task InitializeAsync()
    {
        dataRoot = Path.Combine(Path.GetTempPath(), $"aed-demo-api-{Guid.NewGuid():N}");
        await new DemoDataInitializer().RunAsync(
            new DemoInitializationRequest { DataRoot = dataRoot },
            CancellationToken.None);
        application = CreateDemoApplication(dataRoot);
        client = application.CreateClient();
    }

    public Task DisposeAsync()
    {
        client?.Dispose();
        application?.Dispose();
        if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DemoLogin_FixedCodes_MapToDemoRoleIdentities()
    {
        var requester = await LoginAsync("demo-requester");
        var rescuer = await LoginAsync("demo-rescuer");
        var unqualified = await LoginAsync("demo-unqualified");

        Assert.Equal("demo-openid-requester", requester.GetProperty("openId").GetString());
        Assert.Equal("13000000001", requester.GetProperty("phone").GetString());
        Assert.Equal("demo-openid-rescuer-east", rescuer.GetProperty("openId").GetString());
        Assert.Equal("demo-openid-unqualified", unqualified.GetProperty("openId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(requester.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task DemoLogin_UnknownCode_ReturnsParameterError()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/wechat-login", new { code = "demo-nobody" });
        var body = await ReadAsync<JsonElement>(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1001, body.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task DemoLogin_DisabledAccount_Returns403And1018()
    {
        await BootstrapAndLoginAdminAsync();
        var disable = await Client.PutAsJsonAsync(
            "/api/admin/users/demo-openid-rescuer-east",
            new { accountStatus = "disabled" });
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        var response = await Client.PostAsJsonAsync("/api/auth/wechat-login", new { code = "demo-rescuer" });
        var body = await ReadAsync<JsonElement>(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1018, body.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task DemoBusinessFlow_KeepsQualificationAndReadPermissions()
    {
        var requesterToken = await LoginAndGetTokenAsync("demo-requester");
        var rescuerToken = await LoginAndGetTokenAsync("demo-rescuer");
        var unqualifiedToken = await LoginAndGetTokenAsync("demo-unqualified");

        var publish = await SendAsync(
            HttpMethod.Post,
            "/api/help/request",
            requesterToken,
            new
            {
                lat = 31.2459,
                lng = 121.5061,
                address = "东区演示集合点",
                content = "演示求助：需要 AED",
                type = "help_hand",
                requestChannel = "user"
            });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var helpId = (await ReadAsync<JsonElement>(publish)).GetProperty("data").GetProperty("id").GetString()!;

        var unqualifiedJoin = await SendAsync(
            HttpMethod.Post, $"/api/help/{helpId}/session/join", unqualifiedToken, new { });
        Assert.Equal(HttpStatusCode.Forbidden, unqualifiedJoin.StatusCode);
        Assert.Equal(1003, (await ReadAsync<JsonElement>(unqualifiedJoin)).GetProperty("code").GetInt32());

        var unqualifiedDetail = await SendAsync(HttpMethod.Get, $"/api/help/{helpId}", unqualifiedToken);
        Assert.Equal(HttpStatusCode.Forbidden, unqualifiedDetail.StatusCode);

        var rescuerJoin = await SendAsync(
            HttpMethod.Post, $"/api/help/{helpId}/session/join", rescuerToken, new { });
        Assert.Equal(HttpStatusCode.OK, rescuerJoin.StatusCode);

        var rescuerDetail = await SendAsync(HttpMethod.Get, $"/api/help/{helpId}", rescuerToken);
        Assert.Equal(HttpStatusCode.OK, rescuerDetail.StatusCode);

        var requesterView = await SendAsync(HttpMethod.Get, $"/api/help/{helpId}/rescue-view", requesterToken);
        Assert.Equal(HttpStatusCode.OK, requesterView.StatusCode);
    }

    [Fact]
    public async Task DemoNotification_ReturnsNotSentInsteadOfRealDelivery()
    {
        await SeedSubscriptionAsync("demo-openid-rescuer-east", SubscribeTemplateId);
        var requesterToken = await LoginAndGetTokenAsync("demo-requester");

        var publish = await SendAsync(
            HttpMethod.Post,
            "/api/help/request",
            requesterToken,
            new
            {
                lat = 31.2459,
                lng = 121.5061,
                address = "东区演示集合点",
                content = "演示求助：需要 AED",
                type = "help_hand",
                requestChannel = "user"
            });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var notification = (await ReadAsync<JsonElement>(publish))
            .GetProperty("data").GetProperty("notification");
        Assert.Equal(1, notification.GetProperty("targetCount").GetInt32());
        var recipient = notification.GetProperty("recipients")[0];
        Assert.False(recipient.GetProperty("sent").GetBoolean());
        Assert.Equal(DemoAwareSubscribeMessageSender.DemoNotSentErrorCode, recipient.GetProperty("weChatErrorCode").GetInt32());
        Assert.Contains("演示模式未发送", recipient.GetProperty("weChatErrorMessage").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DemoPhoneBindingAndProfilePhoneChange_AreRejected()
    {
        var requesterToken = await LoginAndGetTokenAsync("demo-requester");

        var bind = await SendAsync(
            HttpMethod.Post, "/api/auth/wechat-phone", requesterToken, new { phone = "13000009999" });
        Assert.Equal(HttpStatusCode.Forbidden, bind.StatusCode);
        Assert.Equal(1003, (await ReadAsync<JsonElement>(bind)).GetProperty("code").GetInt32());

        var update = await SendAsync(
            HttpMethod.Put, "/api/user/profile", requesterToken, new { phone = "13000009999" });
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
    }

    [Fact]
    public async Task DemoDevSendEndpoints_AreExplicitlyRejected()
    {
        var adminKey = await BootstrapAndLoginAdminAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/dev/wechat/subscribe-message/send")
        {
            Content = JsonContent.Create(new
            {
                openId = "demo-openid-rescuer-east",
                data = new { thing2 = new { value = "x" } }
            })
        };
        request.Headers.Add("X-Aed-Admin-Key", adminKey);

        var response = await Client.SendAsync(request);
        var body = await ReadAsync<JsonElement>(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1003, body.GetProperty("code").GetInt32());
        Assert.Contains("演示模式禁止", body.GetProperty("message").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RealMode_DoesNotAcceptDemoCodes()
    {
        using var realModeApplication = new AdminTestApplication(
            dataRoot,
            new Dictionary<string, string?>
            {
                ["Data:Provider"] = "Sqlite",
                ["Data:SqlitePath"] = Path.Combine(dataRoot, "aed.db"),
                ["Demo:Enabled"] = "false"
            });
        using var realModeClient = realModeApplication.CreateClient();

        var response = await realModeClient.PostAsJsonAsync(
            "/api/auth/wechat-login",
            new { code = "demo-requester" });
        var body = await ReadAsync<JsonElement>(response);

        // 真实模式没有演示适配器，也不自动切 Mock：缺 AppId/AppSecret 时给明确配置错误。
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1001, body.GetProperty("code").GetInt32());
        Assert.Contains("WeChat:AppId", body.GetProperty("message").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupFails_WhenDemoEnabledOutsideDevelopment()
    {
        using var productionApplication = new AdminTestApplication(
            dataRoot,
            new Dictionary<string, string?>
            {
                ["Data:Provider"] = "Sqlite",
                ["Data:SqlitePath"] = Path.Combine(dataRoot, "aed.db"),
                ["Demo:Enabled"] = "true",
                ["urls"] = "http://127.0.0.1:5150"
            },
            environmentName: "Production");

        var exception = Assert.Throws<DemoConfigurationException>(() => productionApplication.CreateClient());
        Assert.Contains("Development", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupFails_WhenDemoEnabledWithNonLoopbackListener()
    {
        using var exposedApplication = new AdminTestApplication(
            dataRoot,
            new Dictionary<string, string?>
            {
                ["Data:Provider"] = "Sqlite",
                ["Data:SqlitePath"] = Path.Combine(dataRoot, "aed.db"),
                ["Demo:Enabled"] = "true",
                ["urls"] = "http://0.0.0.0:5150"
            });

        var exception = Assert.Throws<DemoConfigurationException>(() => exposedApplication.CreateClient());
        Assert.Contains("回环", exception.Message, StringComparison.Ordinal);
    }

    private HttpClient Client => client ?? throw new InvalidOperationException("Test client not initialized.");

    private static AdminTestApplication CreateDemoApplication(string root)
    {
        return new AdminTestApplication(root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = Path.Combine(root, "aed.db"),
            ["Demo:Enabled"] = "true",
            ["WeChat:SubscribeTemplateId"] = SubscribeTemplateId
        });
    }

    private async Task<JsonElement> LoginAsync(string code)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/wechat-login", new { code });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync<JsonElement>(response)).GetProperty("data");
    }

    private async Task<string> LoginAndGetTokenAsync(string code)
    {
        return (await LoginAsync(code)).GetProperty("token").GetString()!;
    }

    private async Task<string> BootstrapAndLoginAdminAsync()
    {
        using var bootstrap = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/bootstrap")
        {
            Content = JsonContent.Create(new { username = "demo-admin", password = AdminPassword })
        };
        bootstrap.Headers.Add("X-Aed-Admin-Key", AdminApiTestBase.LegacyAdminKey);
        var bootstrapResponse = await Client.SendAsync(bootstrap);
        Assert.Equal(HttpStatusCode.OK, bootstrapResponse.StatusCode);

        var login = await Client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new { username = "demo-admin", password = AdminPassword, deviceId = "demo-api-test-device" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await ReadAsync<JsonElement>(login);
        var adminKey = body.GetProperty("data").GetProperty("adminKey").GetString()!;
        Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
        Client.DefaultRequestHeaders.Add("X-Aed-Admin-Key", adminKey);
        return adminKey;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string token,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await Client.SendAsync(request);
    }

    private async Task SeedSubscriptionAsync(string openId, string templateId)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={Path.Combine(dataRoot, "aed.db")};Pooling=False;Default Timeout=5");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO experience_user_subscriptions (OpenId, TemplateId, Status, UpdatedAt)
            VALUES ($openId, $templateId, 'accept', $updatedAt);
            """;
        command.Parameters.AddWithValue("$openId", openId);
        command.Parameters.AddWithValue("$templateId", templateId);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<T>();
        return body ?? throw new InvalidOperationException("Response body was empty.");
    }
}
