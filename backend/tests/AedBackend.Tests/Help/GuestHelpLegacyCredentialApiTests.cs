using System.Net;
using System.Net.Http.Json;
using AedBackend.Core.Models.Help;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Admin;
using AedBackend.Tests.Persistence;

namespace AedBackend.Tests.Help;

public sealed class GuestHelpLegacyCredentialApiTests
{
    private const string JsonProvider = "Json";
    private const string SqliteProvider = "Sqlite";
    private const string RequestNo = "HELP-LEGACY";
    private const string FollowupCode = "Q123456";
    private const string CloseToken = "CLOSE12345678";

    [Theory]
    [InlineData(JsonProvider)]
    [InlineData(SqliteProvider)]
    public async Task LegacyShortCredentials_RemainValid(string provider)
    {
        using var temp = new ProviderTempData();
        await temp.WriteMinimalJsonAsync();
        await temp.WriteAsync("help-requests.json", new { requests = new[] { LegacyGuestHelp() } });
        var configuration = await BuildConfigurationAsync(provider, temp);
        using var application = new AdminTestApplication(temp.Root, configuration);
        using var client = application.CreateClient();

        using var query = await client.PostAsJsonAsync(
            "/api/help/guest/query",
            new { requestNo = RequestNo, followupCode = FollowupCode });
        Assert.Equal(HttpStatusCode.OK, query.StatusCode);

        using var close = await client.PostAsJsonAsync(
            "/api/help/guest/close",
            new { requestNo = RequestNo, closeToken = CloseToken });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
    }

    private static HelpRequestRecord LegacyGuestHelp() => new()
    {
        Id = "legacy-guest-help",
        RequestNo = RequestNo,
        RequestChannel = "guest",
        Address = "旧格式测试地址",
        Content = "旧格式凭据兼容测试",
        CreatedAt = DateTimeOffset.UtcNow,
        GuestSession = new HelpGuestSessionRecord(
            "历史游客",
            FollowupCode,
            CloseToken,
            "legacy-anonymous-id",
            false,
            null)
    };

    private static async Task<IReadOnlyDictionary<string, string?>> BuildConfigurationAsync(
        string provider,
        ProviderTempData temp)
    {
        if (provider == JsonProvider) return new Dictionary<string, string?>();
        if (provider != SqliteProvider) throw new ArgumentOutOfRangeException(nameof(provider));
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
        return new Dictionary<string, string?>
        {
            ["Data:Provider"] = SqliteProvider,
            ["Data:SqlitePath"] = temp.DbPath
        };
    }
}
