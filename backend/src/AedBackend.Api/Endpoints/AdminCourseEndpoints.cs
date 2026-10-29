using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Course;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Course;

namespace AedBackend.Api.Endpoints;

internal static class AdminCourseEndpoints
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;

    public static RouteGroupBuilder MapAdminCourseEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/courses", ListCoursesAsync).WithName("AdminListCourses");
        group.MapGet("/courses/{id}", GetCourseAsync).WithName("AdminGetCourse");
        group.MapPost("/courses", CreateCourseAsync).WithName("AdminCreateCourse");
        group.MapPut("/courses/{id}", UpdateCourseAsync).WithName("AdminUpdateCourse");
        group.MapDelete("/courses/{id}", DeleteCourseAsync).WithName("AdminDeleteCourse");
        return group;
    }

    private static async Task<IResult> ListCoursesAsync(
        HttpContext context,
        int? page,
        int? pageSize,
        string? category,
        string? keyword,
        ICourseStore store,
        IAdminAuthorizationService authorization,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var courses = await store.GetAllAsync(ct);
        var filtered = ApplyCourseFilters(courses, category, keyword);

        var safePage = Math.Max(DefaultPage, page ?? DefaultPage);
        var safePageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, 100);
        var paged = filtered.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToArray();

        return Results.Ok(ApiResponse.Ok(new AdminCourseListResponse(
            paged.Select(ToListItem).ToArray(),
            filtered.Count,
            safePage,
            safePageSize,
            Math.Max(1, (int)Math.Ceiling((double)filtered.Count / safePageSize)))));
    }

    private static async Task<IResult> GetCourseAsync(
        HttpContext context,
        string id,
        ICourseStore store,
        IAdminAuthorizationService authorization,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var course = await store.FindByIdAsync(id, ct);
        return course is null
            ? EndpointFailures.JsonFailure(ErrorCodes.CourseNotFound, "课程不存在", 404)
            : Results.Ok(ApiResponse.Ok(ToDetail(course)));
    }

    private static async Task<IResult> CreateCourseAsync(
        HttpContext context,
        CreateCourseRequest body,
        ICourseStore store,
        IAdminAuthorizationService authorization,
        IAdminAuditLogStore auditLogStore,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var validation = ValidateCreate(body);
        if (validation is not null) return validation;

        var course = new CourseRecord
        {
            Title = body.Title.Trim(),
            Category = body.Category.Trim(),
            Thumbnail = body.Thumbnail?.Trim() ?? string.Empty,
            Type = string.IsNullOrWhiteSpace(body.Type) ? "article" : body.Type,
            Content = body.Content ?? string.Empty,
            VideoUrl = body.VideoUrl?.Trim(),
            Videos = AdminCourseVideoMapper.MapVideos(body.Videos, body.VideoUrl),
            Disclaimer = body.Disclaimer ?? string.Empty,
            Pinned = body.Pinned,
            SortOrder = body.SortOrder,
            IsEnabled = body.IsEnabled,
            EstimatedTime = body.EstimatedTime?.Trim(),
            Steps = MapSteps(body.Steps)
        };
        AdminCourseVideoMapper.NormalizeCourseVideos(course);
        var courseValidation = AdminCourseVideoMapper.ValidateCourseVideos(course);
        if (courseValidation is not null) return courseValidation;

        var created = await store.AddAsync(course, ct);

        await auditLogStore.AppendAsync(AdminEndpointHelpers.CreateAudit(
            "Course", "course.create", auth.Principal, created.Id,
            $"新建课程：{created.Title}", context), ct);

        return Results.Ok(ApiResponse.Ok(created));
    }

    private static async Task<IResult> UpdateCourseAsync(
        HttpContext context,
        string id,
        UpdateCourseRequest body,
        ICourseStore store,
        IAdminAuthorizationService authorization,
        IAdminAuditLogStore auditLogStore,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var existing = await store.FindByIdAsync(id, ct);
        if (existing is null)
            return EndpointFailures.JsonFailure(ErrorCodes.CourseNotFound, "课程不存在", 404);

        var validation = ValidateUpdate(body);
        if (validation is not null) return validation;

        ApplyUpdate(existing, body);
        AdminCourseVideoMapper.NormalizeCourseVideos(existing);
        var courseValidation = AdminCourseVideoMapper.ValidateCourseVideos(existing);
        if (courseValidation is not null) return courseValidation;

        var updated = await store.UpdateAsync(id, existing, ct);
        if (updated is null)
            return EndpointFailures.JsonFailure(ErrorCodes.CourseNotFound, "课程不存在", 404);

        await auditLogStore.AppendAsync(AdminEndpointHelpers.CreateAudit(
            "Course", "course.update", auth.Principal, updated.Id,
            $"更新课程：{updated.Title}", context), ct);

        return Results.Ok(ApiResponse.Ok(updated));
    }

    private static async Task<IResult> DeleteCourseAsync(
        HttpContext context,
        string id,
        ICourseStore store,
        IAdminAuthorizationService authorization,
        IAdminAuditLogStore auditLogStore,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var existing = await store.FindByIdAsync(id, ct);
        var deleted = await store.DeleteAsync(id, ct);
        if (!deleted)
            return EndpointFailures.JsonFailure(ErrorCodes.CourseNotFound, "课程不存在", 404);

        await auditLogStore.AppendAsync(AdminEndpointHelpers.CreateAudit(
            "Course", "course.delete", auth.Principal, id,
            $"删除课程：{existing?.Title ?? id}", context), ct);

        return Results.Ok(ApiResponse.Ok());
    }

    private static Task<AdminAuthorizationResult> RequireAdminAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        CancellationToken ct)
    {
        return authorization.RequireAdminSessionAsync(
            AdminEndpointHelpers.ReadAdminKey(context.Request),
            AdminEndpointHelpers.GetClientIp(context),
            AdminEndpointHelpers.GetUserAgent(context.Request),
            ct);
    }

    private static IResult? ValidateCreate(CreateCourseRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Title))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "课程标题不能为空", 400);
        if (string.IsNullOrWhiteSpace(body.Category))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "课程分类不能为空", 400);
        return null;
    }

    private static IResult? ValidateUpdate(UpdateCourseRequest body)
    {
        if (body.Title is not null && string.IsNullOrWhiteSpace(body.Title))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "课程标题不能为空", 400);
        if (body.Category is not null && string.IsNullOrWhiteSpace(body.Category))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "课程分类不能为空", 400);
        return null;
    }

    private static void ApplyUpdate(CourseRecord target, UpdateCourseRequest source)
    {
        if (source.Title is not null) target.Title = source.Title.Trim();
        if (source.Thumbnail is not null) target.Thumbnail = source.Thumbnail.Trim();
        if (source.Type is not null) target.Type = source.Type;
        if (source.Category is not null) target.Category = source.Category.Trim();
        if (source.Content is not null) target.Content = source.Content;
        if (source.VideoUrl is not null) target.VideoUrl = source.VideoUrl.Trim();
        if (source.Videos is not null) target.Videos = AdminCourseVideoMapper.MapVideos(source.Videos, null);
        if (source.Disclaimer is not null) target.Disclaimer = source.Disclaimer;
        if (source.Pinned.HasValue) target.Pinned = source.Pinned.Value;
        if (source.SortOrder.HasValue) target.SortOrder = source.SortOrder.Value;
        if (source.IsEnabled.HasValue) target.IsEnabled = source.IsEnabled.Value;
        if (source.EstimatedTime is not null) target.EstimatedTime = source.EstimatedTime.Trim();
        if (source.Steps is not null) target.Steps = MapSteps(source.Steps);
    }

    private static List<CourseRecord> ApplyCourseFilters(
        IReadOnlyList<CourseRecord> courses,
        string? category,
        string? keyword)
    {
        var query = courses.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(c => c.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(c => c.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        return query.ToList();
    }

    private static AdminCourseListItem ToListItem(CourseRecord c)
    {
        return new AdminCourseListItem(
            c.Id, c.Title, c.Thumbnail, c.Type, c.Category,
            c.Pinned, c.SortOrder, c.IsEnabled, c.CreatedAt);
    }

    private static CourseRecord ToDetail(CourseRecord c)
    {
        return c with
        {
            Videos = AdminCourseVideoMapper.NormalizeVideos(c.Videos, c.VideoUrl),
            VideoUrl = AdminCourseVideoMapper.NormalizeVideos(c.Videos, c.VideoUrl).FirstOrDefault()?.Url
        };
    }

    private static List<CourseStepRecord> MapSteps(List<CourseStepDto>? dtos)
    {
        return dtos?.Select(s => new CourseStepRecord
        {
            Order = s.Order,
            Title = s.Title,
            Description = s.Description,
            Image = s.Image,
            KeyPoints = s.KeyPoints ?? new List<string>()
        }).ToList() ?? new List<CourseStepRecord>();
    }
}
