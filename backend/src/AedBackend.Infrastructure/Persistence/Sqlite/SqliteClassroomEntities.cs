namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class CourseEntity
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Thumbnail { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
    public string Disclaimer { get; set; } = string.Empty;
    public bool Pinned { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; }
    public string CacheVersion { get; set; } = string.Empty;
    public string? EstimatedTime { get; set; }
    public int ViewCount { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public List<CourseVideoEntity> Videos { get; set; } = new();
    public List<CourseStepEntity> Steps { get; set; } = new();
}

internal sealed class CourseVideoEntity
{
    public int Id { get; set; }
    public string CourseId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

internal sealed class CourseStepEntity
{
    public int Id { get; set; }
    public string CourseId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public List<CourseStepKeyPointEntity> KeyPoints { get; set; } = new();
}

internal sealed class CourseStepKeyPointEntity
{
    public int Id { get; set; }
    public int CourseStepId { get; set; }
    public int Order { get; set; }
    public string Text { get; set; } = string.Empty;
}

internal sealed class QuickActionEntity
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; }
    public List<QuickActionStepEntity> Steps { get; set; } = new();
}

internal sealed class QuickActionStepEntity
{
    public int Id { get; set; }
    public string QuickActionId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public List<QuickActionStepKeyPointEntity> KeyPoints { get; set; } = new();
}

internal sealed class QuickActionStepKeyPointEntity
{
    public int Id { get; set; }
    public int QuickActionStepId { get; set; }
    public int Order { get; set; }
    public string Text { get; set; } = string.Empty;
}
