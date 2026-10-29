using System.Text.Json;
using AedBackend.Core.Models.Knowledge;
using AedBackend.Core.Services.Knowledge;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Knowledge;

public sealed class JsonKnowledgeStore : IKnowledgeStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonKnowledgeStore(IOptions<WeChatOptions> options)
    {
        filePath = Path.GetFullPath(options.Value.KnowledgeSummaryFilePath);
    }

    public async Task<KnowledgeSummary> GetSummaryAsync(CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            if (!File.Exists(filePath))
            {
                var seed = DefaultSummary();
                await WriteUnsafeAsync(seed, ct);
                return WithSortedItems(seed);
            }

            var summary = await ReadUnsafeAsync(ct);
            return WithSortedItems(summary);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task SaveSummaryAsync(KnowledgeSummary summary, CancellationToken ct)
    {
        summary.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
        await fileLock.WaitAsync(ct);
        try
        {
            await WriteUnsafeAsync(summary, ct);
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<KnowledgeSummary> ReadUnsafeAsync(CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<KnowledgeSummary>(stream, JsonOptions, ct)
               ?? DefaultSummary();
    }

    private async Task WriteUnsafeAsync(KnowledgeSummary summary, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, summary, JsonOptions, ct);
    }

    private static KnowledgeSummary WithSortedItems(KnowledgeSummary summary)
    {
        summary.Items = summary.Items.OrderBy(i => i.SortOrder).ToList();
        return summary;
    }

    internal static KnowledgeSummary DefaultSummary()
    {
        return new KnowledgeSummary
        {
            UpdatedAt = "2026-05-15T00:00:00+08:00",
            Items = new List<KnowledgeStatItem>
            {
                new() { Key = "aed_count", Title = "AED数量", Value = 24, SortOrder = 1 },
                new() { Key = "trained_people", Title = "培训人数", Value = 6392, SortOrder = 2 },
                new() { Key = "certified_people", Title = "取证人数", Value = 2592, SortOrder = 3 },
                new() { Key = "coverage_rate", Title = "覆盖率(%)", Value = 52.6, SortOrder = 4 },
            }
        };
    }
}
