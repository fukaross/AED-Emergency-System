using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Help;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Core.Services.WeChat;
using AedBackend.Api.WebSockets;
using AedBackend.Infrastructure.Upload;
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
        IUserAccountStatusService accountStatus,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
        IResponderStore responderStore,
        IWeChatSubscribeMessageSender sender,
        CancellationToken cancellationToken)
    {
        var current = await EndpointUsers.RequireActiveUserAsync(
            httpRequest,
            tokenService,
            userStore,
            accountStatus,
            cancellationToken);
        if (current.Failure is not null) return current.Failure;

        var currentUser = current.User!;
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
        // 急救会话随求助发布创建（BackEnd.md §4.5A 生命周期约定）
        await sessionStore.GetOrCreateAsync(record.Id, DateTimeOffset.UtcNow, cancellationToken);
        var recipients = await HelpPublishFlow.SendNotificationsAsync(
            record,
            request,
            currentUser,
            options.Value,
            userStore,
            responderStore,
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
        IUserAccountStatusService accountStatus,
        IHelpRequestStore helpStore,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(httpRequest, tokenService, userStore, accountStatus, cancellationToken);
        if (user.Failure is not null) return user.Failure;

        var records = await helpStore.ListByUserAsync(user.User!.OpenId, cancellationToken);
        var filtered = HelpListPaging.FilterByStatus(records, status);
        return Results.Ok(ApiResponse.Ok(HelpListPaging.ToPage(filtered, page, pageSize, user.User.OpenId)));
    }

    private static async Task<IResult> GetHelpDetailAsync(
        HttpRequest httpRequest,
        string id,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        IResponderStore responderStore,
        IHelpRequestStore helpStore,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(httpRequest, tokenService, userStore, accountStatus, cancellationToken);
        if (user.Failure is not null) return user.Failure;

        var record = await helpStore.FindByIdOrRequestNoAsync(id, cancellationToken);
        if (record is null) return NotFound("求助不存在");

        // 详情权限收紧（BackEnd.md §4.5）：仅求助人本人或认证急救员可访问
        if (record.UserId != user.User!.OpenId)
        {
            var profile = await responderStore.FindByBoundOpenIdAsync(user.User.OpenId, cancellationToken);
            if (profile?.QualificationStatus != EndpointRescuers.ActiveQualificationStatus)
            {
                return EndpointFailures.JsonFailure(ErrorCodes.Forbidden, "仅求助人本人或认证急救员可查看", 403);
            }
        }

        return Results.Ok(ApiResponse.Ok(HelpResponseMapper.ToDetail(record)));
    }

    private static async Task<IResult> SubmitReportAsync(
        HttpRequest httpRequest,
        string id,
        HelpReportRequest request,
        IOptions<WeChatOptions> options,
        IOptions<UploadOptions> uploadOptions,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        IResponderStore responderStore,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
        RescueSessionBroadcaster broadcaster,
        CancellationToken cancellationToken)
    {
        // 仅认证急救员可提交救助报告（BackEnd.md §4.5 权限约定）
        var responder = await EndpointRescuers.RequireCertifiedResponderAsync(
            httpRequest,
            tokenService,
            userStore,
            accountStatus,
            responderStore,
            cancellationToken);
        if (responder.Failure is not null) return responder.Failure;

        var validationError = HelpPublishFlow.ValidateReport(
            request,
            options.Value,
            uploadOptions.Value);
        if (validationError is not null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.HelpReportMissingFields, validationError, 400);
        }

        var now = DateTimeOffset.UtcNow;
        var report = new HelpReportRecord(
            Guid.NewGuid().ToString("N"),
            responder.User!.OpenId,
            request.Images!,
            request.RecordText.Trim(),
            request.IsFalseAlarm,
            now);
        var nextStatus = request.IsFalseAlarm
            ? HelpStatuses.FalseAlarm
            : HelpStatuses.PendingReview;

        return await UpdateHelpAsync(
            async () =>
            {
                var updated = await helpStore.AddReportAsync(
                    new HelpReportUpdate(id, report, nextStatus, now),
                    cancellationToken);
                // 求助状态变化扇出 help_status；误报关闭时级联关闭会话并清空位置（BackEnd.md §4.5A）
                await broadcaster.BroadcastAsync(updated.Id, "help_status", new RescueHelpStatusDto(updated.Status));
                if (request.IsFalseAlarm)
                {
                    await sessionStore.CloseSessionAsync(updated.Id, cancellationToken);
                    await broadcaster.CloseAllAsync(updated.Id, "help closed");
                }
                return updated;
            },
            record => Results.Ok(ApiResponse.Ok(new HelpReportResponse(report.Id, record.Status, now))));
    }

    private static async Task<IResult> CloseHelpAsync(
        HttpRequest httpRequest,
        string id,
        CloseHelpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
        RescueSessionBroadcaster broadcaster,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(httpRequest, tokenService, userStore, accountStatus, cancellationToken);
        if (user.Failure is not null) return user.Failure;

        if (request.Status is not (HelpStatuses.Closed or HelpStatuses.FalseAlarm))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "status 不支持", 400);
        }

        // 仅求助发布者本人可关闭（BackEnd.md §5.4 权限矩阵）；管理端走 /api/admin/help-requests/{id}/close
        var existing = await helpStore.FindByIdOrRequestNoAsync(id, cancellationToken);
        if (existing is null) return NotFound("求助不存在");
        if (existing.UserId != user.User!.OpenId)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.Forbidden, "仅求助发布者本人可关闭", 403);
        }

        return await UpdateHelpAsync(
            async () =>
            {
                var closed = await helpStore.CloseAsync(
                    id,
                    request.Status,
                    request.Note,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
                // 求助关闭级联关闭急救会话并清空参与者位置（BackEnd.md §4.5A 生命周期约定）
                await sessionStore.CloseSessionAsync(closed.Id, cancellationToken);
                await broadcaster.BroadcastAsync(closed.Id, "help_status", new RescueHelpStatusDto(closed.Status));
                await broadcaster.CloseAllAsync(closed.Id, "help closed");
                return closed;
            },
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
        IRescueSessionStore sessionStore,
        RescueSessionBroadcaster broadcaster,
        CancellationToken cancellationToken)
    {
        var record = await helpStore.FindByIdOrRequestNoAsync(request.RequestNo, cancellationToken);
        if (record?.GuestSession?.CloseToken != request.CloseToken)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "求助不存在", 404);
        }

        return await UpdateHelpAsync(
            async () =>
            {
                var closed = await helpStore.CloseAsync(
                    record.Id,
                    HelpStatuses.FalseAlarm,
                    request.Note,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
                // 游客关单同样级联关闭急救会话并清空参与者位置
                await sessionStore.CloseSessionAsync(closed.Id, cancellationToken);
                await broadcaster.BroadcastAsync(closed.Id, "help_status", new RescueHelpStatusDto(closed.Status));
                await broadcaster.CloseAllAsync(closed.Id, "help closed");
                return closed;
            },
            updated => Results.Ok(ApiResponse.Ok(new GuestCloseHelpResponse(
                updated.RequestNo,
                HelpStatuses.FalseAlarm,
                updated.ClosedAt!.Value))));
    }

    private static async Task<CurrentUserResult> RequireUserAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        CancellationToken cancellationToken)
    {
        return await EndpointUsers.RequireActiveUserAsync(
            request,
            tokenService,
            userStore,
            accountStatus,
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

    private static IResult NotFound(string message)
    {
        return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, message, 404);
    }
}
