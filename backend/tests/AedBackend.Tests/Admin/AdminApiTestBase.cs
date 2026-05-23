using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Help;

namespace AedBackend.Tests.Admin;

public abstract class AdminApiTestBase : IAsyncLifetime
{
    public const string LegacyAdminKey = "legacy-test-key";
    protected const string DefaultPassword = "Password123!";

    private readonly string dataRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private AdminTestApplication? application;
    private HttpClient? client;

    protected HttpClient Client => client ?? throw new InvalidOperationException("Client not initialized.");
    protected string AdminAuditLogsPath => Path.Combine(dataRoot, "admin-audit-logs.json");
    protected string AdminUsersPath => Path.Combine(dataRoot, "admin-users.json");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(dataRoot);
        application = new AdminTestApplication(dataRoot);
        client = application.CreateClient();
        return Task.CompletedTask;
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
        return Client.PostAsJsonAsync("/api/admin/auth/login", new { username, password });
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
        var items = users.Select(user => $$"""
                {
                  "openId": "{{user.OpenId}}",
                  "unionId": null,
                  "phone": "{{user.Phone}}",
                  "sessionTokenHash": "hash-{{user.OpenId}}",
                  "createdAt": "2026-05-23T00:00:00+00:00",
                  "lastLoginAt": "2026-05-23T00:00:00+00:00",
                  "subscriptions": {}
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
}
