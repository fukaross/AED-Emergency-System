using System.Text.Json;
using AedBackend.Core.Models.Knowledge;
using AedBackend.Infrastructure.Knowledge;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Knowledge;

public sealed class JsonKnowledgeStoreTests
{
    [Fact]
    public async Task GetSummaryAsync_ReturnsSeedData_WhenFileMissing()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            var summary = await store.GetSummaryAsync(CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(4, summary.Items.Count);
            Assert.Contains(summary.Items, i => i.Key == "aed_count" && Math.Abs(i.Value - 24) < 0.01);
            Assert.Contains(summary.Items, i => i.Key == "trained_people" && Math.Abs(i.Value - 6392) < 0.01);
            Assert.Contains(summary.Items, i => i.Key == "certified_people" && Math.Abs(i.Value - 2592) < 0.01);
            Assert.Contains(summary.Items, i => i.Key == "coverage_rate" && Math.Abs(i.Value - 52.6) < 0.01);
            Assert.True(File.Exists(path));
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsSortedBySortOrder()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            await store.SaveSummaryAsync(new KnowledgeSummary
            {
                Items = new List<KnowledgeStatItem>
                {
                    new() { Key = "z", Title = "Z", Value = 3, SortOrder = 3 },
                    new() { Key = "a", Title = "A", Value = 1, SortOrder = 1 },
                    new() { Key = "b", Title = "B", Value = 2, SortOrder = 2 },
                }
            }, CancellationToken.None);

            var summary = await store.GetSummaryAsync(CancellationToken.None);

            Assert.Equal(3, summary.Items.Count);
            Assert.Equal("A", summary.Items[0].Title);
            Assert.Equal("B", summary.Items[1].Title);
            Assert.Equal("Z", summary.Items[2].Title);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task SaveSummaryAsync_PersistsAndUpdatesTimestamp()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            await store.SaveSummaryAsync(new KnowledgeSummary
            {
                Items = new List<KnowledgeStatItem>
                {
                    new() { Key = "test", Title = "Test", Value = 100, SortOrder = 1 }
                }
            }, CancellationToken.None);

            var summary = await store.GetSummaryAsync(CancellationToken.None);
            Assert.Single(summary.Items);
            Assert.Equal("Test", summary.Items[0].Title);
            Assert.NotNull(summary.UpdatedAt);
            Assert.NotEmpty(summary.UpdatedAt);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    private static JsonKnowledgeStore CreateStore(string path)
    {
        return new JsonKnowledgeStore(Options.Create(new WeChatOptions
        {
            KnowledgeSummaryFilePath = path
        }));
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"knowledge-{Guid.NewGuid():N}",
            "knowledge-summary.json");
    }

    private static void DeleteTempDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
}
