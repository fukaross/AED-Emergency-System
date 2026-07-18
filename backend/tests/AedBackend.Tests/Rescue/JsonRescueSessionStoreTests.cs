using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Rescue;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Rescue;

public sealed partial class JsonRescueSessionStoreTests : IDisposable
{
    private static readonly DateTimeOffset BaseTime = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
    private readonly List<string> tempFiles = [];

    public void Dispose()
    {
        foreach (var path in tempFiles)
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task GetOrCreateAsync_CreatesOnceAndIsIdempotent()
    {
        var store = CreateRegisteredStore();

        var created = await store.GetOrCreateAsync("help-1", BaseTime, CancellationToken.None);
        var again = await store.GetOrCreateAsync("help-1", BaseTime.AddMinutes(5), CancellationToken.None);

        Assert.Equal("help-1", created.HelpRequestId);
        Assert.Equal(BaseTime, again.CreatedAt);
        Assert.Empty(again.Participants);
        Assert.Empty(again.Messages);
    }

    [Fact]
    public async Task FindByHelpIdAsync_MissingSession_ReturnsNull()
    {
        var store = CreateRegisteredStore();

        Assert.Null(await store.FindByHelpIdAsync("missing", CancellationToken.None));
    }

    [Fact]
    public async Task JoinAsync_NewParticipant_AddsParticipantAndJoinedEvent()
    {
        var store = CreateRegisteredStore();

        var joined = await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        var session = joined.Session;

        Assert.True(joined.IsNewParticipant);
        var participant = Assert.Single(session.Participants);
        Assert.Equal("openid-a", participant.OpenId);
        Assert.Equal("张三", participant.DisplayName);
        Assert.Equal(BaseTime, participant.JoinedAt);
        Assert.Equal(BaseTime, participant.LastSeenAt);
        Assert.Equal(0, participant.LastReadSeq);
        var message = Assert.Single(session.Messages);
        Assert.NotNull(joined.JoinedEvent);
        Assert.Equal(message.Id, joined.JoinedEvent!.Id);
        Assert.Equal(1, message.Seq);
        Assert.Equal(RescueMessageKinds.StatusEvent, message.Kind);
        Assert.Equal(RescueEventTypes.Joined, message.EventType);
        Assert.Equal(RescueEventActions.Mark, message.Action);
        Assert.False(string.IsNullOrWhiteSpace(message.Id));
    }

    [Fact]
    public async Task JoinAsync_RepeatJoin_RefreshesLastSeenWithoutDuplicateEvent()
    {
        var store = CreateRegisteredStore();

        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        var joined = await store.JoinAsync(
            Join("help-1", "openid-a", "张三", BaseTime.AddSeconds(30)), CancellationToken.None);
        var session = joined.Session;

        Assert.False(joined.IsNewParticipant);
        Assert.Null(joined.JoinedEvent);
        var participant = Assert.Single(session.Participants);
        Assert.Equal(BaseTime, participant.JoinedAt);
        Assert.Equal(BaseTime.AddSeconds(30), participant.LastSeenAt);
        Assert.Single(session.Messages);
    }

    [Fact]
    public async Task AppendMessageAsync_AssignsMonotonicSeq()
    {
        var store = CreateRegisteredStore();

        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        await store.JoinAsync(Join("help-1", "openid-b", "李四", BaseTime), CancellationToken.None);
        var text = await store.AppendMessageAsync(
            Text("help-1", "openid-a", "张三", "马上到", BaseTime.AddSeconds(10)), CancellationToken.None);

        Assert.Equal(3, text.Seq);
        Assert.Equal(RescueMessageKinds.Text, text.Kind);
        Assert.Equal("马上到", text.Text);
        Assert.Null(text.EventType);
        Assert.Null(text.Action);
    }

    [Fact]
    public async Task AppendMessageAsync_RefreshesSenderLastSeenAt()
    {
        var store = CreateRegisteredStore();

        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        await store.AppendMessageAsync(
            Text("help-1", "openid-a", "张三", "hi", BaseTime.AddSeconds(20)), CancellationToken.None);
        var session = await store.FindByHelpIdAsync("help-1", CancellationToken.None);

        Assert.Equal(BaseTime.AddSeconds(20), session!.Participants[0].LastSeenAt);
    }

    [Fact]
    public async Task AppendMessageAsync_MissingSessionOrParticipant_Throws()
    {
        var store = CreateRegisteredStore();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            store.AppendMessageAsync(Text("help-1", "openid-a", "张三", "hi", BaseTime), CancellationToken.None));
        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            store.AppendMessageAsync(Text("help-1", "openid-b", "李四", "hi", BaseTime), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateLocationAsync_OverwritesLocationAndLastSeenAt()
    {
        var store = CreateRegisteredStore();

        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        await store.UpdateLocationAsync(
            Location("help-1", "openid-a", 20.0, 110.0, BaseTime.AddSeconds(5)),
            CancellationToken.None);
        var participant = await store.UpdateLocationAsync(
            Location("help-1", "openid-a", 20.1, 110.1, BaseTime.AddSeconds(10)),
            CancellationToken.None);

        Assert.Equal(20.1, participant.LastLat);
        Assert.Equal(110.1, participant.LastLng);
        Assert.Equal(BaseTime.AddSeconds(10), participant.LocationReportedAt);
        Assert.Equal(BaseTime.AddSeconds(10), participant.LastSeenAt);
    }

    [Fact]
    public async Task UpdateLocationAsync_MissingSessionOrParticipant_Throws()
    {
        var store = CreateRegisteredStore();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            store.UpdateLocationAsync(
                Location("help-1", "openid-a", 1, 1, BaseTime), CancellationToken.None));
        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            store.UpdateLocationAsync(
                Location("help-1", "ghost", 1, 1, BaseTime), CancellationToken.None));
    }

