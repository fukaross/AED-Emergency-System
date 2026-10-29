using AedBackend.Api.WebSockets;
using AedBackend.Core.Constants;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Api.Endpoints;

internal sealed record RescueViewContextResult
{
    public HelpRequestRecord? Help { get; init; }
    public RescueSessionRecord? Session { get; init; }
    public ExperienceUserRecord? User { get; init; }
    public IResult? Failure { get; init; }

    public static RescueViewContextResult Success(
        HelpRequestRecord help,
        RescueSessionRecord? session,
        ExperienceUserRecord user) => new()
        {
            Help = help,
            Session = session,
            User = user
        };

    public static RescueViewContextResult Failed(IResult failure) => new() { Failure = failure };
}

internal sealed record RescueAuthenticatedUser
{
    public ExperienceUserRecord? User { get; init; }
    public string? TokenHash { get; init; }
    public IResult? Failure { get; init; }
    public int FailureStatusCode { get; init; }

    public static RescueAuthenticatedUser Success(ExperienceUserRecord user, string tokenHash) =>
        new()
        {
            User = user,
            TokenHash = tokenHash,
            FailureStatusCode = StatusCodes.Status200OK
        };

    public static RescueAuthenticatedUser Failed(IResult failure, int statusCode) =>
        new() { Failure = failure, FailureStatusCode = statusCode };
}

internal sealed record RescueAuthorizationDependencies
{
    public required ISessionTokenService Tokens { get; init; }
    public required IExperienceUserStore Users { get; init; }
    public required IUserAccountStatusService AccountStatus { get; init; }
    public required IResponderStore Responders { get; init; }
    public required IHelpRequestStore Helps { get; init; }
    public required IRescueSessionStore Sessions { get; init; }
}

/**
 * 急救会话统一授权器：HTTP 与已建立 WS 均按当前 token、账号状态、资格和参与关系判定。
 */
internal sealed class RescueSessionAuthorizer : IRescueConnectionAuthorizer
{
    private readonly RescueAuthorizationDependencies dependencies;

    public RescueSessionAuthorizer(RescueAuthorizationDependencies dependencies)
    {
        this.dependencies = dependencies;
    }

    /** 验证当前请求是否来自资格仍有效的认证急救员。 */
    public async Task<CertifiedResponderResult> RequireCertifiedResponderAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authenticated = await AuthenticateAsync(request, cancellationToken);
        if (authenticated.Failure is not null)
            return CertifiedResponderResult.Failed(authenticated.Failure);

