namespace AedBackend.Core.DTOs.Course;

public sealed record CourseStepResponse(
    int Order,
    string Title,
    string Description,
    string Image,
    IReadOnlyList<string> KeyPoints);

public sealed record CourseSummaryResponse(
    string Id,
    string Title,
    string Thumbnail,
    string Type,
    string Category,
    string CreatedAt,
    bool Pinned,
    int SortOrder,
    string CacheVersion,
    string? EstimatedTime);

public sealed record CourseDetailResponse(
    string Id,
    string Title,
    string Thumbnail,
    string Type,
    string Category,
    string CreatedAt,
    bool Pinned,
    int SortOrder,
    string CacheVersion,
    string? EstimatedTime,
    string Content,
    string? VideoUrl,
    string Disclaimer,
    int ViewCount,
    IReadOnlyList<CourseStepResponse> Steps);

public sealed record CourseListPage(
    IReadOnlyList<CourseSummaryResponse> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record QuickActionStepResponse(
    int Order,
    string Title,
    string Description,
    string Image,
    IReadOnlyList<string> KeyPoints);

public sealed record QuickActionResponse(
    string Id,
    string Title,
    string Icon,
    string Summary,
    int SortOrder,
    IReadOnlyList<QuickActionStepResponse> Steps);
