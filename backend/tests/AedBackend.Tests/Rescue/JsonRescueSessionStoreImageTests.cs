using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;

namespace AedBackend.Tests.Rescue;

public sealed partial class JsonRescueSessionStoreTests
{
    [Fact]
    public async Task JoinAsync_PersistsAvatarUrl()
    {
        var store = CreateRegisteredStore();

        var joined = await store.JoinAsync(
            new RescueSessionJoinCommand
            {
                HelpRequestId = "help-1",
                OpenId = "openid-a",
                DisplayName = "张三",
                AvatarUrl = "https://example.com/avatar.png",
                Now = BaseTime
            },
            CancellationToken.None);

        var participant = Assert.Single(joined.Session.Participants);
        Assert.Equal("https://example.com/avatar.png", participant.AvatarUrl);
    }

    [Fact]
    public async Task AppendImageMessageAsync_AppendsImageMessage()
    {
        var store = CreateRegisteredStore();
        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);

        var message = await store.AppendImageMessageAsync(
            new RescueImageMessageCommand
            {
                HelpRequestId = "help-1",
                SenderOpenId = "openid-a",
                SenderName = "张三",
                ImageUrl = "https://example.com/photo.png",
                Now = BaseTime.AddSeconds(1)
            },
            CancellationToken.None);

        Assert.Equal(RescueMessageKinds.Image, message.Kind);
        Assert.Equal("https://example.com/photo.png", message.ImageUrl);
        Assert.Null(message.Text);
        var reloaded = await store.FindByHelpIdAsync("help-1", CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded.Messages.Count);
        Assert.Equal("https://example.com/photo.png", reloaded.Messages[1].ImageUrl);
    }
}
