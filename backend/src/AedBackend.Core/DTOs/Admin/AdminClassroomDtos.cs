namespace AedBackend.Core.DTOs.Admin;

public sealed record KnowledgeStatItemDto(
    string Key,
    string Title,
    double Value,
    int SortOrder);

public sealed record UpdateKnowledgeSummaryRequest(
    List<KnowledgeStatItemDto> Items);

public sealed record AdminCourseListItem(
    string Id,
    string Title,
    string Thumbnail,
    string Type,
    string Category,
    bool Pinned,
    int SortOrder,
    bool IsEnabled,
    string CreatedAt);

public sealed record AdminCourseListResponse(
    IReadOnlyList<AdminCourseListItem> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record CreateCourseRequest(
    string Title,
    string Category,
    string? Thumbnail,
    string? Type,
    string? Content,
    string? VideoUrl,
    List<CourseVideoDto>? Videos,
    string? Disclaimer,
    bool Pinned,
    int SortOrder,
    bool IsEnabled,
    string? EstimatedTime,
    List<CourseStepDto>? Steps);

public sealed record UpdateCourseRequest(
    string? Title,
    string? Thumbnail,
    string? Type,
    string? Category,
    string? Content,
    string? VideoUrl,
    List<CourseVideoDto>? Videos,
    string? Disclaimer,
    bool? Pinned,
    int? SortOrder,
    bool? IsEnabled,
    string? EstimatedTime,
    List<CourseStepDto>? Steps);

public sealed record CourseVideoDto(
    int Order,
    string? Title,
    string Url);

public sealed record CourseStepDto(
    int Order,
    string Title,
    string Description,
    string Image,
    List<string> KeyPoints);

public sealed record AdminQuickActionListItem(
    string Id,
    string Title,
    string Icon,
    int SortOrder,
    bool IsEnabled);

public sealed record AdminQuickActionDetail(
    string Id,
    string Title,
    string Icon,
    string Summary,
    int SortOrder,
    bool IsEnabled,
    IReadOnlyList<QuickActionStepDto> Steps);

public sealed record CreateQuickActionRequest(
    string Title,
    string Icon,
    string? Summary,
    int SortOrder,
    bool IsEnabled,
    List<QuickActionStepDto>? Steps);

public sealed record UpdateQuickActionRequest(
    string? Title,
    string? Icon,
    string? Summary,
    int? SortOrder,
    bool? IsEnabled,
    List<QuickActionStepDto>? Steps);

public sealed record QuickActionStepDto(
    int Order,
    string Title,
    string Description,
    string Image,
    List<string> KeyPoints);
