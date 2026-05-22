using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Help;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

public static class HelpEndpoints
{
    public static IEndpointRouteBuilder MapHelpEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/help")
            .WithTags("Help");

        group.MapPost("/request", PublishHelpAsync).WithName("PublishHelpRequest");
        group.MapGet("/list", ListHelpAsync).WithName("ListHelpRequests");
        group.MapPost("/guest/query", GuestQueryHelpAsync).WithName("GuestQueryHelp");
        group.MapPost("/guest/close", GuestCloseHelpAsync).WithName("GuestCloseHelp");
        group.MapGet("/{id}", GetHelpDetailAsync).WithName("GetHelpDetail");
        group.MapPost("/{id}/report", SubmitReportAsync).WithName("SubmitHelpReport");
        group.MapPut("/{id}/close", CloseHelpAsync).WithName("CloseHelp");

        return app;
    }

    private static async Task<IResult> PublishHelpAsync(
        HttpRequest httpRequest,
        PublishHelpRequest request,
        IOptions<WeChatOptions> options,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IHelpRequestStore helpStore,
        IWeChatSubscribeMessageSender sender,
        CancellationToken cancellationToken)
    {
        var currentUser = await EndpointUsers.FindCurrentUserAsync(
            httpRequest,
            tokenService,
            userStore,
            cancellationToken);
        var validationError = HelpPublishFlow.ValidatePublishRequest(
            request,
            options.Value,
            currentUser);
        if (validationError is not null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validationError, 400);
        }

        if (await helpStore.HasActiveRequestAsync(currentUser!.OpenId, cancellationToken))
        {
            return EndpointFailures.JsonFailure(
                ErrorCodes.DuplicateHelpRequest,
                "已有进行中的求助",
                409);
        }

        var record = HelpPublishFlow.CreateRecord(request, currentUser);
        await helpStore.SaveAsync(record, cancellationToken);
        var recipients = await HelpPublishFlow.SendNotificationsAsync(
            record,
            request,
            currentUser,
            options.Value,
            userStore,
            sender,
            cancellationToken);

        return Results.Ok(ApiResponse.Ok(HelpPublishFlow.ToPublishResponse(record, recipients)));
    }

    private static async Task<IResult> ListHelpAsync(
        HttpRequest httpRequest,
        int? page,
        int? pageSize,
        string? status,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IHelpRequestStore helpStore,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(httpRequest, tokenService, userStore, cancellationToken);
        if (user is null) return Unauthorized();

        var records = await helpStore.ListByUserAsync(user.OpenId, cancellationToken);
        var filtered = HelpListPaging.FilterByStatus(records, status);
        return Results.Ok(ApiResponse.Ok(HelpListPaging.ToPage(filtered, page, pageSize)));
    }

    private static async Task<IResult> GetHelpDetailAsync(
        HttpRequest httpRequest,
        string id,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IHelpRequestStore helpStore,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(httpRequest, tokenService, userStore, cancellationToken);
        if (user is null) return Unauthorized();

        var record = await helpStore.FindByIdOrRequestNoAsync(id, cancellationToken);
        return record is null
            ? NotFound("求助不存在")
            : Results.Ok(ApiResponse.Ok(HelpResponseMapper.ToDetail(record)));
    }

    private static async Task<IResult> SubmitReportAsync(
        HttpRequest httpRequest,
        string id,
        HelpReportRequest request,
        IOptions<WeChatOptions> options,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IHelpRequestStore helpStore,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(httpRequest, tokenService, userStore, cancellationToken);
        if (user is null) return Unauthorized();

        // TODO: Revalidate responder_profiles status/type before accepting reports.
        var validationError = HelpPublishFlow.ValidateReport(request, options.Value);
        if (validationError is not null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.HelpReportMissingFields, validationError, 400);
        }

        var now = DateTimeOffset.UtcNow;
        var report = new HelpReportRecord(
            Guid.NewGuid().ToString("N"),
            user.OpenId,
            request.Images!,
            request.RecordText.Trim(),
            request.IsFalseAlarm,
            now);
        var nextStatus = request.IsFalseAlarm
            ? HelpStatuses.FalseAlarm
            : HelpStatuses.PendingReview;

        return await UpdateHelpAsync(
            () => helpStore.AddReportAsync(new HelpReportUpdate(id, report, nextStatus, now), cancellationToken),
            record => Results.Ok(ApiResponse.Ok(new HelpReportResponse(report.Id, record.Status, now))));
    }

    private static async Task<IResult> CloseHelpAsync(
        HttpRequest httpRequest,
        string id,
        CloseHelpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IHelpRequestStore helpStore,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(httpRequest, tokenService, userStore, cancellationToken);
        if (user is null) return Unauthorized();

        if (request.Status is not (HelpStatuses.Closed or HelpStatuses.FalseAlarm))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "status 不支持", 400);
        }

        return await UpdateHelpAsync(
            () => helpStore.CloseAsync(
                id,
                request.Status,
                request.Note,
                DateTimeOffset.UtcNow,
                cancellationToken),
            _ => Results.Ok(ApiResponse.Ok<object>(null)));
    }

    private static async Task<IResult> GuestQueryHelpAsync(
        GuestHelpLookupRequest request,
        IHelpRequestStore helpStore,
        CancellationToken cancellationToken)
    {
        var record = await helpStore.FindByIdOrRequestNoAsync(request.RequestNo, cancellationToken);
        if (record?.GuestSession?.FollowupCode != request.FollowupCode)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "求助不存在", 404);
        }

        return Results.Ok(ApiResponse.Ok(HelpResponseMapper.ToGuestLookup(record)));
    }

    private static async Task<IResult> GuestCloseHelpAsync(
        GuestCloseHelpRequest request,
        IHelpRequestStore helpStore,
        CancellationToken cancellationToken)
    {
        var record = await helpStore.FindByIdOrRequestNoAsync(request.RequestNo, cancellationToken);
        if (record?.GuestSession?.CloseToken != request.CloseToken)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "求助不存在", 404);
        }

        return await UpdateHelpAsync(
            () => helpStore.CloseAsync(
                record.Id,
                HelpStatuses.FalseAlarm,
                request.Note,
                DateTimeOffset.UtcNow,
                cancellationToken),
            updated => Results.Ok(ApiResponse.Ok(new GuestCloseHelpResponse(
                updated.RequestNo,
                HelpStatuses.FalseAlarm,
                updated.ClosedAt!.Value))));
    }

    private static async Task<ExperienceUserRecord?> RequireUserAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        CancellationToken cancellationToken)
    {
        return await EndpointUsers.FindCurrentUserAsync(
            request,
            tokenService,
            userStore,
            cancellationToken);
    }

    private static async Task<IResult> UpdateHelpAsync(
        Func<Task<HelpRequestRecord>> update,
        Func<HelpRequestRecord, IResult> success)
    {
        try
        {
            return success(await update());
        }
        catch (KeyNotFoundException)
        {
            return NotFound("求助不存在");
        }
        catch (InvalidOperationException)
        {
            return EndpointFailures.JsonFailure(
                ErrorCodes.HelpAlreadyClosed,
                "该求助已结束",
                409);
        }
    }

    private static IResult Unauthorized()
    {
        return EndpointFailures.JsonFailure(ErrorCodes.Unauthorized, "登录 token 无效", 401);
    }

    private static IResult NotFound(string message)
    {
        return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, message, 404);
    }
}
