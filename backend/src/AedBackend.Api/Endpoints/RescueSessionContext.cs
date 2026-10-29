using AedBackend.Core.Constants;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;

namespace AedBackend.Api.Endpoints;

/** 会话端点常用服务集合，避免私有 helper 参数逐个透传。 */
internal sealed record SessionEndpointServices
{
    public required RescueSessionAuthorizer Authorizer { get; init; }
    public required IHelpRequestStore HelpStore { get; init; }
    public required IRescueSessionStore SessionStore { get; init; }
    public required RescueSessionLifecycleCoordinator Lifecycle { get; init; }
}

internal sealed record ParticipantContextResult
{
    public HelpRequestRecord? Help { get; init; }
    public RescueSessionRecord? Session { get; init; }
    public RescueSessionParticipantRecord? Participant { get; init; }
    public ExperienceUserRecord? User { get; init; }
    public string? TokenHash { get; init; }
    public IResult? Failure { get; init; }
    public int FailureStatusCode { get; init; }

    public static ParticipantContextResult Failed(IResult failure, int statusCode)
    {
        return new ParticipantContextResult
        {
            Failure = failure,
            FailureStatusCode = statusCode
        };
    }
}

internal static class RescueSessionContext
{
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
