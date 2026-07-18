using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Help;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Api.WebSockets;

namespace AedBackend.Api.Endpoints;

internal static class AdminHelpEndpoints
{
    public static RouteGroupBuilder MapAdminHelpEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/help-requests", ListHelpRequestsAsync);
        group.MapGet("/help-requests/{id}", GetHelpRequestAsync);
        group.MapPut("/help-requests/{id}/close", CloseHelpRequestAsync);
        return group;
    }

    private static async Task<IResult> ListHelpRequestsAsync(
        HttpContext context,
        [AsParameters] AdminHelpQuery query,
        IAdminAuthorizationService authorization,
        IHelpRequestStore store,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var records = await store.ListAllAsync(cancellationToken);
        var filtered = ApplyFilters(records, query);
        return Results.Ok(ApiResponse.Ok(
            AdminEndpointHelpers.ToAdminHelpPage(filtered, query.Page, query.PageSize)));
    }

    private static async Task<IResult> GetHelpRequestAsync(
        HttpContext context,
        string id,
        IAdminAuthorizationService authorization,
        IHelpRequestStore store,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var record = await store.FindByIdOrRequestNoAsync(id, cancellationToken);
        return record is null
            ? EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "求助不存在", 404)
            : Results.Ok(ApiResponse.Ok(AdminEndpointHelpers.ToAdminHelpDetail(record)));
    }

    private static async Task<IResult> CloseHelpRequestAsync(
        HttpContext context,
        string id,
        CloseHelpRequest request,
        IAdminAuthorizationService authorization,
        IHelpRequestStore store,
        HelpClosureApplication closure,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        if (request.Status is not (HelpStatuses.Closed or HelpStatuses.FalseAlarm))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "status 不支持", 400);
        var existing = await store.FindByIdOrRequestNoAsync(id, cancellationToken);
        if (existing is null)
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "求助不存在", 404);

        return await HelpEndpoints.UpdateHelpAsync(
            () => closure.CloseAsync(
                new HelpClosureCommand
                {
                    HelpId = existing.Id,
                    Status = request.Status,
                    Note = request.Note,
                    Audit = CreateCloseAudit(
                        context,
                        auth.Principal,
                        existing with { Status = request.Status })
                },
                cancellationToken),
            record => Results.Ok(ApiResponse.Ok(AdminEndpointHelpers.ToAdminHelpDetail(record))));
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

    private static IReadOnlyList<HelpRequestRecord> ApplyFilters(
        IReadOnlyList<HelpRequestRecord> records,
        AdminHelpQuery query)
    {
        var filtered = HelpListPaging.FilterByStatus(records, query.Status);
        return string.IsNullOrWhiteSpace(query.Keyword)
            ? filtered
            : filtered.Where(item => MatchesKeyword(item, query.Keyword.Trim())).ToArray();
    }

    private static bool MatchesKeyword(HelpRequestRecord item, string keyword)
    {
        return Contains(item.RequestNo, keyword)
            || Contains(item.Address, keyword)
            || Contains(item.Content, keyword);
    }

    private static bool Contains(string? value, string keyword)
    {
        return value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static Core.Models.Admin.AdminAuditLogRecord CreateCloseAudit(
        HttpContext context,
        Core.Models.Admin.AdminPrincipal? actor,
        HelpRequestRecord record)
    {
        var eventName = record.Status == HelpStatuses.FalseAlarm ? "help.false_alarm" : "help.close";
        return AdminEndpointHelpers.CreateAudit(
            "help",
            eventName,
            actor,
            record.Id,
            $"求助 {record.RequestNo} 已处理为 {record.Status}",
            context,
            new Dictionary<string, object?> { ["status"] = record.Status });
    }

    private sealed class AdminHelpQuery
    {
        public int? Page { get; init; }
        public int? PageSize { get; init; }
        public string? Status { get; init; }
        public string? Keyword { get; init; }
    }
}
