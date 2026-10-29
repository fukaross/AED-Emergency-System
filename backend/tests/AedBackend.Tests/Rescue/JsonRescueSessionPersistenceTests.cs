using System.Text.Json;
using AedBackend.Core.Models.Rescue;
using AedBackend.Infrastructure.Rescue;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Rescue;

public sealed class JsonRescueSessionPersistenceTests : IDisposable
{
    private static readonly DateTimeOffset BaseTime = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"aed-rescue-persistence-{Guid.NewGuid():N}");

    public JsonRescueSessionPersistenceTests() => Directory.CreateDirectory(root);

    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public async Task CloseSessionAsync_ClosedSessionWithLocation_PreservesClosedAtAndClearsLocation()
    {
        var path = FilePath();
        var originalClosedAt = BaseTime.AddMinutes(2);
        await WriteDatabaseAsync(path, ClosedSession(originalClosedAt, hasLocation: true));
        var store = CreateStore(path);

        await store.CloseSessionAsync("help-1", DateTimeOffset.UtcNow, CancellationToken.None);
        var actual = await store.FindByHelpIdAsync("help-1", CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(originalClosedAt, actual.ClosedAt);
        var participant = Assert.Single(actual.Participants);
        Assert.Null(participant.LastLat);
        Assert.Null(participant.LastLng);
        Assert.Null(participant.LocationReportedAt);
    }

    [Fact]
    public async Task CloseSessionAsync_AlreadyNormalizedClosedSession_DoesNotRewriteFile()
    {
        var path = FilePath();
        await WriteDatabaseAsync(path, ClosedSession(BaseTime.AddMinutes(2), hasLocation: false));
        File.SetLastWriteTimeUtc(path, new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var timestamp = File.GetLastWriteTimeUtc(path);

        await CreateStore(path).CloseSessionAsync("help-1", DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"sessions\":null}")]
    [InlineData("{\"sessions\":[null]}")]
    [InlineData("[]")]
    [InlineData("{")]
    public async Task ReadAsync_InvalidExistingDocument_ThrowsInvalidDataWithAbsolutePath(string payload)
    {
        var path = FilePath();
        await File.WriteAllTextAsync(path, payload);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateStore(path).FindByHelpIdAsync("help-1", CancellationToken.None));

        Assert.Contains(Path.GetFullPath(path), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ReturnsEmptyDatabase()
    {
        var database = await JsonRescueSessionPersistence.ReadAsync(FilePath(), CancellationToken.None);

        Assert.Empty(database.Sessions);
    }

    [Fact]
    public async Task WriteAtomicallyAsync_CancellationBeforeReplace_PreservesPrimaryAndDeletesTemp()
    {
        var path = FilePath();
        await WriteDatabaseAsync(path, ClosedSession(BaseTime, hasLocation: false));
        var original = await File.ReadAllTextAsync(path);
        var replacement = Database(new RescueSessionRecord
        {
            HelpRequestId = "help-2",
            CreatedAt = BaseTime
        });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            JsonRescueSessionPersistence.WriteAtomicallyAsync(path, replacement, tempPath =>
            {
                Assert.True(File.Exists(tempPath));
                Assert.Equal(original, File.ReadAllText(path));
                return Task.FromCanceled(cts.Token);
            }));

        Assert.Equal(original, await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.EnumerateFiles(root, "*.tmp"));
    }

    private string FilePath() => Path.Combine(root, "rescue-sessions.json");

    private static JsonRescueSessionStore CreateStore(string path) =>
        new(Options.Create(new WeChatOptions { RescueSessionsFilePath = path }));

    private static async Task WriteDatabaseAsync(string path, RescueSessionRecord session)
    {
        var json = JsonSerializer.Serialize(Database(session), JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    private static JsonRescueSessionDatabase Database(RescueSessionRecord session) => new()
    {
        Sessions = [session]
    };

    private static RescueSessionRecord ClosedSession(DateTimeOffset closedAt, bool hasLocation) => new()
    {
        HelpRequestId = "help-1",
        CreatedAt = BaseTime,
        ClosedAt = closedAt,
        Participants =
        [
            new RescueSessionParticipantRecord
            {
                OpenId = "openid-a",
                DisplayName = "张三",
                JoinedAt = BaseTime,
                LastSeenAt = BaseTime,
                LastLat = hasLocation ? 20.1 : null,
                LastLng = hasLocation ? 110.1 : null,
                LocationReportedAt = hasLocation ? BaseTime.AddSeconds(1) : null
            }
        ]
    };
}