        var profile = await FindActiveProfileAsync(authenticated.User!.OpenId, cancellationToken);
        return profile is null
            ? CertifiedResponderResult.Failed(CertifiedResponderRequired())
            : CertifiedResponderResult.Success(authenticated.User, profile);
    }

    /** 验证当前请求是否仍是指定活动会话中的认证参与者。 */
    public async Task<ParticipantContextResult> RequireParticipantAsync(
        HttpRequest request,
        string helpId,
        CancellationToken cancellationToken)
    {
        var authenticated = await AuthenticateAsync(request, cancellationToken);
        if (authenticated.Failure is not null)
            return ParticipantContextResult.Failed(authenticated.Failure, authenticated.FailureStatusCode);
        if (await FindActiveProfileAsync(authenticated.User!.OpenId, cancellationToken) is null)
            return ParticipantContextResult.Failed(CertifiedResponderRequired(), StatusCodes.Status403Forbidden);

        var resource = await FindActiveSessionAsync(helpId, cancellationToken);
        if (resource.Failure is not null)
            return ParticipantContextResult.Failed(resource.Failure, resource.FailureStatusCode);
        var participant = resource.Session?.Participants.FirstOrDefault(
            item => item.OpenId == authenticated.User.OpenId);
        if (participant is null)
            return ParticipantContextResult.Failed(
                RescueSessionContext.NotParticipant(),
                StatusCodes.Status403Forbidden);
        return new ParticipantContextResult
        {
            Help = resource.Help,
            Session = resource.Session,
            Participant = participant,
            User = authenticated.User,
            TokenHash = authenticated.TokenHash,
            FailureStatusCode = StatusCodes.Status200OK
        };
    }

    /** 求助人可只读查看；其他用户必须同时保持认证资格和会话参与关系。 */
    public async Task<RescueViewContextResult> RequireRescueViewAsync(
        HttpRequest request,
        string helpId,
        CancellationToken cancellationToken)
    {
        var authenticated = await AuthenticateAsync(request, cancellationToken);
        if (authenticated.Failure is not null)
            return RescueViewContextResult.Failed(authenticated.Failure);
        var resource = await FindActiveSessionAsync(helpId, cancellationToken);
        if (resource.Failure is not null) return RescueViewContextResult.Failed(resource.Failure);
        if (resource.Help!.UserId == authenticated.User!.OpenId)
            return RescueViewContextResult.Success(resource.Help, resource.Session, authenticated.User);

        var profile = await FindActiveProfileAsync(authenticated.User.OpenId, cancellationToken);
        if (profile is null) return RescueViewContextResult.Failed(CertifiedResponderViewRequired());
        var joined = resource.Session?.Participants.Any(
            item => item.OpenId == authenticated.User.OpenId) == true;
        return joined
            ? RescueViewContextResult.Success(resource.Help, resource.Session, authenticated.User)
            : RescueViewContextResult.Failed(JoinRequired());
    }

    /** 每次敏感扇出前重新验证连接创建时绑定的原 token 与当前资格。 */
    public async Task<bool> IsAuthorizedAsync(
        RescueSocketAuthorization authorization)
    {
        var cancellationToken = authorization.CancellationToken;
        var identity = authorization.Identity;
        var user = await dependencies.Users.FindByTokenHashAsync(
            identity.TokenHash,
            cancellationToken);
        if (user?.OpenId != identity.OpenId) return false;
        if (await dependencies.AccountStatus.IsDisabledAsync(identity.OpenId, cancellationToken)) return false;
        if (await FindActiveProfileAsync(identity.OpenId, cancellationToken) is null) return false;

        var help = await dependencies.Helps.FindByIdOrRequestNoAsync(
            authorization.HelpId,
            cancellationToken);
        if (help is null
            || (!authorization.AllowClosedSession && HelpStatuses.IsClosed(help.Status))) return false;
        var access = await dependencies.Sessions.FindParticipantAccessAsync(
            new RescueSessionParticipantQuery
            {
                HelpRequestId = help.Id,
                OpenId = identity.OpenId,
            },
            cancellationToken);
        return access is not null
            && (authorization.AllowClosedSession || access.ClosedAt is null);
    }

    private async Task<RescueAuthenticatedUser> AuthenticateAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var token = EndpointSecurity.ReadBearerToken(request);
        if (string.IsNullOrWhiteSpace(token))
            return RescueAuthenticatedUser.Failed(EndpointUsers.Unauthorized(), StatusCodes.Status401Unauthorized);
        var tokenHash = dependencies.Tokens.HashToken(token);
        var user = await dependencies.Users.FindByTokenHashAsync(tokenHash, cancellationToken);
        if (user is null)
            return RescueAuthenticatedUser.Failed(EndpointUsers.Unauthorized(), StatusCodes.Status401Unauthorized);
        return await dependencies.AccountStatus.IsDisabledAsync(user.OpenId, cancellationToken)
            ? RescueAuthenticatedUser.Failed(EndpointUsers.AccountDisabled(), StatusCodes.Status403Forbidden)
            : RescueAuthenticatedUser.Success(user, tokenHash);
    }

    private async Task<ResponderProfileRecord?> FindActiveProfileAsync(
        string openId,
        CancellationToken cancellationToken)
    {
        var profile = await dependencies.Responders.FindByBoundOpenIdAsync(openId, cancellationToken);
        return profile?.QualificationStatus == EndpointRescuers.ActiveQualificationStatus ? profile : null;
    }

    private async Task<ActiveSessionResult> FindActiveSessionAsync(
        string helpId,
        CancellationToken cancellationToken)
    {
        var help = await dependencies.Helps.FindByIdOrRequestNoAsync(helpId, cancellationToken);
        if (help is null) return ActiveSessionResult.Failed(RescueSessionContext.NotFound(), 404);
        if (HelpStatuses.IsClosed(help.Status))
            return ActiveSessionResult.Failed(RescueSessionContext.SessionClosed(), 409);
        var session = await dependencies.Sessions.FindByHelpIdAsync(help.Id, cancellationToken);
        return session?.ClosedAt is not null
            ? ActiveSessionResult.Failed(RescueSessionContext.SessionClosed(), 409)
            : ActiveSessionResult.Success(help, session);
    }

    private static IResult CertifiedResponderRequired() =>
        EndpointFailures.JsonFailure(ErrorCodes.Forbidden, "仅认证急救员可执行该操作", 403);

    private static IResult CertifiedResponderViewRequired() =>
        EndpointFailures.JsonFailure(ErrorCodes.Forbidden, "仅求助人本人或已加入会话的急救员可查看", 403);

    private static IResult JoinRequired() =>
        EndpointFailures.JsonFailure(ErrorCodes.RescueNotParticipant, "请先加入急救会话", 403);

    private sealed record ActiveSessionResult
    {
        public HelpRequestRecord? Help { get; init; }
        public RescueSessionRecord? Session { get; init; }
        public IResult? Failure { get; init; }
        public int FailureStatusCode { get; init; }

        public static ActiveSessionResult Success(HelpRequestRecord help, RescueSessionRecord? session) =>
            new()
            {
                Help = help,
                Session = session,
                FailureStatusCode = StatusCodes.Status200OK
            };

        public static ActiveSessionResult Failed(IResult failure, int statusCode) =>
            new() { Failure = failure, FailureStatusCode = statusCode };
    }
}
