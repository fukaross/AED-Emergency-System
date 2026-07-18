using AedBackend.Core.Constants;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Api.Endpoints;

internal static class EndpointRescuers
{
    public const string ActiveQualificationStatus = "active";

    /**
     * 认证急救员准入判定：有效登录态 + responder_profiles 绑定当前用户且资格 active（BackEnd.md §4.5A）。
     */
    public static async Task<CertifiedResponderResult> RequireCertifiedResponderAsync(
        CertifiedResponderAuthorization authorization)
    {
        var current = await EndpointUsers.RequireActiveUserAsync(
            authorization.Request,
            new ActiveUserDependencies(
                authorization.TokenService,
                authorization.UserStore,
                authorization.AccountStatus),
            authorization.CancellationToken);
        if (current.Failure is not null)
        {
            return CertifiedResponderResult.Failed(current.Failure);
        }

        var profile = await authorization.ResponderStore.FindByBoundOpenIdAsync(
            current.User!.OpenId,
            authorization.CancellationToken);
        return profile?.QualificationStatus == ActiveQualificationStatus
            ? CertifiedResponderResult.Success(current.User, profile)
            : CertifiedResponderResult.Failed(
                EndpointFailures.JsonFailure(ErrorCodes.Forbidden, "仅认证急救员可执行该操作", 403));
    }

    /** 进入会话时的展示名：急救员档案姓名优先，回退用户昵称，兜底「急救员」。 */
    public static string ResolveDisplayName(ResponderProfileRecord profile, ExperienceUserRecord user)
    {
        if (!string.IsNullOrWhiteSpace(profile.Name)) return profile.Name.Trim();
        return string.IsNullOrWhiteSpace(user.Name) ? "急救员" : user.Name.Trim();
    }
}

internal sealed record CertifiedResponderAuthorization
{
    public required HttpRequest Request { get; init; }
    public required ISessionTokenService TokenService { get; init; }
    public required IExperienceUserStore UserStore { get; init; }
    public required IUserAccountStatusService AccountStatus { get; init; }
    public required IResponderStore ResponderStore { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}

public sealed record CertifiedResponderResult(
    ExperienceUserRecord? User,
    ResponderProfileRecord? Profile,
    IResult? Failure)
{
    public static CertifiedResponderResult Success(ExperienceUserRecord user, ResponderProfileRecord profile)
    {
        return new CertifiedResponderResult(user, profile, null);
    }

    public static CertifiedResponderResult Failed(IResult failure)
    {
        return new CertifiedResponderResult(null, null, failure);
    }
}
