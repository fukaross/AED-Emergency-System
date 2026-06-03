using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Api.Endpoints;

internal static class EndpointUsers
{
    public static async Task<ExperienceUserRecord?> FindCurrentUserAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore store,
        CancellationToken cancellationToken)
    {
        var token = EndpointSecurity.ReadBearerToken(request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return await store.FindByTokenHashAsync(
            tokenService.HashToken(token),
            cancellationToken);
    }

    public static async Task<CurrentUserResult> RequireActiveUserAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore store,
        IUserAccountStatusService accountStatus,
        CancellationToken cancellationToken)
    {
        var user = await FindCurrentUserAsync(request, tokenService, store, cancellationToken);
        if (user is null) return CurrentUserResult.Unauthorized();
        return await accountStatus.IsDisabledAsync(user.OpenId, cancellationToken)
            ? CurrentUserResult.Forbidden()
            : CurrentUserResult.Success(user);
    }

    public static IResult Unauthorized()
    {
        return EndpointFailures.JsonFailure(ErrorCodes.Unauthorized, "登录 token 无效", 401);
    }

    public static IResult AccountDisabled()
    {
        return EndpointFailures.JsonFailure(ErrorCodes.Forbidden, "账号已禁用", 403);
    }
}

public sealed record CurrentUserResult(
    ExperienceUserRecord? User,
    IResult? Failure)
{
    public static CurrentUserResult Success(ExperienceUserRecord user)
    {
        return new CurrentUserResult(user, null);
    }

    public static CurrentUserResult Unauthorized()
    {
        return new CurrentUserResult(null, EndpointUsers.Unauthorized());
    }

    public static CurrentUserResult Forbidden()
    {
        return new CurrentUserResult(null, EndpointUsers.AccountDisabled());
    }
}
