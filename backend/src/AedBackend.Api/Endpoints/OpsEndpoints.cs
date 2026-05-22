using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Help;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;

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
        var authFailure = await ValidateAdminAsync(httpRequest, authorization, cancellationToken);
        if (authFailure is not null) return authFailure;

        var records = await store.ListAllAsync(cancellationToken);
        var filtered = FilterOpsRecords(records, query);
        return Results.Ok(ApiResponse.Ok(HelpListPaging.ToPage(filtered, query.Page, query.PageSize)));
    }

    private static async Task<IResult> CloseHelpRequestAsync(
        HttpRequest httpRequest,
        string id,
        CloseHelpRequest request,
        IAdminAuthorizationService authorization,
        IHelpRequestStore store,
        CancellationToken cancellationToken)
    {
        var authFailure = await ValidateAdminAsync(httpRequest, authorization, cancellationToken);
        if (authFailure is not null) return authFailure;

        if (request.Status is not (HelpStatuses.Closed or HelpStatuses.FalseAlarm))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "status 不支持", 400);
        }

        try
        {
            var record = await store.CloseAsync(
                id,
                request.Status,
                request.Note,
                DateTimeOffset.UtcNow,
                cancellationToken);
            return Results.Ok(ApiResponse.Ok(HelpResponseMapper.ToDetail(record)));
        }
        catch (KeyNotFoundException)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "求助不存在", 404);
        }
        catch (InvalidOperationException)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.HelpAlreadyClosed, "该求助已结束", 409);
        }
    }

    private static async Task<IResult?> ValidateAdminAsync(
        HttpRequest request,
        IAdminAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var result = await authorization.RequireLegacyOrAdminAsync(
            AdminEndpointHelpers.ReadAdminKey(request),
            request.HttpContext.Connection.RemoteIpAddress?.ToString(),
            request.Headers.UserAgent.ToString(),
            cancellationToken);
        return result.Succeeded ? null : AdminEndpointHelpers.AuthorizationFailure(result);
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
