namespace AedBackend.Core.Models.Course;

public sealed record CourseRecord
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Thumbnail { get; set; } = string.Empty;
    public string Type { get; set; } = "article";
    public string Category { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
    public string Disclaimer { get; set; } = string.Empty;
    public bool Pinned { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string CacheVersion { get; set; } = string.Empty;
    public string? EstimatedTime { get; set; }
    public int ViewCount { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public List<CourseStepRecord> Steps { get; set; } = new();
}

public sealed record CourseStepRecord
{
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public List<string> KeyPoints { get; set; } = new();
}

public sealed record QuickActionRecord
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<QuickActionStepRecord> Steps { get; set; } = new();
}

public sealed record QuickActionStepRecord
{
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public List<string> KeyPoints { get; set; } = new();
}
