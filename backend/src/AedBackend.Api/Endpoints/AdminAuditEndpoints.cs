using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Services.Admin;

namespace AedBackend.Api.Endpoints;

internal static class AdminAuditEndpoints
{
    public static RouteGroupBuilder MapAdminAuditEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/audit-logs", ListAuditLogsAsync);
        return group;
    }

    private static async Task<IResult> ListAuditLogsAsync(
        HttpContext context,
        [AsParameters] AuditLogQuery query,
        IAdminAuthorizationService authorization,
        IAdminAuditLogStore auditLogs,
        CancellationToken cancellationToken)
    {
        var auth = await RequireSuperAdminAsync(context, authorization, cancellationToken);
        if (auth is not null) return auth;

        var logs = await auditLogs.ListAsync(cancellationToken);
        var filtered = ApplyFilters(logs, query);
        return Results.Ok(ApiResponse.Ok(
            AdminEndpointHelpers.ToAuditPage(filtered, query.Page, query.PageSize)));
    }

    private static async Task<IResult?> RequireSuperAdminAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var result = await authorization.RequireAdminSessionAsync(
            AdminEndpointHelpers.ReadAdminKey(context.Request),
            AdminEndpointHelpers.GetClientIp(context),
            AdminEndpointHelpers.GetUserAgent(context.Request),
            cancellationToken);

        if (!result.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(result);
        return result.Principal!.Role == AdminRoles.SuperAdmin
            ? null
            : AdminEndpointHelpers.Forbidden();
    }

    private static IReadOnlyList<AdminAuditLogRecord> ApplyFilters(
        IReadOnlyList<AdminAuditLogRecord> logs,
        AuditLogQuery query)
    {
        var filtered = logs.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query.EventType))
            filtered = filtered.Where(log => log.EventType == query.EventType);
        if (!string.IsNullOrWhiteSpace(query.EventName))
            filtered = filtered.Where(log => log.EventName == query.EventName);
        if (!string.IsNullOrWhiteSpace(query.Actor))
            filtered = filtered.Where(log => Contains(log.ActorName, query.Actor) || Contains(log.ActorId, query.Actor));

        return string.IsNullOrWhiteSpace(query.Keyword)
            ? filtered.ToArray()
            : filtered.Where(log => MatchesKeyword(log, query.Keyword.Trim())).ToArray();
    }

    private static bool MatchesKeyword(AdminAuditLogRecord log, string keyword)
    {
        return Contains(log.EventName, keyword)
            || Contains(log.TargetId, keyword)
            || Contains(log.Summary, keyword)
            || Contains(log.ActorName, keyword);
    }

    private static bool Contains(string? value, string keyword)
    {
        return value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
    }

    private sealed class AuditLogQuery
    {
        public int? Page { get; init; }
        public int? PageSize { get; init; }
        public string? EventType { get; init; }
        public string? EventName { get; init; }
        public string? Actor { get; init; }
        public string? Keyword { get; init; }
    }
}
