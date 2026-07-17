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

        // 求助人本人或认证急救员可见（BackEnd.md §4.5A rescue-view 权限）
        if (help.UserId != current.User!.OpenId)
        {
            var profile = await responderStore.FindByBoundOpenIdAsync(current.User.OpenId, cancellationToken);
            if (profile?.QualificationStatus != EndpointRescuers.ActiveQualificationStatus)
            {
                return EndpointFailures.JsonFailure(ErrorCodes.Forbidden, "仅求助人本人或认证急救员可查看", 403);
            }
        }

        var session = await services.SessionStore.FindByHelpIdAsync(help.Id, cancellationToken);
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

        var sessions = await services.SessionStore.ListByParticipantAsync(responder.User!.OpenId, cancellationToken);
        var helps = (await services.HelpStore.ListAllAsync(cancellationToken)).ToDictionary(h => h.Id);
        var items = sessions
            .Where(s => helps.TryGetValue(s.HelpRequestId, out var help) && HelpStatuses.Active.Contains(help.Status))
            .Select(s => ToMineItem(s, helps[s.HelpRequestId], responder.User.OpenId))
            .OrderByDescending(item => item.LastActivityAt ?? item.JoinedAt)
            .ToArray();
        return Results.Ok(ApiResponse.Ok(new RescueMineResponse(items)));
    }

    private static RescueMineItemDto ToMineItem(
        RescueSessionRecord session,
        HelpRequestRecord help,
        string openId)
    {
        var participant = session.Participants.First(p => p.OpenId == openId);
        return new RescueMineItemDto(
            help.Id,
            help.RequestNo,
            help.Status,
            help.Type,
            help.Address,
            participant.JoinedAt,
            RescueSessionProjection.UnreadCount(session, openId, participant.LastReadSeq),
            session.Messages.Count == 0 ? null : session.Messages.Max(m => m.CreatedAt));
    }
}
