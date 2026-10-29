using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Services.Admin;

namespace AedBackend.Api.Endpoints;

internal static class AdminUserEndpoints
{
    public static RouteGroupBuilder MapAdminUserEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/users", ListUsersAsync);
        group.MapGet("/users/{openId}", GetUserAsync);
        group.MapPut("/users/{openId}", UpdateUserAsync);
        return group;
    }

    private static async Task<IResult> ListUsersAsync(
        HttpContext context,
        [AsParameters] AdminUserEndpointQuery query,
        IAdminAuthorizationService authorization,
        IAdminUserService users,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        try
        {
            var result = await users.ListUsersAsync(query.ToQuery(), cancellationToken);
            return Results.Ok(ApiResponse.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, ex.Message, 400);
        }
    }

    private static async Task<IResult> GetUserAsync(
        HttpContext context,
        string openId,
        IAdminAuthorizationService authorization,
        IAdminUserService users,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var result = await users.GetUserAsync(openId, cancellationToken);
        return result is null
            ? EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "用户不存在", 404)
            : Results.Ok(ApiResponse.Ok(result));
    }

    private static async Task<IResult> UpdateUserAsync(
        HttpContext context,
        string openId,
        AdminUserUpdatePayload payload,
        IAdminAuthorizationService authorization,
        IAdminAuditLogStore auditLogs,
        IAdminUserService users,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        try
        {
            var result = await users.UpdateUserAsync(openId, payload, cancellationToken);
            await auditLogs.AppendAsync(CreateAudit(context, auth.Principal, openId, payload), cancellationToken);
            return Results.Ok(ApiResponse.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, ex.Message, 400);
        }
        catch (KeyNotFoundException)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "用户不存在", 404);
        }
    }

    private static Task<AdminAuthorizationResult> RequireAdminAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        return authorization.RequireAdminSessionAsync(
            AdminEndpointHelpers.ReadAdminKey(context.Request),
            AdminEndpointHelpers.GetClientIp(context),
            AdminEndpointHelpers.GetUserAgent(context.Request),
            cancellationToken);
    }

    private static Core.Models.Admin.AdminAuditLogRecord CreateAudit(
        HttpContext context,
        Core.Models.Admin.AdminPrincipal? actor,
        string openId,
        AdminUserUpdatePayload payload)
    {
        return AdminEndpointHelpers.CreateAudit(
            "admin",
            "admin.user_profile_update",
            actor,
            openId,
            "管理端用户投影已更新",
            context,
            new Dictionary<string, object?> { ["changedFields"] = ChangedFields(payload) });
    }

    private static IReadOnlyList<string> ChangedFields(AdminUserUpdatePayload payload)
    {
        var fields = new List<string>();
        if (payload.PrimaryRole.HasValue) fields.Add("primaryRole");
        if (payload.AccountStatus.HasValue) fields.Add("accountStatus");
        if (payload.ResponderStatus.HasValue) fields.Add("responderStatus");
        if (payload.Certified.HasValue) fields.Add("certified");
        if (payload.Level.HasValue) fields.Add("level");
        if (payload.AdminRemark.HasValue) fields.Add("adminRemark");
        return fields;
    }

    private sealed class AdminUserEndpointQuery
    {
        public int? Page { get; init; }
        public int? PageSize { get; init; }
        public string? Keyword { get; init; }
        public string? PrimaryRole { get; init; }
        public string? AccountStatus { get; init; }
        public string? ResponderStatus { get; init; }
        public string? Level { get; init; }

        public AdminUserQuery ToQuery()
        {
            return new AdminUserQuery(
                Page,
                PageSize,
                Keyword,
                PrimaryRole,
                AccountStatus,
                ResponderStatus,
                Level);
        }
    }
}
