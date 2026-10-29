using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Help;
using AedBackend.Infrastructure.Help;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Help;

public sealed class JsonHelpRequestIdentityStoreTests
{
    [Fact]
    public async Task TrySaveIfNoActiveAsync_DuplicateRequestNumber_ThrowsWithoutAppending()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"aed-help-identity-{Guid.NewGuid():N}",
            "help-requests.json");
        var store = new JsonHelpRequestStore(Options.Create(new WeChatOptions
        {
            HelpRequestsFilePath = path
        }));

        try
        {
            Assert.True(await store.TrySaveIfNoActiveAsync(
                CreateRequest("help-a", "HELP-SAME", "openid-a"),
                CancellationToken.None));
            await store.CloseAsync(
                new HelpClosureUpdate
                {
                    HelpRequestId = "help-a",
                    Status = HelpStatuses.Closed,
                    ClosedAt = DateTimeOffset.UtcNow
                },
                CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.TrySaveIfNoActiveAsync(
                    CreateRequest("help-b", "HELP-SAME", "openid-b"),
                    CancellationToken.None));
            Assert.Single(await store.ListAllAsync(CancellationToken.None));
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static HelpRequestRecord CreateRequest(string id, string requestNo, string userId) => new()
    {
        Id = id,
        RequestNo = requestNo,
        UserId = userId,
        Lat = 19.974,
        Lng = 110.517,
        Address = "教学楼A一层大厅",
        Content = "有人晕倒需要协助",
        Type = "help_hand",
        CreatedAt = DateTimeOffset.UtcNow,
        RequesterPhone = "13800138000",
        Timeline = [new HelpTimelineRecord(
            HelpStatuses.Published,
            DateTimeOffset.UtcNow,
            null)]
    };
}
