namespace AedBackend.Core.Models.Knowledge;

public sealed record KnowledgeStatItem
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double Value { get; set; }
    public int SortOrder { get; set; }
}

public sealed record KnowledgeSummary
{
    public List<KnowledgeStatItem> Items { get; set; } = new();
    public string UpdatedAt { get; set; } = string.Empty;
}
