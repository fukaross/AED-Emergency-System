using AedBackend.Core.Models.Help;
using AedBackend.Infrastructure.Help;

namespace AedBackend.Tests.Help;

public sealed class JsonHelpRequestPersistenceTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"requests\":null}")]
    public async Task ReadAsync_InvalidEnvelope_ThrowsInsteadOfResettingData(string json)
    {
        var path = CreateTempFilePath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, json);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                JsonHelpRequestPersistence.ReadAsync(path, CancellationToken.None));
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task WriteAtomicallyAsync_ReplacesCompleteEnvelope()
    {
        var path = CreateTempFilePath();
        try
        {
            var database = new HelpRequestDatabase
            {
                Requests = [new HelpRequestRecord { Id = "help-1", RequestNo = "HELP001" }]
            };

            await JsonHelpRequestPersistence.WriteAtomicallyAsync(path, database);
            var restored = await JsonHelpRequestPersistence.ReadAsync(path, CancellationToken.None);

            Assert.Equal("help-1", Assert.Single(restored.Requests).Id);
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    private static string CreateTempFilePath() => Path.Combine(
        Path.GetTempPath(),
        $"aed-help-persistence-{Guid.NewGuid():N}",
        "help-requests.json");

    private static void DeleteTempDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
}
