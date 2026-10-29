using AedBackend.Core.Services.Rescue;

namespace AedBackend.Tests.Rescue;

public sealed partial class JsonRescueSessionStoreTests
{
    [Fact]
    public async Task ListOverviewsAsync_ReadsOnlyRequestedSessions()
    {
        var store = CreateRegisteredStore();

        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        await store.JoinAsync(Join("help-2", "openid-b", "李四", BaseTime), CancellationToken.None);

        var mine = await store.ListOverviewsAsync(
            new RescueSessionOverviewQuery
            {
                HelpRequestIds = ["help-1"],
                ViewerOpenId = "openid-a"
            },
            CancellationToken.None);

        var item = Assert.Single(mine);
        Assert.Equal("help-1", item.HelpRequestId);
        Assert.NotNull(item.Viewer);
    }

    [Fact]
    public async Task Store_PersistsAcrossInstances()
    {
        var path = RegisterTempFile();

        await CreateStore(path).JoinAsync(
            Join("help-1", "openid-a", "张三", BaseTime),
            CancellationToken.None);
        var session = await CreateStore(path).FindByHelpIdAsync("help-1", CancellationToken.None);

        Assert.NotNull(session);
        Assert.Single(session.Participants);
        Assert.Single(session.Messages);
    }
}
