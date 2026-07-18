using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Help;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Admin;
using AedBackend.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Tests.Help;

public sealed class SqliteHelpRequestStoreTests
{
    [Fact]
    public async Task TrySaveIfNoActiveAsync_ConcurrentRequests_PersistsExactlyOne()
    {
        using var temp = new ProviderTempData();
        await temp.WriteMinimalJsonAsync();
        await new JsonToSqliteMigrator().RunAsync(
            temp.Root,
            temp.DbPath,
            CancellationToken.None);
        using var app = CreateSqliteApplication(temp);
        var store = app.Services.GetRequiredService<IHelpRequestStore>();

        var results = await Task.WhenAll(
            store.TrySaveIfNoActiveAsync(CreateRequest("help-a", "HELP-A"), CancellationToken.None),
            store.TrySaveIfNoActiveAsync(CreateRequest("help-b", "HELP-B"), CancellationToken.None));

        Assert.Single(results, saved => saved);
        Assert.Single(results, saved => !saved);
        Assert.Single(await store.ListAllAsync(CancellationToken.None));
    }

    private static HelpRequestRecord CreateRequest(string id, string requestNo) => new()
    {
        Id = id,
        RequestNo = requestNo,
        UserId = "openid-1",
        Lat = 19.974,
        Lng = 110.517,
        Address = "教学楼A一层大厅",
        Content = "有人晕倒需要协助",
        Type = "help_hand",
        CreatedAt = DateTimeOffset.UtcNow,
        RequesterPhone = "REDACTED",
        Timeline = [new HelpTimelineRecord(
            HelpStatuses.Published,
            DateTimeOffset.UtcNow,
            null)]
    };

    private static AdminTestApplication CreateSqliteApplication(ProviderTempData temp) =>
        new(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });
}
