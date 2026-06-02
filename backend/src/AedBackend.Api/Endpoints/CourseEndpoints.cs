using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Course;
using AedBackend.Core.Services.Course;

namespace AedBackend.Api.Endpoints;

public static class CourseEndpoints
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const string DefaultVideoTitle = "视频";

    public static IEndpointRouteBuilder MapCourseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/courses")
            .WithTags("Courses");

        group.MapGet("/", ListCoursesAsync).WithName("ListCourses");
        group.MapGet("/{id}", GetDetailAsync).WithName("GetCourseDetail");

        return app;
    }

    public static IEndpointRouteBuilder MapQuickActionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/quick-actions", ListQuickActionsAsync)
            .WithName("ListQuickActions")
            .WithTags("QuickActions");

        app.MapGet("/api/quick-actions/{id}", GetQuickActionAsync)
            .WithName("GetQuickActionDetail")
            .WithTags("QuickActions");

        return app;
    }

    private static async Task<IResult> ListCoursesAsync(
        int? page,
        int? pageSize,
        string? category,
        ICourseStore store,
        CancellationToken ct)
    {
        var courses = await store.GetAllAsync(ct);
        var filtered = courses
            .Where(c => c.IsEnabled)
            .Select(c => c);

        if (!string.IsNullOrWhiteSpace(category))
        {
            filtered = filtered.Where(c =>
                c.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        var items = filtered.ToArray();
        var pg = Math.Max(DefaultPage, page ?? DefaultPage);
        var ps = Math.Max(1, pageSize ?? DefaultPageSize);
        var paged = items.Skip((pg - 1) * ps).Take(ps).ToArray();

        var response = new CourseListPage(
            paged.Select(ToSummary).ToArray(),
            items.Length,
            pg,
            ps,
            Math.Max(1, (int)Math.Ceiling((double)items.Length / ps)));

        return Results.Ok(ApiResponse.Ok(response));
    }

    private static async Task<IResult> GetDetailAsync(
        string id,
        ICourseStore store,
        CancellationToken ct)
    {
        var course = await store.FindByIdAsync(id, ct);
        if (course is null || !course.IsEnabled)
        {
            return Results.NotFound(ApiResponse.Fail(ErrorCodes.CourseNotFound, "课程不存在"));
        }

        return Results.Ok(ApiResponse.Ok(ToDetail(course)));
    }

    private static async Task<IResult> ListQuickActionsAsync(
        IQuickActionStore store,
        CancellationToken ct)
    {
        var actions = await store.GetAllAsync(ct);
        var response = actions.Select(ToQuickActionResponse).ToArray();
        return Results.Ok(ApiResponse.Ok(response));
    }

    private static async Task<IResult> GetQuickActionAsync(
        string id,
        IQuickActionStore store,
        CancellationToken ct)
    {
        var action = await store.FindByIdAsync(id, ct);
        if (action is null || !action.IsEnabled)
        {
            return Results.NotFound(ApiResponse.Fail(ErrorCodes.CourseNotFound, "关键步骤不存在"));
        }

        return Results.Ok(ApiResponse.Ok(ToQuickActionResponse(action)));
    }

    private static CourseSummaryResponse ToSummary(Core.Models.Course.CourseRecord c)
    {
        return new CourseSummaryResponse(
            c.Id, c.Title, c.Thumbnail, c.Type, c.Category,
            c.CreatedAt, c.Pinned, c.SortOrder, c.CacheVersion, c.EstimatedTime);
    }

    private static CourseDetailResponse ToDetail(Core.Models.Course.CourseRecord c)
    {
        return new CourseDetailResponse(
            c.Id, c.Title, c.Thumbnail, c.Type, c.Category,
            c.CreatedAt, c.Pinned, c.SortOrder, c.CacheVersion, c.EstimatedTime,
            c.Content, ResolveVideoUrl(c), ResolveVideos(c), c.Disclaimer, c.ViewCount,
            c.Steps.Select(s => new CourseStepResponse(
                s.Order, s.Title, s.Description, s.Image,
                s.KeyPoints)).ToArray());
    }

    private static string? ResolveVideoUrl(Core.Models.Course.CourseRecord c)
    {
        return ResolveVideos(c).FirstOrDefault()?.Url ?? c.VideoUrl;
    }

    private static IReadOnlyList<CourseVideoResponse> ResolveVideos(Core.Models.Course.CourseRecord c)
    {
        var videos = c.Videos
            .Where(v => !string.IsNullOrWhiteSpace(v.Url))
            .OrderBy(v => v.Order)
            .Select((v, index) => new CourseVideoResponse(
                index + 1,
                ResolveVideoTitle(v.Title, index + 1),
                v.Url.Trim()))
            .ToArray();

        return videos.Length > 0 || string.IsNullOrWhiteSpace(c.VideoUrl)
            ? videos
            : [new CourseVideoResponse(1, ResolveVideoTitle(null, 1), c.VideoUrl.Trim())];
    }

    private static string ResolveVideoTitle(string? title, int order)
    {
        return string.IsNullOrWhiteSpace(title) ? $"{DefaultVideoTitle} {order}" : title.Trim();
    }

    private static QuickActionResponse ToQuickActionResponse(Core.Models.Course.QuickActionRecord a)
    {
        return new QuickActionResponse(
            a.Id, a.Title, a.Icon, a.Summary, a.SortOrder,
            a.Steps.Select(s => new QuickActionStepResponse(
                s.Order, s.Title, s.Description, s.Image,
                s.KeyPoints)).ToArray());
    }
}