    [Fact]
    public async Task MarkReadAsync_UpdatesLastReadSeq()
    {
        var store = CreateRegisteredStore();

        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        var lastReadSeq = await store.MarkReadAsync(
            Read("help-1", "openid-a", 1),
            CancellationToken.None);

        Assert.Equal(1, lastReadSeq);
    }

    [Fact]
    public async Task CloseSessionAsync_ClearsLocationsButKeepsMessages()
    {
        var store = CreateRegisteredStore();

        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        await store.JoinAsync(Join("help-1", "openid-b", "李四", BaseTime), CancellationToken.None);
        await store.UpdateLocationAsync(
            Location("help-1", "openid-a", 20.0, 110.0, BaseTime.AddSeconds(5)),
            CancellationToken.None);
        await store.AppendMessageAsync(
            Text("help-1", "openid-a", "张三", "到了", BaseTime.AddSeconds(6)), CancellationToken.None);

        await store.CloseSessionAsync("help-1", DateTimeOffset.UtcNow, CancellationToken.None);
        var session = await store.FindByHelpIdAsync("help-1", CancellationToken.None);

        Assert.NotNull(session);
        Assert.All(session.Participants, p =>
        {
            Assert.Null(p.LastLat);
            Assert.Null(p.LastLng);
            Assert.Null(p.LocationReportedAt);
        });
        Assert.Equal(3, session.Messages.Count);
    }

    [Fact]
    public async Task CloseSessionAsync_IdempotentAndMissingSessionIsSilent()
    {
        var store = CreateRegisteredStore();

        await store.CloseSessionAsync("missing", DateTimeOffset.UtcNow, CancellationToken.None);
        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        await store.CloseSessionAsync("help-1", DateTimeOffset.UtcNow, CancellationToken.None);
        await store.CloseSessionAsync("help-1", DateTimeOffset.UtcNow, CancellationToken.None);

        var session = await store.FindByHelpIdAsync("help-1", CancellationToken.None);
        Assert.NotNull(session);
        Assert.Single(session.Messages);
    }

    private static RescueSessionJoinCommand Join(
        string helpId,
        string openId,
        string name,
        DateTimeOffset now) => new()
        {
            HelpRequestId = helpId,
            OpenId = openId,
            DisplayName = name,
            Now = now
        };

    private static RescueTextMessageCommand Text(
        string helpId,
        string openId,
        string name,
        string text,
        DateTimeOffset now) => new()
        {
            HelpRequestId = helpId,
            SenderOpenId = openId,
            SenderName = name,
            Text = text,
            Now = now
        };

    private static RescueSessionLocationCommand Location(
        string helpId,
        string openId,
        double lat,
        double lng,
        DateTimeOffset now) => new()
        {
            HelpRequestId = helpId,
            OpenId = openId,
            Lat = lat,
            Lng = lng,
            Now = now
        };

    private static RescueSessionReadCommand Read(string helpId, string openId, long lastReadSeq) => new()
    {
        HelpRequestId = helpId,
        OpenId = openId,
        LastReadSeq = lastReadSeq
    };

    private JsonRescueSessionStore CreateRegisteredStore() => CreateStore(RegisterTempFile());

    private string RegisterTempFile()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"aed-rescue-sessions-{Guid.NewGuid():N}",
            "rescue-sessions.json");
        tempFiles.Add(path);
        return path;
    }

    private static JsonRescueSessionStore CreateStore(string path)
    {
        return new JsonRescueSessionStore(Options.Create(new WeChatOptions
        {
            RescueSessionsFilePath = path
        }));
    }

    private static void DeleteTempDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
