using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Tests.Admin;
using AedBackend.Tests.Persistence;

namespace AedBackend.Tests.Help;

public sealed class HelpReportOssUrlTests
{
    [Fact]
    public async Task SubmitReport_AllowsOssUploadUrl()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await temp.WriteUserJsonAsync("openid-1", "REDACTED", "token-1");
        await temp.WriteBoundResponderJsonAsync("openid-1");
        using var app = new AdminTestApplication(temp.Root, new Dictionary<string, string?>
        {
            ["Upload:Oss:PublicBaseUrl"] = "REDACTED"
        });
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token-1");

        var response = await client.PostAsJsonAsync("/api/help/help-01/report", new
        {
            images = new[] { "REDACTED/uploads/report/a.jpg" },
            recordText = "现场处理完毕",
            isFalseAlarm = false
        });

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, json.GetProperty("code").GetInt32());
    }
}
