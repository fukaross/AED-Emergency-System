using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Rescue;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

internal static class RescueSessionQueryEndpoints
{
    public static IEndpointRouteBuilder MapRescueSessionQueryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/help")
            .WithTags("Rescue")
            .MapGet("/{id}/rescue-view", GetRescueViewAsync)
            .WithName("GetRescueView");
        app.MapGroup("/api/rescue-sessions")
            .WithTags("Rescue")
            .MapGet("/mine", ListMineAsync)
            .WithName("ListMyRescueSessions");
        return app;
    }

    private static async Task<IResult> GetRescueViewAsync(
        HttpRequest httpRequest,
        string id,
        IOptions<RescueOptions> rescueOptions,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        IResponderStore responderStore,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        var services = new SessionEndpointServices(tokenService, userStore, accountStatus, helpStore, sessionStore);
        var current = await EndpointUsers.RequireActiveUserAsync(
            httpRequest,
            services.Tokens,
            services.Users,
            services.AccountStatus,
            cancellationToken);
        if (current.Failure is not null) return current.Failure;

        var help = await services.HelpStore.FindByIdOrRequestNoAsync(id, cancellationToken);
        if (help is null) return RescueSessionContext.NotFound();
        if (HelpStatuses.IsClosed(help.Status)) return RescueSessionContext.SessionClosed();

        var session = await services.SessionStore.FindByHelpIdAsync(help.Id, cancellationToken);
        if (session?.ClosedAt is not null) return RescueSessionContext.SessionClosed();
        var isRequester = help.UserId == current.User!.OpenId;
        var isParticipant = session?.Participants.Any(
            participant => participant.OpenId == current.User.OpenId) == true;
        if (!isRequester && !isParticipant)
        {
            var profile = await responderStore.FindByBoundOpenIdAsync(current.User.OpenId, cancellationToken);
            if (profile?.QualificationStatus == EndpointRescuers.ActiveQualificationStatus)
            {
                return EndpointFailures.JsonFailure(
                    ErrorCodes.RescueNotParticipant,
                    "请先加入急救会话",
                    403);
            }
            return EndpointFailures.JsonFailure(
                ErrorCodes.Forbidden,
                "仅求助人本人或已加入会话的急救员可查看",
                403);
        }

        var now = DateTimeOffset.UtcNow;
        return Results.Ok(ApiResponse.Ok(RescueSessionMapper.ToRescueView(help, session, rescueOptions.Value, now)));
    }

    private static async Task<IResult> ListMineAsync(
        HttpRequest httpRequest,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        IResponderStore responderStore,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
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

        var sessions = (await services.SessionStore.ListOverviewsAsync(responder.User!.OpenId, cancellationToken))
            .ToDictionary(session => session.HelpRequestId);
        var items = (await services.HelpStore.ListAllAsync(cancellationToken))
            .Where(help => HelpStatuses.Active.Contains(help.Status))
            .Select(help => new
            {
                Item = ToMineItem(sessions.GetValueOrDefault(help.Id), help),
                help.CreatedAt,
            })
            .OrderByDescending(candidate => candidate.Item.UnreadCount > 0)
            .ThenByDescending(candidate => candidate.Item.LastActivityAt ?? candidate.CreatedAt)
            .Select(candidate => candidate.Item)
            .ToArray();
        return Results.Ok(ApiResponse.Ok(new RescueMineResponse(items)));
    }

    private static RescueMineItemDto ToMineItem(
        RescueSessionOverviewRecord? session,
        HelpRequestRecord help)
    {
        var viewer = session?.Viewer;
        return new RescueMineItemDto(
            help.Id,
            help.RequestNo,
            help.Status,
            help.Type,
            help.Address,
            viewer is not null,
            viewer?.JoinedAt,
            viewer?.UnreadCount ?? 0,
            session?.LastActivityAt);
    }
}
