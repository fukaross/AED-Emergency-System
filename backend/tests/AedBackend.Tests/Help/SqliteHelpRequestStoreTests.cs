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

    [Fact]
    public async Task ListOverviewsAsync_Sqlite_SortsByCreatedAtWithoutTranslationError()
    {
        using var temp = new ProviderTempData();
        await temp.WriteMinimalJsonAsync();
        await new JsonToSqliteMigrator().RunAsync(
            temp.Root,
            temp.DbPath,
            CancellationToken.None);
        using var app = CreateSqliteApplication(temp);
        var store = app.Services.GetRequiredService<IHelpRequestStore>();

        var now = DateTimeOffset.UtcNow;
        Assert.True(await store.TrySaveIfNoActiveAsync(
            CreateRequest("help-old", "HELP-OLD", "openid-old", now.AddMinutes(-10)),
            CancellationToken.None));
        Assert.True(await store.TrySaveIfNoActiveAsync(
            CreateRequest("help-mid", "HELP-MID", "openid-mid", now.AddMinutes(-5)),
            CancellationToken.None));
        Assert.True(await store.TrySaveIfNoActiveAsync(
            CreateRequest("help-new", "HELP-NEW", "openid-new", now),
            CancellationToken.None));

        var page = await store.ListOverviewsAsync(
            new HelpRequestOverviewQuery
            {
                Scope = HelpRequestOverviewScope.Active,
                Offset = 0,
                Limit = 10
            },
            CancellationToken.None);

        Assert.Equal(3, page.Total);
        Assert.Equal(
            ["help-new", "help-mid", "help-old"],
            page.Items.Select(item => item.Id).ToArray());
    }

    private static HelpRequestRecord CreateRequest(string id, string requestNo) =>
        CreateRequest(id, requestNo, "openid-1", DateTimeOffset.UtcNow);

    private static HelpRequestRecord CreateRequest(
        string id,
        string requestNo,
        string userId,
        DateTimeOffset createdAt) => new()
        {
            Id = id,
            RequestNo = requestNo,
            UserId = userId,
            Lat = 19.974,
            Lng = 110.517,
            Address = "教学楼A一层大厅",
            Content = "有人晕倒需要协助",
            Type = "help_hand",
            CreatedAt = createdAt,
            RequesterPhone = "REDACTED",
            Timeline = [new HelpTimelineRecord(
            HelpStatuses.Published,
            createdAt,
            null)]
        };

    private static AdminTestApplication CreateSqliteApplication(ProviderTempData temp) =>
        new(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });
}
