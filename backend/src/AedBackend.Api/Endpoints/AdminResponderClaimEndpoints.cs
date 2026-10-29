using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.User;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.User;
using AedBackend.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

internal static class AdminResponderClaimEndpoints
{
    private static readonly IReadOnlySet<string> ValidStatuses =
        new HashSet<string> { "all", "submitted", "approved", "rejected" };

    public static RouteGroupBuilder MapAdminResponderClaimEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/responder-claims", ListClaimsAsync).WithName("AdminListResponderClaims");
        group.MapGet("/responder-claims/{id:int}", GetClaimAsync).WithName("AdminGetResponderClaim");
        group.MapPut("/responder-claims/{id:int}/approve", ApproveClaimAsync).WithName("AdminApproveResponderClaim");
        group.MapPut("/responder-claims/{id:int}/reject", RejectClaimAsync).WithName("AdminRejectResponderClaim");
        return group;
    }

    private static async Task<IResult> ListClaimsAsync(
        HttpContext context,
        int? page,
        int? pageSize,
        string? status,
        string? keyword,
        IResponderClaimApplicationStore applications,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        var auth = await AuthorizeAsync(context, authorization, dataOptions, ct);
        if (!auth.Succeeded) return auth.Result!;
        var query = CreateQuery(page, pageSize, status, keyword);
        if (query.Error is not null) return BadRequest(query.Error);

        var result = await applications.ListAllAsync(query.Query, ct);
        return Results.Ok(ApiResponse.Ok(new AdminResponderClaimListResponse(
            result.Items.Select(ToListItem).ToArray(),
            result.Total,
            result.Page,
            result.PageSize,
            result.TotalPages)));
    }

    private static async Task<IResult> GetClaimAsync(
        HttpContext context,
        int id,
        IResponderClaimApplicationStore applications,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        var auth = await AuthorizeAsync(context, authorization, dataOptions, ct);
        if (!auth.Succeeded) return auth.Result!;
        var item = await applications.GetByIdAsync(id, ct);
        return item is null
            ? EndpointFailures.JsonFailure(ErrorCodes.ResponderClaimNotFound, "急救员认领申请不存在", 404)
            : Results.Ok(ApiResponse.Ok(ToDetail(item)));
    }

    private static async Task<IResult> ApproveClaimAsync(
        HttpContext context,
        int id,
        ApproveResponderClaimRequest body,
        IUserIdentityReviewService reviewer,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        var auth = await AuthorizeAsync(context, authorization, dataOptions, ct);
        if (!auth.Succeeded) return auth.Result!;
        if (string.IsNullOrWhiteSpace(body.ResponderProfileId))
            return BadRequest("必须选择急救员档案");
        if (body.ReviewNote is { Length: > 200 })
            return BadRequest("审核意见不能超过 200 字");

        try
        {
            var result = await reviewer.ApproveResponderClaimAsync(id, new ResponderClaimApproveCommand(
                auth.Principal!, body.ResponderProfileId.Trim(), TrimOptional(body.ReviewNote),
                AdminEndpointHelpers.GetClientIp(context),
                AdminEndpointHelpers.GetUserAgent(context.Request)), ct);
            return Results.Ok(ApiResponse.Ok(new AdminResponderClaimReviewResponse(ToDetail(result))));
        }
        catch (UserIdentityReviewException ex)
        {
            return EndpointFailures.JsonFailure(ex.ErrorCode, ex.Message, ex.StatusCode);
        }
    }

    private static async Task<IResult> RejectClaimAsync(
        HttpContext context,
        int id,
        RejectUserIdentityRequest body,
        IUserIdentityReviewService reviewer,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        var auth = await AuthorizeAsync(context, authorization, dataOptions, ct);
        if (!auth.Succeeded) return auth.Result!;
        var note = body.ReviewNote?.Trim();
        if (string.IsNullOrWhiteSpace(note) || note.Length > 200)
            return BadRequest("审核意见必须为 1-200 字");

        try
        {
            var result = await reviewer.RejectResponderClaimAsync(id, new UserIdentityRejectCommand(
                auth.Principal!, note, AdminEndpointHelpers.GetClientIp(context),
                AdminEndpointHelpers.GetUserAgent(context.Request)), ct);
            return Results.Ok(ApiResponse.Ok(new AdminResponderClaimReviewResponse(ToDetail(result))));
        }
        catch (UserIdentityReviewException ex)
        {
            return EndpointFailures.JsonFailure(ex.ErrorCode, ex.Message, ex.StatusCode);
        }
    }

    private static async Task<AuthResult> AuthorizeAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return new AuthResult(false, FeatureRequiresSqlite(), null);
        var auth = await authorization.RequireAdminSessionAsync(
            AdminEndpointHelpers.ReadAdminKey(context.Request),
            AdminEndpointHelpers.GetClientIp(context),
            AdminEndpointHelpers.GetUserAgent(context.Request),
            ct);
        return auth.Succeeded
            ? new AuthResult(true, null, auth.Principal)
            : new AuthResult(false, AdminEndpointHelpers.AuthorizationFailure(auth), null);
    }

    private static (UserIdentityAdminQuery Query, string? Error) CreateQuery(
        int? page,
        int? pageSize,
        string? status,
        string? keyword)
    {
        var safePage = page ?? 1;
        var safeSize = pageSize ?? 20;
        if (safePage < 1) return (new UserIdentityAdminQuery(), "page 必须大于等于 1");
        if (safeSize is < 1 or > 100) return (new UserIdentityAdminQuery(), "pageSize 必须在 1 到 100 之间");
        if (!string.IsNullOrWhiteSpace(status) && !ValidStatuses.Contains(status))
            return (new UserIdentityAdminQuery(), $"状态不支持: {status}");
        return (new UserIdentityAdminQuery(safePage, safeSize, status, keyword), null);
    }

    private static AdminResponderClaimListItem ToListItem(ResponderClaimApplicationRecord item)
    {
        return new AdminResponderClaimListItem(item.Id.ToString(), item.OpenId, item.Payload.Name,
            MaskPhone(item.Payload.Phone), item.Status, item.ResponderProfileId,
            item.CreatedAt, item.UpdatedAt, item.ReviewedAt);
    }

    private static AdminResponderClaimDetail ToDetail(ResponderClaimApplicationRecord item)
    {
        return new AdminResponderClaimDetail(item.Id.ToString(), item.OpenId, item.Status,
            item.ResponderProfileId, ToPayload(item.Payload), item.ReviewNote, item.ReviewedBy,
            item.ReviewedAt, item.CreatedAt, item.UpdatedAt);
    }

    private static AdminResponderClaimPayload ToPayload(ResponderClaimPayloadRecord payload)
    {
        return new AdminResponderClaimPayload(payload.Name, payload.IdNumberLast6, payload.Phone,
            payload.CertificateNo, payload.Note);
    }

    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult BadRequest(string message) => EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, message, 400);
    private static string MaskPhone(string? phone) => string.IsNullOrWhiteSpace(phone) || phone.Length < 7 ? string.Empty : $"{phone[..3]}****{phone[^4..]}";
    private static IResult FeatureRequiresSqlite() => EndpointFailures.JsonFailure(ErrorCodes.FeatureRequiresSqlite, "Phase 7D 急救员认领接口仅支持 SQLite 数据源", 501);
    private sealed record AuthResult(bool Succeeded, IResult? Result, Core.Models.Admin.AdminPrincipal? Principal);
}
