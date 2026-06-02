using AedBackend.Core.Models.Course;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal static partial class SqliteMapper
{
    public static CourseEntity ToEntity(CourseRecord item)
    {
        return new CourseEntity
        {
            Id = item.Id,
            Title = item.Title,
            Thumbnail = item.Thumbnail,
            Type = item.Type,
            Category = item.Category,
            Content = item.Content,
            VideoUrl = item.VideoUrl,
            Disclaimer = item.Disclaimer,
            Pinned = item.Pinned,
            SortOrder = item.SortOrder,
            IsEnabled = item.IsEnabled,
            CacheVersion = item.CacheVersion,
            EstimatedTime = item.EstimatedTime,
            ViewCount = item.ViewCount,
            CreatedAt = item.CreatedAt,
            Videos = item.Videos.Select(ToEntity).ToList(),
            Steps = item.Steps.Select(ToEntity).ToList()
        };
    }

    public static CourseRecord ToRecord(CourseEntity item)
    {
        return new CourseRecord
        {
            Id = item.Id,
            Title = item.Title,
            Thumbnail = item.Thumbnail,
            Type = item.Type,
            Category = item.Category,
            Content = item.Content,
            VideoUrl = item.VideoUrl,
            Disclaimer = item.Disclaimer,
            Pinned = item.Pinned,
            SortOrder = item.SortOrder,
            IsEnabled = item.IsEnabled,
            CacheVersion = item.CacheVersion,
            EstimatedTime = item.EstimatedTime,
            ViewCount = item.ViewCount,
            CreatedAt = item.CreatedAt,
            Videos = item.Videos.OrderBy(v => v.Order).Select(ToRecord).ToList(),
            Steps = item.Steps.OrderBy(s => s.Order).Select(ToRecord).ToList()
        };
    }

    public static QuickActionEntity ToEntity(QuickActionRecord item)
    {
        return new QuickActionEntity
        {
            Id = item.Id,
            Title = item.Title,
            Icon = item.Icon,
            Summary = item.Summary,
            SortOrder = item.SortOrder,
            IsEnabled = item.IsEnabled,
            Steps = item.Steps.Select(ToEntity).ToList()
        };
    }

    public static QuickActionRecord ToRecord(QuickActionEntity item)
    {
        return new QuickActionRecord
        {
            Id = item.Id,
            Title = item.Title,
            Icon = item.Icon,
            Summary = item.Summary,
            SortOrder = item.SortOrder,
            IsEnabled = item.IsEnabled,
            Steps = item.Steps.OrderBy(s => s.Order).Select(ToRecord).ToList()
        };
    }

    private static CourseVideoEntity ToEntity(CourseVideoRecord item)
    {
        return new CourseVideoEntity { Order = item.Order, Title = item.Title, Url = item.Url };
    }

    private static CourseVideoRecord ToRecord(CourseVideoEntity item)
    {
        return new CourseVideoRecord { Order = item.Order, Title = item.Title, Url = item.Url };
    }

    private static CourseStepEntity ToEntity(CourseStepRecord item)
    {
        return new CourseStepEntity
        {
            Order = item.Order,
            Title = item.Title,
            Description = item.Description,
            Image = item.Image,
            KeyPoints = item.KeyPoints.Select((text, index) =>
                new CourseStepKeyPointEntity { Order = index + 1, Text = text }).ToList()
        };
    }

    private static CourseStepRecord ToRecord(CourseStepEntity item)
    {
        return new CourseStepRecord
        {
            Order = item.Order,
            Title = item.Title,
            Description = item.Description,
            Image = item.Image,
            KeyPoints = item.KeyPoints.OrderBy(k => k.Order).Select(k => k.Text).ToList()
        };
    }

    private static QuickActionStepEntity ToEntity(QuickActionStepRecord item)
    {
        return new QuickActionStepEntity
        {
            Order = item.Order,
            Title = item.Title,
            Description = item.Description,
            Image = item.Image,
            KeyPoints = item.KeyPoints.Select((text, index) =>
                new QuickActionStepKeyPointEntity { Order = index + 1, Text = text }).ToList()
        };
    }

    private static QuickActionStepRecord ToRecord(QuickActionStepEntity item)
    {
        return new QuickActionStepRecord
        {
            Order = item.Order,
            Title = item.Title,
            Description = item.Description,
            Image = item.Image,
            KeyPoints = item.KeyPoints.OrderBy(k => k.Order).Select(k => k.Text).ToList()
        };
    }
}
