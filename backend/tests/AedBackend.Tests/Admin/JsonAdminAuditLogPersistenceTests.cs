using AedBackend.Core.Models.Admin;
using AedBackend.Infrastructure.Admin;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Admin;

public sealed class JsonAdminAuditLogPersistenceTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"aed-admin-audit-persistence-{Guid.NewGuid():N}");

    public JsonAdminAuditLogPersistenceTests() => Directory.CreateDirectory(root);

    public void Dispose() => Directory.Delete(root, true);

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"logs\":null}")]
    [InlineData("{\"logs\":[null]}")]
    [InlineData("[]")]
    [InlineData("{")]
    public async Task ListAsync_InvalidExistingDocument_ThrowsInvalidDataWithAbsolutePath(
        string payload)
    {
        var path = FilePath();
        await File.WriteAllTextAsync(path, payload);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateStore(path).ListAsync(CancellationToken.None));

        Assert.Contains(Path.GetFullPath(path), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAtomicallyAsync_FailureBeforeReplace_PreservesPrimaryAndDeletesTemp()
    {
        var path = FilePath();
        await JsonAdminAuditLogPersistence.WriteAtomicallyAsync(path, Database("original"));
        var original = await File.ReadAllTextAsync(path);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            JsonAdminAuditLogPersistence.WriteAtomicallyAsync(path, Database("replacement"), tempPath =>
            {
                Assert.True(File.Exists(tempPath));
                Assert.Equal(original, File.ReadAllText(path));
                throw new InvalidOperationException("stop before replace");
            }));

        Assert.Equal(original, await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.EnumerateFiles(root, "*.tmp"));
    }

    private string FilePath() => Path.Combine(root, "admin-audit-logs.json");

    private static JsonAdminAuditLogStore CreateStore(string path) =>
        new(Options.Create(new AdminOptions { AdminAuditLogsFilePath = path }));

    private static AdminAuditLogDatabase Database(string id) => new()
    {
        Logs =
        [
            new AdminAuditLogRecord
            {
                Id = id,
                EventType = "test",
                EventName = "test.event",
                CreatedAt = new DateTimeOffset(2026, 7, 18, 0, 0, 0, TimeSpan.Zero)
            }
        ]
    };
}
