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

public static class OpsEndpoints
{
    public static IEndpointRouteBuilder MapOpsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ops")
            .WithTags("Ops");

        group.MapGet("/help-requests", ListHelpRequestsAsync)
            .WithName("OpsListHelpRequests");
        group.MapPut("/help-requests/{id}/close", CloseHelpRequestAsync)
            .WithName("OpsCloseHelpRequest");

        return app;
    }

    private static async Task<IResult> ListHelpRequestsAsync(
        HttpRequest httpRequest,
        [AsParameters] OpsHelpRequestQuery query,
        IAdminAuthorizationService authorization,
        IHelpRequestStore store,
        CancellationToken cancellationToken)
    {
        var auth = await AuthorizeAdminAsync(httpRequest, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var records = await store.ListAllAsync(cancellationToken);
        var filtered = FilterOpsRecords(records, query);
        return Results.Ok(ApiResponse.Ok(HelpListPaging.ToPage(filtered, query.Page, query.PageSize)));
    }

    private static async Task<IResult> CloseHelpRequestAsync(
        HttpRequest httpRequest,
        string id,
        CloseHelpRequest request,
        IAdminAuthorizationService authorization,
        IHelpRequestStore helpStore,
        HelpClosureApplication closure,
        CancellationToken cancellationToken)
    {
        var auth = await AuthorizeAdminAsync(httpRequest, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        if (request.Status is not (HelpStatuses.Closed or HelpStatuses.FalseAlarm))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "status 不支持", 400);
        }
        var existing = await helpStore.FindByIdOrRequestNoAsync(id, cancellationToken);
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
                        httpRequest.HttpContext,
                        auth.Principal,
                        existing with { Status = request.Status })
                },
                cancellationToken),
            record => Results.Ok(ApiResponse.Ok(HelpResponseMapper.ToDetail(record))));
    }

    private static Task<AdminAuthorizationResult> AuthorizeAdminAsync(
        HttpRequest request,
        IAdminAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        return authorization.RequireLegacyOrAdminAsync(
            AdminEndpointHelpers.ReadAdminKey(request),
            request.HttpContext.Connection.RemoteIpAddress?.ToString(),
            request.Headers.UserAgent.ToString(),
            cancellationToken);
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
            $"兼容入口将求助 {record.RequestNo} 处理为 {record.Status}",
            context,
            new Dictionary<string, object?> { ["status"] = record.Status });
    }

    private static IReadOnlyList<HelpRequestRecord> FilterOpsRecords(
        IReadOnlyList<HelpRequestRecord> records,
        OpsHelpRequestQuery query)
    {
        var filtered = HelpListPaging.FilterByStatus(records, query.Status);
        var keyword = query.Keyword?.Trim();
        return string.IsNullOrWhiteSpace(keyword)
            ? filtered
            : filtered.Where(item => MatchesKeyword(item, keyword)).ToArray();
    }

    private static bool MatchesKeyword(HelpRequestRecord record, string keyword)
    {
        return ContainsKeyword(record.RequestNo, keyword)
            || ContainsKeyword(record.Address, keyword)
            || ContainsKeyword(record.Content, keyword);
    }

    private static bool ContainsKeyword(string? value, string keyword)
    {
        return value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
    }

    private sealed class OpsHelpRequestQuery
    {
        public int? Page { get; init; }
        public int? PageSize { get; init; }
        public string? Status { get; init; }
        public string? Keyword { get; init; }
    }
}
