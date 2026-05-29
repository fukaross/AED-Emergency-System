namespace AedBackend.Core.DTOs.Knowledge;

public sealed record KnowledgeStatItemResponse(
    string Key,
    string Title,
    double Value,
    int SortOrder);

public sealed record KnowledgeSummaryResponse(
    IReadOnlyList<KnowledgeStatItemResponse> Items,
    string UpdatedAt);
