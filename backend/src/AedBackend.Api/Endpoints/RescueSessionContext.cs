using AedBackend.Core.Constants;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Api.Endpoints;

/** 会话端点常用服务集合，避免私有 helper 参数逐个透传。 */
internal sealed record SessionEndpointServices(
    ISessionTokenService Tokens,
    IExperienceUserStore Users,
    IUserAccountStatusService AccountStatus,
    IHelpRequestStore HelpStore,
    IRescueSessionStore SessionStore);

internal sealed record ParticipantContextResult(
    HelpRequestRecord? Help,
    RescueSessionRecord? Session,
    RescueSessionParticipantRecord? Participant,
    ExperienceUserRecord? User,
    IResult? Failure)
{
    public static ParticipantContextResult Success(
        HelpRequestRecord help,
        RescueSessionRecord session,
        RescueSessionParticipantRecord participant,
        ExperienceUserRecord user)
    {
        return new ParticipantContextResult(help, session, participant, user, null);
    }

    public static ParticipantContextResult Failed(IResult failure)
    {
        return new ParticipantContextResult(null, null, null, null, failure);
    }
}

internal static class RescueSessionContext
{
    /**
     * 会话参与者上下文：登录态 → 求助存在 → 求助未关闭 → 已进入会话，任一不满足返回对应契约错误。
     */
    public static async Task<ParticipantContextResult> RequireParticipantAsync(
        HttpRequest httpRequest,
        string helpId,
        SessionEndpointServices services,
        CancellationToken cancellationToken)
    {
        var current = await EndpointUsers.RequireActiveUserAsync(
            httpRequest,
            services.Tokens,
            services.Users,
            services.AccountStatus,
            cancellationToken);
        if (current.Failure is not null) return ParticipantContextResult.Failed(current.Failure);

        var help = await services.HelpStore.FindByIdOrRequestNoAsync(helpId, cancellationToken);
        if (help is null) return ParticipantContextResult.Failed(NotFound());
        if (HelpStatuses.IsClosed(help.Status)) return ParticipantContextResult.Failed(SessionClosed());

        var session = await services.SessionStore.FindByHelpIdAsync(help.Id, cancellationToken);
        if (session?.ClosedAt is not null) return ParticipantContextResult.Failed(SessionClosed());
        var participant = session?.Participants.FirstOrDefault(p => p.OpenId == current.User!.OpenId);
        return session is null || participant is null
            ? ParticipantContextResult.Failed(NotParticipant())
            : ParticipantContextResult.Success(help, session, participant, current.User!);
    }

    public static IResult NotFound()
    {
        return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "求助不存在", 404);
    }

    public static IResult SessionClosed()
    {
        return EndpointFailures.JsonFailure(ErrorCodes.RescueSessionClosed, "求助已结束，急救会话已关闭", 409);
    }

    public static IResult NotParticipant()
    {
        return EndpointFailures.JsonFailure(ErrorCodes.RescueNotParticipant, "仅会话参与者可执行该操作", 403);
    }
}
