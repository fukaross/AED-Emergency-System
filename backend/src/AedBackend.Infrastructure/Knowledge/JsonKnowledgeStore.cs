using System.Text.Json;
using AedBackend.Core.Models.Knowledge;
using AedBackend.Core.Services.Knowledge;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Knowledge;

/**
 * JSON provider 仅作为只读/回滚数据源：统计横幅来自既有 JSON 文件，不再内置旧部署的统计数字。
 * 文件缺失时返回空横幅且不写盘；演示数值由 demo/data/knowledge-summary.json 导入 SQLite。
 */
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
        if (!File.Exists(filePath)) return new KnowledgeSummary();

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<KnowledgeSummary>(stream, JsonOptions, ct)
               ?? new KnowledgeSummary();
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
}
