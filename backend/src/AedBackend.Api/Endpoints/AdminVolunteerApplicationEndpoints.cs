using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Volunteer;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Volunteer;
using AedBackend.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

internal static class AdminVolunteerApplicationEndpoints
{
    private static readonly IReadOnlySet<string> ValidStatuses =
        new HashSet<string> { "all", "submitted", "approved", "rejected" };
    private static readonly IReadOnlySet<string> ValidCampuses =
        new HashSet<string> { "guilinyang", "fucheng" };

    public static RouteGroupBuilder MapAdminVolunteerApplicationEndpoints(
        this RouteGroupBuilder group)
    {
        group.MapGet("/volunteer-applications", ListApplicationsAsync)
            .WithName("AdminListVolunteerApplications");
        group.MapGet("/volunteer-applications/{id:int}", GetApplicationAsync)
            .WithName("AdminGetVolunteerApplication");
        group.MapPut("/volunteer-applications/{id:int}/approve", ApproveApplicationAsync)
            .WithName("AdminApproveVolunteerApplication");
        group.MapPut("/volunteer-applications/{id:int}/reject", RejectApplicationAsync)
            .WithName("AdminRejectVolunteerApplication");
        return group;
    }

    private static async Task<IResult> ListApplicationsAsync(
        HttpContext context,
        int? page,
        int? pageSize,
        string? status,
        string? keyword,
        IVolunteerApplicationStore applications,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var query = CreateQuery(page, pageSize, status, keyword);
        if (query.Error is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, query.Error, 400);

        var result = await applications.ListAllAsync(query.Query, ct);
        return Results.Ok(ApiResponse.Ok(new AdminVolunteerApplicationListResponse(
            result.Items.Select(ToListItem).ToArray(),
            result.Total,
            result.Page,
            result.PageSize,
            result.TotalPages)));
    }

    private static async Task<IResult> GetApplicationAsync(
        HttpContext context,
        int id,
        IVolunteerApplicationStore applications,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var item = await applications.GetByIdAsync(id, ct);
        return item is null
            ? EndpointFailures.JsonFailure(ErrorCodes.VolunteerApplicationNotFound, "志愿者申请不存在", 404)
            : Results.Ok(ApiResponse.Ok(ToDetail(item)));
    }

    private static async Task<IResult> ApproveApplicationAsync(
        HttpContext context,
        int id,
        ApproveVolunteerApplicationRequest body,
        IVolunteerApplicationReviewService reviewer,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var validation = ValidateApprove(body);
        if (validation is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validation, 400);

        try
        {
            var result = await reviewer.ApproveAsync(id, new VolunteerApplicationApproveCommand(
                auth.Principal!, body.Campus!.Trim(), body.Lat!.Value, body.Lng!.Value,
                TrimOptional(body.ReviewNote), AdminEndpointHelpers.GetClientIp(context),
                AdminEndpointHelpers.GetUserAgent(context.Request)), ct);
            return Results.Ok(ApiResponse.Ok(ToReviewResponse(result)));
        }
        catch (VolunteerApplicationReviewException ex)
        {
            return EndpointFailures.JsonFailure(ex.ErrorCode, ex.Message, ex.StatusCode);
        }
    }

    private static async Task<IResult> RejectApplicationAsync(
        HttpContext context,
        int id,
        RejectVolunteerApplicationRequest body,
        IVolunteerApplicationReviewService reviewer,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var note = body.ReviewNote?.Trim();
        if (string.IsNullOrWhiteSpace(note) || note.Length > 200)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "审核意见必须为 1-200 字", 400);

        try
        {
            var result = await reviewer.RejectAsync(id, new VolunteerApplicationRejectCommand(
                auth.Principal!, note, AdminEndpointHelpers.GetClientIp(context),
                AdminEndpointHelpers.GetUserAgent(context.Request)), ct);
            return Results.Ok(ApiResponse.Ok(ToReviewResponse(result)));
        }
        catch (VolunteerApplicationReviewException ex)
        {
            return EndpointFailures.JsonFailure(ex.ErrorCode, ex.Message, ex.StatusCode);
        }
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

    private static (AdminVolunteerApplicationQuery Query, string? Error) CreateQuery(
        int? page,
        int? pageSize,
        string? status,
        string? keyword)
    {
        var safePage = page ?? 1;
        var safePageSize = pageSize ?? 20;
        if (safePage < 1) return (new AdminVolunteerApplicationQuery(), "page 必须大于等于 1");
        if (safePageSize is < 1 or > 100)
            return (new AdminVolunteerApplicationQuery(), "pageSize 必须在 1 到 100 之间");
        if (!string.IsNullOrWhiteSpace(status) && !ValidStatuses.Contains(status))
            return (new AdminVolunteerApplicationQuery(), $"状态不支持: {status}");
        return (new AdminVolunteerApplicationQuery(safePage, safePageSize, status, keyword), null);
    }

    private static string? ValidateApprove(ApproveVolunteerApplicationRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.Campus) || !ValidCampuses.Contains(body.Campus))
            return $"校区不支持: {body.Campus}";
        if (body.Lat is null or < -90 or > 90) return "lat 不合法";
        if (body.Lng is null or < -180 or > 180) return "lng 不合法";
        return body.ReviewNote is { Length: > 200 } ? "审核意见不能超过 200 字" : null;
    }

    private static AdminVolunteerApplicationListItem ToListItem(VolunteerApplicationRecord item)
    {
        return new AdminVolunteerApplicationListItem(
            item.Id.ToString(),
            item.OpenId,
            item.Payload.Name,
            MaskPhone(item.Payload.Phone),
            item.Status,
            item.AttemptNumber,
            item.DerivedFromId,
            item.CreatedRescuerId,
            item.CreatedAt,
            item.UpdatedAt,
            item.ReviewedAt);
    }

    private static AdminVolunteerApplicationDetail ToDetail(VolunteerApplicationRecord item)
    {
        return new AdminVolunteerApplicationDetail(
            item.Id.ToString(),
            item.OpenId,
            item.Status,
            item.AttemptNumber,
            item.DerivedFromId,
            item.Payload,
            item.ReviewNote,
            item.ReviewedBy,
            item.ReviewedAt,
            item.CreatedRescuerId,
            item.CreatedAt,
            item.UpdatedAt);
    }

    private static AdminVolunteerApplicationReviewResponse ToReviewResponse(
        VolunteerApplicationReviewResult result)
    {
        return new AdminVolunteerApplicationReviewResponse(
            ToDetail(result.Application),
            result.CreatedResponder?.Id);
    }

    private static string? TrimOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string MaskPhone(string phone)
    {
        return string.IsNullOrWhiteSpace(phone) || phone.Length < 7
            ? phone
            : $"{phone[..3]}****{phone[^4..]}";
    }

    private static IResult FeatureRequiresSqlite()
    {
        return EndpointFailures.JsonFailure(
            ErrorCodes.FeatureRequiresSqlite,
            "Phase 7 志愿者审核接口仅支持 SQLite 数据源",
            501);
    }
}
