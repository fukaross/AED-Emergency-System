using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Rescue;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Rescue;

public sealed class JsonRescueSessionStoreHardeningTests : IDisposable
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
    public async Task MarkReadAsync_DoesNotRegressLastReadSeq()
    {
        var store = CreateRegisteredStore();

        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        await store.AppendMessageAsync(Text("help-1", "openid-a", "张三", "第一条", BaseTime.AddSeconds(1)), CancellationToken.None);
        await store.AppendMessageAsync(Text("help-1", "openid-a", "张三", "第二条", BaseTime.AddSeconds(2)), CancellationToken.None);
        await store.MarkReadAsync(Read("help-1", "openid-a", 3), CancellationToken.None);
        var lastReadSeq = await store.MarkReadAsync(Read("help-1", "openid-a", 1), CancellationToken.None);

        Assert.Equal(3, lastReadSeq);
    }

    [Fact]
    public async Task JoinAsync_ConcurrentDuplicate_ReturnsExactlyOneNewParticipant()
    {
        var store = CreateRegisteredStore();
        var command = Join("help-1", "openid-a", "张三", BaseTime);

        var results = await Task.WhenAll(
            store.JoinAsync(command, CancellationToken.None),
            store.JoinAsync(command, CancellationToken.None));

        Assert.Single(results, result => result.IsNewParticipant);
        Assert.Single(results, result => result.JoinedEvent is not null);
        var session = await store.FindByHelpIdAsync("help-1", CancellationToken.None);
        Assert.NotNull(session);
        Assert.Single(session.Participants);
        Assert.Single(session.Messages);
    }

    [Fact]
    public async Task AppendStatusEventAsync_StatusEventsAreAtomicallyDebouncedInsideStore()
    {
        var store = CreateRegisteredStore();

        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        var initialRevoke = await store.AppendStatusEventAsync(
            Status("help-1", "openid-a", "张三", RescueEventTypes.TakenAed, RescueEventActions.Revoke, BaseTime),
            CancellationToken.None);
        var marked = await store.AppendStatusEventAsync(
            Status("help-1", "openid-a", "张三", RescueEventTypes.TakenAed, RescueEventActions.Mark, BaseTime.AddSeconds(1)),
            CancellationToken.None);
        var repeated = await store.AppendStatusEventAsync(
            Status("help-1", "openid-a", "张三", RescueEventTypes.TakenAed, RescueEventActions.Mark, BaseTime.AddSeconds(2)),
            CancellationToken.None);
        var revoked = await store.AppendStatusEventAsync(
            Status("help-1", "openid-a", "张三", RescueEventTypes.TakenAed, RescueEventActions.Revoke, BaseTime.AddSeconds(3)),
            CancellationToken.None);
        var reRevoked = await store.AppendStatusEventAsync(
            Status("help-1", "openid-a", "张三", RescueEventTypes.TakenAed, RescueEventActions.Revoke, BaseTime.AddSeconds(4)),
            CancellationToken.None);

        Assert.False(initialRevoke.Appended);
        Assert.True(marked.Appended);
        Assert.False(repeated.Appended);
        Assert.True(revoked.Appended);
        Assert.False(reRevoked.Appended);
        Assert.Equal(2, marked.Event!.Seq);
        Assert.Equal(3, revoked.Event!.Seq);
        Assert.True(repeated.State.TakenAed);
        Assert.False(reRevoked.State.TakenAed);
    }

    [Fact]
    public async Task CloseSessionAsync_RejectsLateWritesEvenWhenSessionWasNeverCreated()
    {
        var store = CreateRegisteredStore();

        await store.CloseSessionAsync("help-closed", DateTimeOffset.UtcNow, CancellationToken.None);
        await Assert.ThrowsAsync<RescueSessionClosedException>(() =>
            store.JoinAsync(Join("help-closed", "openid-a", "张三", BaseTime), CancellationToken.None));

        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        await store.CloseSessionAsync("help-1", DateTimeOffset.UtcNow, CancellationToken.None);
        var closed = await store.FindByHelpIdAsync("help-1", CancellationToken.None);

        Assert.NotNull(closed);
        Assert.NotNull(closed.ClosedAt);

        await Assert.ThrowsAsync<RescueSessionClosedException>(() =>
            store.AppendMessageAsync(Text("help-1", "openid-a", "张三", "晚到消息", BaseTime.AddSeconds(1)), CancellationToken.None));
        await Assert.ThrowsAsync<RescueSessionClosedException>(() =>
            store.AppendStatusEventAsync(
                Status("help-1", "openid-a", "张三", RescueEventTypes.Arrived, RescueEventActions.Mark, BaseTime.AddSeconds(1)),
                CancellationToken.None));
        await Assert.ThrowsAsync<RescueSessionClosedException>(() =>
            store.UpdateLocationAsync(
                Location("help-1", "openid-a", 20, 110, BaseTime.AddSeconds(2)),
                CancellationToken.None));
        await Assert.ThrowsAsync<RescueSessionClosedException>(() =>
            store.MarkReadAsync(Read("help-1", "openid-a", 1), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateLocationAsync_IgnoresReportOlderThanStoredLocation()
    {
        var store = CreateRegisteredStore();
        var newerAt = BaseTime.AddSeconds(10);
        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        await store.UpdateLocationAsync(
            Location("help-1", "openid-a", 20.1, 110.1, newerAt),
            CancellationToken.None);

        var participant = await store.UpdateLocationAsync(
            Location("help-1", "openid-a", 19.9, 109.9, BaseTime.AddSeconds(5)),
            CancellationToken.None);

        Assert.Equal(20.1, participant.LastLat);
        Assert.Equal(110.1, participant.LastLng);
        Assert.Equal(newerAt, participant.LocationReportedAt);
        var reloaded = await store.FindByHelpIdAsync("help-1", CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal(newerAt, Assert.Single(reloaded.Participants).LocationReportedAt);
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

    private static RescueSessionStatusCommand Status(
        string helpId,
        string openId,
        string name,
        string eventType,
        string action,
        DateTimeOffset now) => new()
        {
            HelpRequestId = helpId,
            SenderOpenId = openId,
            SenderName = name,
            EventType = eventType,
            Action = action,
            Now = now
        };

    private JsonRescueSessionStore CreateRegisteredStore() => CreateStore(RegisterTempFile());

    private string RegisterTempFile()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"aed-rescue-sessions-hardening-{Guid.NewGuid():N}",
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
