using System.Text.Json;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Rescue;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Rescue;

public sealed class JsonRescueSessionStoreHardeningTests : IDisposable
{
    private static readonly DateTimeOffset BaseTime = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

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
        await store.MarkReadAsync("help-1", "openid-a", 3, CancellationToken.None);
        var session = await store.MarkReadAsync("help-1", "openid-a", 1, CancellationToken.None);

        Assert.Equal(3, session.Participants[0].LastReadSeq);
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
        Assert.Equal(new long[] { 1, 2 }, repeated.Session.Messages.Select(item => item.Seq).ToArray());
        Assert.Equal(new long[] { 1, 2, 3 }, reRevoked.Session.Messages.Select(item => item.Seq).ToArray());
        Assert.True(RescueSessionProjection.FoldParticipantStates(repeated.Session)["openid-a"].TakenAed);
        Assert.False(RescueSessionProjection.FoldParticipantStates(reRevoked.Session)["openid-a"].TakenAed);
    }

    [Fact]
    public async Task CloseSessionAsync_RejectsLateWritesEvenWhenSessionWasNeverCreated()
    {
        var store = CreateRegisteredStore();

        await store.CloseSessionAsync("help-closed", CancellationToken.None);
        await Assert.ThrowsAsync<RescueSessionClosedException>(() =>
            store.JoinAsync(Join("help-closed", "openid-a", "张三", BaseTime), CancellationToken.None));

        await store.JoinAsync(Join("help-1", "openid-a", "张三", BaseTime), CancellationToken.None);
        await store.CloseSessionAsync("help-1", CancellationToken.None);
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
            store.UpdateLocationAsync(new RescueSessionLocationCommand("help-1", "openid-a", 20, 110, BaseTime.AddSeconds(2)), CancellationToken.None));
        await Assert.ThrowsAsync<RescueSessionClosedException>(() =>
            store.MarkReadAsync("help-1", "openid-a", 1, CancellationToken.None));
    }

    [Fact]
    public async Task AppendMessageAsync_CancellationDuringPrimaryFileTruncate_DoesNotCorruptStore()
    {
        var path = RegisterTempFile();
        await SeedLargeSessionAsync(path);
        var originalLength = new FileInfo(path).Length;
        var store = CreateStore(path);
        using var cts = new CancellationTokenSource();

        var operation = store.AppendMessageAsync(
            Text("help-1", "openid-a", "张三", "追加一条", BaseTime.AddSeconds(1)),
            cts.Token);

        var armCancellation = Task.Run(async () =>
        {
            while (!operation.IsCompleted && !cts.IsCancellationRequested)
            {
                if (File.Exists(path) && new FileInfo(path).Length < originalLength)
                {
                    cts.Cancel();
                    break;
                }

                await Task.Delay(1);
            }
        });

        var session = await operation;
        await armCancellation;

        Assert.Contains(session.Messages, item => item.Text == "追加一条");
        var reloaded = await CreateStore(path).FindByHelpIdAsync("help-1", CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Contains(reloaded.Messages, item => item.Text == "追加一条");
    }

    private static RescueSessionJoinCommand Join(string helpId, string openId, string name, DateTimeOffset now) =>
        new(helpId, openId, name, now);

    private static RescueSessionAppendCommand Text(
        string helpId, string openId, string name, string text, DateTimeOffset now) =>
        new(helpId, RescueMessageKinds.Text, openId, name, text, null, null, now);

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

    private static async Task SeedLargeSessionAsync(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = new
        {
            sessions = new[]
            {
                new
                {
                    helpRequestId = "help-1",
                    createdAt = BaseTime,
                    participants = new[]
                    {
                        new
                        {
                            openId = "openid-a",
                            displayName = "张三",
                            joinedAt = BaseTime,
                            lastSeenAt = BaseTime,
                            lastLat = 20.0,
                            lastLng = 110.0,
                            locationReportedAt = BaseTime,
                            lastReadSeq = 0L
                        }
                    },
                    messages = new[]
                    {
                        new
                        {
                            id = Guid.NewGuid().ToString(),
                            seq = 1L,
                            kind = RescueMessageKinds.Text,
                            senderOpenId = "openid-a",
                            senderName = "张三",
                            text = new string('x', 12_000_000),
                            eventType = (string?)null,
                            action = (string?)null,
                            createdAt = BaseTime
                        }
                    }
                }
            }
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload, JsonOptions));
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
