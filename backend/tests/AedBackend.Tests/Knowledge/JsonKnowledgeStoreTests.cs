using System.Text.Json;
using AedBackend.Core.Models.Knowledge;
using AedBackend.Infrastructure.Knowledge;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Knowledge;

public sealed class JsonKnowledgeStoreTests
{
    /** JSON provider 已移除旧部署统计种子：缺文件时返回空横幅且不写盘。 */
    [Fact]
    public async Task GetSummaryAsync_ReturnsEmptySummary_WhenFileMissing()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            var summary = await store.GetSummaryAsync(CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Empty(summary.Items);
            Assert.Equal(string.Empty, summary.UpdatedAt);
            Assert.False(File.Exists(path));
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
