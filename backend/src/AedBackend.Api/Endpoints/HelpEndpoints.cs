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
using Microsoft.AspNetCore.Mvc;
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
        group.MapPost("/guest/query", GuestHelpEndpoints.QueryAsync).WithName("GuestQueryHelp");
        group.MapPost("/guest/close", GuestHelpEndpoints.CloseAsync).WithName("GuestCloseHelp");
        group.MapGet("/{id}", GetHelpDetailAsync).WithName("GetHelpDetail");
        group.MapPost("/{id}/report", SubmitReportAsync).WithName("SubmitHelpReport");
        group.MapPut("/{id}/close", CloseHelpAsync).WithName("CloseHelp");

        return app;
    }

    /** 求助一旦持久化，通知阶段不再受客户端断连取消。 */
    private static async Task<IResult> PublishHelpAsync(
        [AsParameters] PublishHelpEndpointRequest endpoint)
    {
        var preparation = await PreparePublishAsync(endpoint);
        if (preparation.Failure is not null) return preparation.Failure;
        var currentUser = preparation.User!;
        var record = HelpPublishFlow.CreateRecord(endpoint.Body, currentUser);
        var saved = await HelpPublishPersistence.ExecuteAsync(new HelpPublishPersistenceInput
        {
            HelpId = record.Id,
            Lifecycle = endpoint.Lifecycle,
            TrySaveHelp = token => endpoint.HelpStore.TrySaveIfNoActiveAsync(record, token),
            EnsureSession = token => endpoint.SessionStore.GetOrCreateAsync(
                record.Id,
                record.CreatedAt,
                token),
            RequestCancellationToken = endpoint.CancellationToken,
        });
        if (!saved) return DuplicateActiveHelp();
        var recipients = await HelpPublishFlow.SendNotificationsAsync(new HelpNotificationInput
        {
            Record = record,
            Request = endpoint.Body,
            CurrentUser = currentUser,
            Options = endpoint.Options.Value,
            UserStore = endpoint.UserStore,
            AccountStatus = endpoint.AccountStatus,
            ResponderStore = endpoint.ResponderStore,
            Sender = endpoint.Sender,
            CancellationToken = CancellationToken.None,
        });

        return Results.Ok(ApiResponse.Ok(HelpPublishFlow.ToPublishResponse(record, recipients)));
    }

    private static async Task<IResult> ListHelpAsync(
        [AsParameters] ListHelpEndpointRequest endpoint)
    {
        var user = await RequireUserAsync(endpoint);
        if (user.Failure is not null) return user.Failure;

        var records = await endpoint.HelpStore.ListByUserAsync(
            user.User!.OpenId,
            endpoint.CancellationToken);
        var filtered = HelpListPaging.FilterByStatus(records, endpoint.Status);
        return Results.Ok(ApiResponse.Ok(HelpListPaging.ToPage(
            filtered,
            endpoint.Page,
            endpoint.PageSize,
            user.User.OpenId)));
    }

    private static async Task<IResult> GetHelpDetailAsync(
        [AsParameters] GetHelpEndpointRequest endpoint)
    {
        var cancellationToken = endpoint.CancellationToken;
        var user = await RequireUserAsync(endpoint);
        if (user.Failure is not null) return user.Failure;

        var record = await endpoint.HelpStore.FindByIdOrRequestNoAsync(
            endpoint.Id,
            cancellationToken);
        if (record is null) return NotFound("求助不存在");

        var viewerRole = "requester";
        var canSubmitReport = false;
        // 详情权限收紧（BackEnd.md §4.5）：仅求助人本人或认证急救员可访问
        if (record.UserId != user.User!.OpenId)
        {
            var profile = await endpoint.ResponderStore.FindByBoundOpenIdAsync(
                user.User.OpenId,
                cancellationToken);
            if (profile?.QualificationStatus != EndpointRescuers.ActiveQualificationStatus)
            {
                return EndpointFailures.JsonFailure(ErrorCodes.Forbidden, "仅求助人本人或认证急救员可查看", 403);
            }

            viewerRole = "responder";
            canSubmitReport = HelpStatuses.Active.Contains(record.Status);
        }

        return Results.Ok(ApiResponse.Ok(HelpResponseMapper.ToDetail(
            record,
            viewerRole,
            canSubmitReport)));
    }

    private static async Task<IResult> SubmitReportAsync(
        [AsParameters] SubmitHelpReportEndpointRequest endpoint)
    {
        // 仅认证急救员可提交救助报告（BackEnd.md §4.5 权限约定）
        var responder = await EndpointRescuers.RequireCertifiedResponderAsync(
            new CertifiedResponderAuthorization
            {
                Request = endpoint.HttpRequest,
                TokenService = endpoint.TokenService,
                UserStore = endpoint.UserStore,
                AccountStatus = endpoint.AccountStatus,
                ResponderStore = endpoint.ResponderStore,
                CancellationToken = endpoint.CancellationToken
            });
        if (responder.Failure is not null) return responder.Failure;

        return await HelpReportFlow.ExecuteAsync(new HelpReportInput
        {
            HelpId = endpoint.Id,
            Request = endpoint.Body,
            WeChatOptions = endpoint.Options.Value,
            UploadOptions = endpoint.UploadOptions.Value,
            Responder = responder,
            HelpStore = endpoint.HelpStore,
            Lifecycle = endpoint.Lifecycle,
            Broadcaster = endpoint.Broadcaster,
            Closure = endpoint.Closure,
            CancellationToken = endpoint.CancellationToken,
        });
    }

    private static async Task<IResult> CloseHelpAsync(
        [AsParameters] CloseHelpEndpointRequest endpoint)
    {
        var cancellationToken = endpoint.CancellationToken;
        var user = await RequireUserAsync(endpoint);
        if (user.Failure is not null) return user.Failure;

        if (endpoint.Body.Status is not (HelpStatuses.Closed or HelpStatuses.FalseAlarm))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "status 不支持", 400);
        }

        // 仅求助发布者本人可关闭（BackEnd.md §5.4 权限矩阵）；管理端走 /api/admin/help-requests/{id}/close
        var existing = await endpoint.HelpStore.FindByIdOrRequestNoAsync(
            endpoint.Id,
            cancellationToken);
        if (existing is null) return NotFound("求助不存在");
        if (existing.UserId != user.User!.OpenId)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.Forbidden, "仅求助发布者本人可关闭", 403);
        }

        return await UpdateHelpAsync(
            () => endpoint.Closure.CloseAsync(
                new HelpClosureCommand
                {
                    HelpId = existing.Id,
                    Status = endpoint.Body.Status,
                    Note = endpoint.Body.Note
                },
                cancellationToken),
            _ => Results.Ok(ApiResponse.Ok<object>(null)));
    }

    private static async Task<HelpPublishPreparation> PreparePublishAsync(
        PublishHelpEndpointRequest endpoint)
    {
        var current = await EndpointUsers.RequireActiveUserAsync(
            endpoint.HttpRequest,
            new ActiveUserDependencies(
                endpoint.TokenService,
                endpoint.UserStore,
                endpoint.AccountStatus),
            endpoint.CancellationToken);
        if (current.Failure is not null)
            return new HelpPublishPreparation(null, current.Failure);

        var validationError = HelpPublishFlow.ValidatePublishRequest(
            endpoint.Body,
            endpoint.Options.Value,
            current.User);
        if (validationError is not null)
        {
            return new HelpPublishPreparation(
                null,
                EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validationError, 400));
        }

        return new HelpPublishPreparation(current.User, null);
    }

    private static async Task<CurrentUserResult> RequireUserAsync(
        IUserHelpEndpointRequest endpoint)
    {
        return await EndpointUsers.RequireActiveUserAsync(
            endpoint.HttpRequest,
            new ActiveUserDependencies(
                endpoint.TokenService,
                endpoint.UserStore,
                endpoint.AccountStatus),
            endpoint.CancellationToken);
    }

    internal static async Task<IResult> UpdateHelpAsync(
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
        catch (HelpAlreadyClosedException)
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

    private static IResult DuplicateActiveHelp()
    {
        return EndpointFailures.JsonFailure(
            ErrorCodes.DuplicateHelpRequest,
            "已有进行中的求助",
            409);
    }

    private sealed record HelpPublishPreparation(
        ExperienceUserRecord? User,
        IResult? Failure);
}
