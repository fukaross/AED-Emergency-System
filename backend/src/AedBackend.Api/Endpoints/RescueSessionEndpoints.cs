using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Core.Services.WeChat;
using AedBackend.Api.WebSockets;
using AedBackend.Infrastructure.Rescue;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

public static class RescueSessionEndpoints
{
    private const int MessageTextMaxLength = 500;

    public static IEndpointRouteBuilder MapRescueSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var helpGroup = app.MapGroup("/api/help").WithTags("Rescue");
        helpGroup.MapPost("/{id}/session/join", JoinSessionAsync).WithName("JoinRescueSession");
        helpGroup.MapGet("/{id}/session", GetSessionAsync).WithName("GetRescueSession");
        helpGroup.MapPost("/{id}/session/messages", PostMessageAsync).WithName("PostRescueMessage");
        helpGroup.MapPost("/{id}/session/status-events", PostStatusEventAsync).WithName("PostRescueStatusEvent");
        helpGroup.MapPut("/{id}/session/location", ReportLocationAsync).WithName("ReportRescueLocation");
        app.MapRescueSessionQueryEndpoints();
        return app;
    }

    private static async Task<IResult> JoinSessionAsync(
        HttpRequest httpRequest,
        string id,
        IOptions<RescueOptions> rescueOptions,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        IResponderStore responderStore,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
        RescueSessionBroadcaster broadcaster,
        CancellationToken cancellationToken)
    {
        var services = new SessionEndpointServices(tokenService, userStore, accountStatus, helpStore, sessionStore);
        var responder = await EndpointRescuers.RequireCertifiedResponderAsync(
            httpRequest,
            services.Tokens,
            services.Users,
            services.AccountStatus,
            responderStore,
            cancellationToken);
        if (responder.Failure is not null) return responder.Failure;

        var help = await services.HelpStore.FindByIdOrRequestNoAsync(id, cancellationToken);
        if (help is null) return RescueSessionContext.NotFound();
        if (HelpStatuses.IsClosed(help.Status)) return RescueSessionContext.SessionClosed();

        var now = DateTimeOffset.UtcNow;
        var existing = await services.SessionStore.FindByHelpIdAsync(help.Id, cancellationToken);
        var isNewParticipant = existing?.Participants.Any(p => p.OpenId == responder.User!.OpenId) != true;
        var displayName = EndpointRescuers.ResolveDisplayName(responder.Profile!, responder.User!);
        var session = await services.SessionStore.JoinAsync(
            new RescueSessionJoinCommand(help.Id, responder.User!.OpenId, displayName, now),
            cancellationToken);

        if (isNewParticipant)
        {
            // 新参与者：扇出 joined 事件与参与者对象（ADR 0001，仅提示，权威数据以 HTTP 为准）
            var joined = session.Messages
                .Where(m => m.Kind == RescueMessageKinds.StatusEvent
                    && m.EventType == RescueEventTypes.Joined
                    && m.SenderOpenId == responder.User!.OpenId)
                .MaxBy(m => m.Seq);
            if (joined is not null)
            {
                await broadcaster.BroadcastAsync(help.Id, "message", RescueSessionMapper.ToMessage(joined));
            }
            await broadcaster.BroadcastAsync(
                help.Id,
                "participant",
                FindParticipant(session, responder.User!.OpenId, rescueOptions.Value, now));
        }

        if (help.Status == HelpStatuses.Published)
        {
            help = await services.HelpStore.TransitionToRespondingAsync(help.Id, now, cancellationToken);
            await broadcaster.BroadcastAsync(help.Id, "help_status", new RescueHelpStatusDto(help.Status));
        }

        return Results.Ok(ApiResponse.Ok(RescueSessionMapper.ToSnapshot(
            session,
            help,
            responder.User.OpenId,
            null,
            rescueOptions.Value,
            now)));
    }

    private static async Task<IResult> GetSessionAsync(
        HttpRequest httpRequest,
        string id,
        long? afterSeq,
        IOptions<RescueOptions> rescueOptions,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        var services = new SessionEndpointServices(tokenService, userStore, accountStatus, helpStore, sessionStore);
        var context = await RescueSessionContext.RequireParticipantAsync(httpRequest, id, services, cancellationToken);
        if (context.Failure is not null) return context.Failure;

        var now = DateTimeOffset.UtcNow;
        var snapshot = RescueSessionMapper.ToSnapshot(
            context.Session!,
            context.Help!,
            context.User!.OpenId,
            afterSeq,
            rescueOptions.Value,
            now);
        // 隐式已读：lastReadSeq 推进到当前 latestSeq，响应中的 me 同步反映新值（BackEnd.md §4.5A）
        await services.SessionStore.MarkReadAsync(
            context.Help!.Id,
            context.User.OpenId,
            snapshot.LatestSeq,
            cancellationToken);
        return Results.Ok(ApiResponse.Ok(snapshot with
        {
            Me = snapshot.Me with { LastReadSeq = snapshot.LatestSeq },
        }));
    }

    private static async Task<IResult> PostMessageAsync(
        HttpRequest httpRequest,
        string id,
        SendRescueMessageRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
        RescueSessionBroadcaster broadcaster,
        CancellationToken cancellationToken)
    {
        var services = new SessionEndpointServices(tokenService, userStore, accountStatus, helpStore, sessionStore);
        var context = await RescueSessionContext.RequireParticipantAsync(httpRequest, id, services, cancellationToken);
        if (context.Failure is not null) return context.Failure;

        var text = request.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > MessageTextMaxLength)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "text 需为 1~500 字符", 400);
        }

        var session = await services.SessionStore.AppendMessageAsync(
            new RescueSessionAppendCommand(
                context.Help!.Id,
                RescueMessageKinds.Text,
                context.User!.OpenId,
                context.Participant!.DisplayName,
                text,
                null,
                null,
                DateTimeOffset.UtcNow),
            cancellationToken);
        var message = RescueSessionMapper.ToMessage(LatestMessage(session));
        await broadcaster.BroadcastAsync(context.Help.Id, "message", message);
        return Results.Ok(ApiResponse.Ok(message));
    }

    private static async Task<IResult> PostStatusEventAsync(
        HttpRequest httpRequest,
        string id,
        SendRescueStatusEventRequest request,
        IOptions<RescueOptions> rescueOptions,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
        RescueSessionBroadcaster broadcaster,
        CancellationToken cancellationToken)
    {
        var services = new SessionEndpointServices(tokenService, userStore, accountStatus, helpStore, sessionStore);
        var context = await RescueSessionContext.RequireParticipantAsync(httpRequest, id, services, cancellationToken);
        if (context.Failure is not null) return context.Failure;

        if (request.EventType is not (RescueEventTypes.TakenAed or RescueEventTypes.Arrived)
            || request.Action is not (RescueEventActions.Mark or RescueEventActions.Revoke))
        {
            return EndpointFailures.JsonFailure(
                ErrorCodes.ParameterInvalid,
                "eventType 仅支持 taken_aed / arrived，action 仅支持 mark / revoke",
                400);
        }

        var states = RescueSessionProjection.FoldParticipantStates(context.Session!);
        var current = states[context.User!.OpenId];
        var isMarked = request.EventType == RescueEventTypes.TakenAed ? current.TakenAed : current.Arrived;
        if (isMarked == (request.Action == RescueEventActions.Mark))
        {
            return Results.Ok(ApiResponse.Ok(new RescueStatusEventResponse(
                false,
                null,
                new RescueMyStateDto(context.User.OpenId, current.TakenAed, current.Arrived))));
        }

        var session = await services.SessionStore.AppendMessageAsync(
            new RescueSessionAppendCommand(
                context.Help!.Id,
                RescueMessageKinds.StatusEvent,
                context.User.OpenId,
                context.Participant!.DisplayName,
                null,
                request.EventType,
                request.Action,
                DateTimeOffset.UtcNow),
            cancellationToken);
        var updated = RescueSessionProjection.FoldParticipantStates(session)[context.User.OpenId];
        var eventMessage = RescueSessionMapper.ToMessage(LatestMessage(session));
        await broadcaster.BroadcastAsync(context.Help.Id, "message", eventMessage);
        await broadcaster.BroadcastAsync(
            context.Help.Id,
            "participant",
            FindParticipant(session, context.User.OpenId, rescueOptions.Value, DateTimeOffset.UtcNow));
        return Results.Ok(ApiResponse.Ok(new RescueStatusEventResponse(
            true,
            eventMessage,
            new RescueMyStateDto(context.User.OpenId, updated.TakenAed, updated.Arrived))));
    }

    private static async Task<IResult> ReportLocationAsync(
        HttpRequest httpRequest,
        string id,
        ReportRescueLocationRequest request,
        IOptions<RescueOptions> rescueOptions,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
        RescueSessionBroadcaster broadcaster,
        CancellationToken cancellationToken)
    {
        var services = new SessionEndpointServices(tokenService, userStore, accountStatus, helpStore, sessionStore);
        var context = await RescueSessionContext.RequireParticipantAsync(httpRequest, id, services, cancellationToken);
        if (context.Failure is not null) return context.Failure;

        if (request.Lat is < -90 or > 90 || request.Lng is < -180 or > 180)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "lat/lng 不合法", 400);
        }

        var now = DateTimeOffset.UtcNow;
        var session = await services.SessionStore.UpdateLocationAsync(
            new RescueSessionLocationCommand(
                context.Help!.Id,
                context.User!.OpenId,
                request.Lat,
                request.Lng,
                now),
            cancellationToken);
        await broadcaster.BroadcastAsync(
            context.Help.Id,
            "participant",
            FindParticipant(session, context.User.OpenId, rescueOptions.Value, now));
        var me = session.Participants.First(p => p.OpenId == context.User.OpenId);
        return Results.Ok(ApiResponse.Ok(new ReportRescueLocationResponse(me.LocationReportedAt!.Value)));
    }

    private static RescueSessionMessageRecord LatestMessage(RescueSessionRecord session)
    {
        return session.Messages.OrderByDescending(message => message.Seq).First();
    }

    private static RescueParticipantDto FindParticipant(
        RescueSessionRecord session,
        string openId,
        RescueOptions options,
        DateTimeOffset now)
    {
        return RescueSessionMapper.ToParticipants(session, options, now).First(p => p.OpenId == openId);
    }
}
