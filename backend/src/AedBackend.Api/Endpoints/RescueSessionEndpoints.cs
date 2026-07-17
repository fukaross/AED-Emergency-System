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
        return await RescueSessionJoinFlow.ExecuteAsync(new RescueSessionJoinInput
        {
            HttpRequest = httpRequest,
            HelpId = id,
            Options = rescueOptions.Value,
            ResponderStore = responderStore,
            Services = services,
            Broadcaster = broadcaster,
            CancellationToken = cancellationToken,
        });
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
        var write = await RescueSessionWriteGuard.ExecuteAsync(() =>
            services.SessionStore.MarkReadAsync(
                context.Help!.Id,
                context.User.OpenId,
                snapshot.LatestSeq,
                cancellationToken));
        if (write.Failure is not null) return write.Failure;
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

        var write = await RescueSessionWriteGuard.ExecuteAsync(() =>
            services.SessionStore.AppendMessageAsync(
                new RescueSessionAppendCommand(
                    context.Help!.Id,
                    RescueMessageKinds.Text,
                    context.User!.OpenId,
                    context.Participant!.DisplayName,
                    text,
                    null,
                    null,
                    DateTimeOffset.UtcNow),
                cancellationToken));
        if (write.Failure is not null) return write.Failure;
        var session = write.Value!;
        var message = RescueSessionMapper.ToMessage(LatestMessage(session));
        await broadcaster.BroadcastAsync(context.Help!.Id, "message", message);
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
        return await RescueSessionStatusFlow.ExecuteAsync(new RescueSessionStatusInput
        {
            HttpRequest = httpRequest,
            HelpId = id,
            Request = request,
            Options = rescueOptions.Value,
            Services = services,
            Broadcaster = broadcaster,
            CancellationToken = cancellationToken,
        });
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
        var write = await RescueSessionWriteGuard.ExecuteAsync(() =>
            services.SessionStore.UpdateLocationAsync(
                new RescueSessionLocationCommand(
                    context.Help!.Id,
                    context.User!.OpenId,
                    request.Lat,
                    request.Lng,
                    now),
                cancellationToken));
        if (write.Failure is not null) return write.Failure;
        var session = write.Value!;
        var participant = RescueSessionMapper.ToParticipants(session, rescueOptions.Value, now)
            .First(item => item.OpenId == context.User!.OpenId);
        await broadcaster.BroadcastAsync(
            context.Help!.Id,
            "participant",
            participant);
        var me = session.Participants.First(p => p.OpenId == context.User!.OpenId);
        return Results.Ok(ApiResponse.Ok(new ReportRescueLocationResponse(me.LocationReportedAt!.Value)));
    }

    private static RescueSessionMessageRecord LatestMessage(RescueSessionRecord session)
    {
        return session.Messages.OrderByDescending(message => message.Seq).First();
    }

}
