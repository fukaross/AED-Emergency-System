using AedBackend.Core.Models.Knowledge;

namespace AedBackend.Core.Services.Knowledge;

public interface IKnowledgeStore
{
    Task<KnowledgeSummary> GetSummaryAsync(CancellationToken ct);
    Task SaveSummaryAsync(KnowledgeSummary summary, CancellationToken ct);
}
