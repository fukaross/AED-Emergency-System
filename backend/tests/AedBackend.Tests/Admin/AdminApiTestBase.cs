using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Help;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Tests.Admin;

public abstract class AdminApiTestBase : IAsyncLifetime
{
    public const string LegacyAdminKey = "legacy-test-key";
    protected const string DefaultPassword = "Password123!";
    protected const string DefaultUserToken = "user-test-token";

    private readonly string dataRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private AdminTestApplication? application;
    private HttpClient? client;

    protected HttpClient Client => client ?? throw new InvalidOperationException("Client not initialized.");
    protected string AdminAuditLogsPath => Path.Combine(dataRoot, "admin-audit-logs.json");
    protected string AdminUsersPath => Path.Combine(dataRoot, "admin-users.json");
    protected string UploadsRoot => Path.Combine(dataRoot, "uploads");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(dataRoot);
        application = new AdminTestApplication(dataRoot, configureServices: ConfigureServices);
        client = application.CreateClient();
        return Task.CompletedTask;
    }

    /** 允许测试类替换/追加服务注册（如订阅消息发送器替身），默认不改动。 */
    protected virtual Action<IServiceCollection>? ConfigureServices => null;

    /** 创建 TestServer 内存 WebSocket 客户端，用于 WS smoke 测试。 */
    protected WebSocketClient CreateWebSocketClient()
    {
        return application!.Server.CreateWebSocketClient();
    }

    public Task DisposeAsync()
    {
        client?.Dispose();
        application?.Dispose();
        if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, true);
        return Task.CompletedTask;
    }

    protected async Task<string> BootstrapAndLoginAsync(string username = "root")
    {
        var bootstrap = await BootstrapAsync(username, DefaultPassword);
        Assert.True(bootstrap.IsSuccessStatusCode);
        return await LoginAndGetKeyAsync(username);
    }

    protected async Task<string> LoginAndGetKeyAsync(string username, string password = DefaultPassword)
    {
        var login = await LoginAsync(username, password);
        var body = await ReadAsync<ApiResponse<JsonElement>>(login);
        return body.Data.GetProperty("adminKey").GetString()!;
    }

    protected async Task<HttpResponseMessage> BootstrapAsync(string username, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/bootstrap")
        {
            Content = JsonContent.Create(new { username, password })
        };
        request.Headers.Add("X-Aed-Admin-Key", LegacyAdminKey);
        return await Client.SendAsync(request);
    }

    protected Task<HttpResponseMessage> LoginAsync(string username, string password)
    {
        return LoginAsync(username, password, "test-device-default");
    }

    protected Task<HttpResponseMessage> LoginAsync(
        string username,
        string password,
        string deviceId)
    {
        return Client.PostAsJsonAsync("/api/admin/auth/login", new { username, password, deviceId });
    }

    protected void UseAdminKey(string adminKey)
    {
        Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
        Client.DefaultRequestHeaders.Add("X-Aed-Admin-Key", adminKey);
    }

    protected void UseLegacyKey()
    {
        UseAdminKey(LegacyAdminKey);
    }

    protected void UseUserToken(string token = DefaultUserToken)
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    protected void SeedLoggedInExperienceUser(
        string openId = "openid-1",
        string phone = "REDACTED",
        string token = DefaultUserToken)
    {
        WriteExperienceUsers([
            new ExperienceUserSeed(openId, phone, null, null, HashToken(token))
        ]);
        UseUserToken(token);
    }

    protected async Task CreateAdminAsync(string username, string password = DefaultPassword)
    {
        var create = await Client.PostAsJsonAsync(
            "/api/admin/admins",
            new { username, password });
        Assert.True(create.IsSuccessStatusCode);
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var result = await response.Content.ReadFromJsonAsync<T>();
        return result ?? throw new InvalidOperationException("Response body was empty.");
    }

    protected static async Task<JsonElement> ReadRawAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    protected void SeedExperienceUsers(params (string OpenId, string Phone)[] users)
    {
        WriteExperienceUsers(users.Select(user =>
            new ExperienceUserSeed(user.OpenId, user.Phone, null, null, $"hash-{user.OpenId}")));
    }

    protected void SeedExperienceUsers(
        params (string OpenId, string Phone, string? Name, string? AvatarUrl)[] users)
    {
        WriteExperienceUsers(users.Select(user =>
            new ExperienceUserSeed(
                user.OpenId,
                user.Phone,
                user.Name,
                user.AvatarUrl,
                $"hash-{user.OpenId}")));
    }

    /** 写入多个带登录 token 的体验用户；subscribeTemplateId 非空时同时为每个用户写入该模板 accept 订阅。 */
    protected void SeedLoggedInExperienceUsers(string? subscribeTemplateId, params (string OpenId, string Token)[] users)
    {
        WriteExperienceUsers(
            users.Select(user => new ExperienceUserSeed(
                user.OpenId,
                "REDACTED",
                null,
                null,
                HashToken(user.Token),
                subscribeTemplateId is not null)),
            subscribeTemplateId);
    }

    /** 写入绑定用户与资格状态的急救员档案，用于急救会话准入测试。 */
    protected void SeedBoundResponders(params (string Id, string? BoundOpenId, string QualificationStatus, string Name)[] responders)
    {
        var items = responders.Select(responder => $$"""
            {
              "id": "{{responder.Id}}",
              "recordId": "{{responder.Id}}",
              "name": "{{responder.Name}}",
              "phone": "REDACTED",
              "campus": "guilinyang",
              "qualificationStatus": "{{responder.QualificationStatus}}",
              "boundOpenId": {{JsonValue(responder.BoundOpenId)}}
            }
        """);
        WriteDataFile("rescuer-profiles.json", $$"""
        [
        {{string.Join(",\n", items)}}
        ]
        """);
    }

    private void WriteExperienceUsers(IEnumerable<ExperienceUserSeed> users, string? subscribeTemplateId = null)
    {
        // lastLoginAt 驱动 token 30 天有效期判定，必须使用当前时间，避免固定日期过期导致 401
        var lastLoginAt = DateTimeOffset.UtcNow.ToString("O");
        var items = users.Select(user => $$"""
                {
                  "openId": "{{user.OpenId}}",
                  "unionId": null,
                  "phone": "{{user.Phone}}",
                  "name": {{JsonValue(user.Name)}},
                  "avatarUrl": {{JsonValue(user.AvatarUrl)}},
                  "gender": "unknown",
                  "sessionTokenHash": "{{user.TokenHash}}",
                  "createdAt": "2026-05-23T00:00:00+00:00",
                  "lastLoginAt": "{{lastLoginAt}}",
                  "subscriptions": {{SubscriptionsJson(user, subscribeTemplateId, lastLoginAt)}}
                }
        """);
        WriteDataFile("wechat-experience-users.json", $$"""
        {
          "users": [
        {{string.Join(",\n", items)}}
          ]
        }
        """);
    }

    private static string SubscriptionsJson(ExperienceUserSeed user, string? templateId, string updatedAt)
    {
        if (!user.Subscribed || templateId is null) return "{}";
        return $$"""{ "{{templateId}}": { "templateId": "{{templateId}}", "status": "accept", "updatedAt": "{{updatedAt}}" } }""";
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }

    protected void SeedHelpRequests(params HelpSeed[] requests)
    {
        var items = requests.Select(CreateHelpJson);
        WriteDataFile("help-requests.json", $$"""
        {
          "requests": [
        {{string.Join(",\n", items)}}
          ]
        }
        """);
    }

    /// <summary>写入最小急救员档案（仅 id + 发证日期），用于仪表盘急救员增长图测试。</summary>
    protected void SeedResponders(params (string Id, string? IssueDate)[] responders)
    {
        var items = responders.Select(responder => $$"""
            {
              "id": "{{responder.Id}}",
              "recordId": "{{responder.Id}}",
              "name": "测试急救员",
              "issueDate": {{JsonValue(responder.IssueDate)}}
            }
        """);
        WriteDataFile("rescuer-profiles.json", $$"""
        [
        {{string.Join(",\n", items)}}
        ]
        """);
    }

    protected static HelpSeed Help(
        string id,
        string status = HelpStatuses.Published,
        string content = "测试求助",
        string address = "测试地点",
        string requestNo = "HELP1")
    {
        return new HelpSeed(id, requestNo, status, content, address);
    }

    private void WriteDataFile(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(dataRoot, fileName), content);
    }

    private static string CreateHelpJson(HelpSeed request)
    {
        return $$"""
            {
              "id": "{{request.Id}}",
              "requestNo": "{{request.RequestNo}}",
              "userId": "openid-1",
              "requestChannel": "user",
              "lat": 1,
              "lng": 1,
              "address": "{{request.Address}}",
              "content": "{{request.Content}}",
              "type": "general",
              "status": "{{request.Status}}",
              "createdAt": "{{request.CreatedAt:O}}",
              "closedAt": {{JsonValue(request.ClosedAt)}},
              "closeNote": null,
              "requesterPhone": "REDACTED",
              "images": ["/uploads/help/a.jpg"],
              "responders": [],
              "timeline": [],
              "reports": [
                {
                  "id": "report-{{request.Id}}",
                  "reporterOpenId": "reporter-1",
                  "images": ["/uploads/report/b.jpg"],
                  "recordText": "现场已处理",
                  "isFalseAlarm": false,
                  "submittedAt": "2026-05-23T01:00:00+00:00"
                }
              ]
            }
        """;
    }

    private static string JsonValue(DateTimeOffset? value)
    {
        return value is null ? "null" : $"\"{value.Value:O}\"";
    }

    protected sealed record HelpSeed(
        string Id,
        string RequestNo,
        string Status,
        string Content,
        string Address)
    {
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Parse("2026-05-23T00:00:00+00:00");
        public DateTimeOffset? ClosedAt { get; init; }
    }

    private sealed record ExperienceUserSeed(
        string OpenId,
        string Phone,
        string? Name,
        string? AvatarUrl,
        string TokenHash,
        bool Subscribed = false);

    private static string JsonValue(string? value)
    {
        return value is null ? "null" : JsonSerializer.Serialize(value);
    }

    protected static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode statusCode,
        int errorCode)
    {
        var body = await ReadAsync<ApiResponse<JsonElement>>(response);
        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal(errorCode, body.Code);
    }
}
