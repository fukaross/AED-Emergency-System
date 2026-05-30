using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Course;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Course;

namespace AedBackend.Api.Endpoints;

internal static class AdminQuickActionEndpoints
{
    public static RouteGroupBuilder MapAdminQuickActionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/quick-actions", ListAsync).WithName("AdminListQuickActions");
        group.MapGet("/quick-actions/{id}", GetAsync).WithName("AdminGetQuickAction");
        group.MapPost("/quick-actions", CreateAsync).WithName("AdminCreateQuickAction");
        group.MapPut("/quick-actions/{id}", UpdateAsync).WithName("AdminUpdateQuickAction");
        group.MapDelete("/quick-actions/{id}", DeleteAsync).WithName("AdminDeleteQuickAction");
        return group;
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        IQuickActionStore store,
        IAdminAuthorizationService authorization,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var all = await store.GetAllAdminAsync(ct);
        var items = all.Select(ToListItem).ToArray();
        return Results.Ok(ApiResponse.Ok(items));
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        string id,
        IQuickActionStore store,
        IAdminAuthorizationService authorization,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var action = await store.FindByIdAsync(id, ct);
        return action is null
            ? EndpointFailures.JsonFailure(ErrorCodes.CourseNotFound, "关键步骤不存在", 404)
            : Results.Ok(ApiResponse.Ok(ToDetail(action)));
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        CreateQuickActionRequest body,
        IQuickActionStore store,
        IAdminAuthorizationService authorization,
        IAdminAuditLogStore auditLogStore,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var validation = ValidateCreate(body);
        if (validation is not null) return validation;

        var action = new QuickActionRecord
        {
            Title = body.Title.Trim(),
            Icon = body.Icon.Trim(),
            Summary = body.Summary ?? string.Empty,
            SortOrder = body.SortOrder,
            IsEnabled = body.IsEnabled,
            Steps = MapSteps(body.Steps)
        };

        var created = await store.AddAsync(action, ct);

        await auditLogStore.AppendAsync(AdminEndpointHelpers.CreateAudit(
            "QuickAction", "quick-action.create", auth.Principal, created.Id,
            $"新建关键步骤：{created.Title}", context), ct);

        return Results.Ok(ApiResponse.Ok(ToDetail(created)));
    }

    private static async Task<IResult> UpdateAsync(
        HttpContext context,
        string id,
        UpdateQuickActionRequest body,
        IQuickActionStore store,
        IAdminAuthorizationService authorization,
        IAdminAuditLogStore auditLogStore,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var existing = await store.FindByIdAsync(id, ct);
        if (existing is null)
            return EndpointFailures.JsonFailure(ErrorCodes.CourseNotFound, "关键步骤不存在", 404);

        var validation = ValidateUpdate(body);
        if (validation is not null) return validation;

        ApplyUpdate(existing, body);

        var updated = await store.UpdateAsync(id, existing, ct);
        if (updated is null)
            return EndpointFailures.JsonFailure(ErrorCodes.CourseNotFound, "关键步骤不存在", 404);

        await auditLogStore.AppendAsync(AdminEndpointHelpers.CreateAudit(
            "QuickAction", "quick-action.update", auth.Principal, updated.Id,
            $"更新关键步骤：{updated.Title}", context), ct);

        return Results.Ok(ApiResponse.Ok(ToDetail(updated)));
    }

    private static async Task<IResult> DeleteAsync(
        HttpContext context,
        string id,
        IQuickActionStore store,
        IAdminAuthorizationService authorization,
        IAdminAuditLogStore auditLogStore,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var existing = await store.FindByIdAsync(id, ct);
        var deleted = await store.DeleteAsync(id, ct);
        if (!deleted)
            return EndpointFailures.JsonFailure(ErrorCodes.CourseNotFound, "关键步骤不存在", 404);

        await auditLogStore.AppendAsync(AdminEndpointHelpers.CreateAudit(
            "QuickAction", "quick-action.delete", auth.Principal, id,
            $"删除关键步骤：{existing?.Title ?? id}", context), ct);

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

    private static IResult? ValidateCreate(CreateQuickActionRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Title))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "关键步骤标题不能为空", 400);
        if (string.IsNullOrWhiteSpace(body.Icon))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "关键步骤图标不能为空", 400);
        return null;
    }

    private static IResult? ValidateUpdate(UpdateQuickActionRequest body)
    {
        if (body.Title is not null && string.IsNullOrWhiteSpace(body.Title))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "关键步骤标题不能为空", 400);
        if (body.Icon is not null && string.IsNullOrWhiteSpace(body.Icon))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "关键步骤图标不能为空", 400);
        return null;
    }

    private static void ApplyUpdate(QuickActionRecord target, UpdateQuickActionRequest source)
    {
        if (source.Title is not null) target.Title = source.Title.Trim();
        if (source.Icon is not null) target.Icon = source.Icon.Trim();
        if (source.Summary is not null) target.Summary = source.Summary;
        if (source.SortOrder.HasValue) target.SortOrder = source.SortOrder.Value;
        if (source.IsEnabled.HasValue) target.IsEnabled = source.IsEnabled.Value;
        if (source.Steps is not null) target.Steps = MapSteps(source.Steps);
    }

    private static AdminQuickActionListItem ToListItem(QuickActionRecord a)
    {
        return new AdminQuickActionListItem(a.Id, a.Title, a.Icon, a.SortOrder, a.IsEnabled);
    }

    private static AdminQuickActionDetail ToDetail(QuickActionRecord a)
    {
        return new AdminQuickActionDetail(
            a.Id, a.Title, a.Icon, a.Summary, a.SortOrder, a.IsEnabled,
            a.Steps.Select(s => new QuickActionStepDto(
                s.Order, s.Title, s.Description, s.Image,
                s.KeyPoints)).ToArray());
    }

    private static List<QuickActionStepRecord> MapSteps(List<QuickActionStepDto>? dtos)
    {
        return dtos?.Select(s => new QuickActionStepRecord
        {
            Order = s.Order,
            Title = s.Title,
            Description = s.Description,
            Image = s.Image,
            KeyPoints = s.KeyPoints ?? new List<string>()
        }).ToList() ?? new List<QuickActionStepRecord>();
    }
}
