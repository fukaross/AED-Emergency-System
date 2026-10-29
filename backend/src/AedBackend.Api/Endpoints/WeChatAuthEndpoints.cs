using System.Text.RegularExpressions;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.WeChat;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Demo;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

public static class WeChatAuthEndpoints
{
    private const int SessionExpiresIn = 30 * 24 * 3600;

    private static readonly Regex MainlandPhonePattern = new(
        @"^1[3-9]\d{9}$",
        RegexOptions.Compiled);

    public static IEndpointRouteBuilder MapWeChatAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth");

        group.MapPost("/wechat-login", LoginAsync)
            .WithName("WeChatLogin");

        group.MapPost("/wechat-phone", BindPhoneAsync)
            .WithName("BindWeChatPhone");

        group.MapPost("/refresh", RefreshAsync)
            .WithName("AuthRefresh");

        return app;
    }

    private static async Task<IResult> LoginAsync(
        WeChatLoginRequest request,
        IOptions<WeChatOptions> options,
        IOptions<DemoOptions> demoOptions,
        IWeChatLoginClient loginClient,
        ISessionTokenService tokenService,
        IExperienceUserStore store,
        IUserAccountStatusService accountStatus,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateLoginRequest(request, options.Value, demoOptions.Value.Enabled);
        if (validationError is not null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validationError, 400);
        }

        var codeSession = await loginClient.CodeToSessionAsync(request.Code, cancellationToken);
        if (!codeSession.Succeeded || codeSession.Session is null)
        {
            return ToCodeSessionFailure(codeSession.ErrorCode, codeSession.ErrorMessage);
        }

        if (await accountStatus.IsDisabledAsync(codeSession.Session.OpenId, cancellationToken))
        {
            return EndpointUsers.AccountDisabled();
        }

        var token = tokenService.CreateToken();
        var tokenHash = tokenService.HashToken(token);
        var result = await store.SaveLoginAsync(codeSession.Session, tokenHash, cancellationToken);

        var response = new WeChatLoginResponse(
            token,
            result.User.OpenId,
            result.IsNewUser,
            SessionExpiresIn,
            result.User.Phone);

        return Results.Ok(ApiResponse.Ok(response));
    }

    private static async Task<IResult> RefreshAsync(
        HttpRequest httpRequest,
        ISessionTokenService tokenService,
        IExperienceUserStore store,
        IUserAccountStatusService accountStatus,
        CancellationToken cancellationToken)
    {
        var oldToken = EndpointSecurity.ReadBearerToken(httpRequest);
        if (string.IsNullOrWhiteSpace(oldToken))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.Unauthorized, "缺少登录凭证", 401);
        }

        var oldTokenHash = tokenService.HashToken(oldToken);
        var current = await store.FindByTokenHashAsync(oldTokenHash, cancellationToken);
        if (current is null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.Unauthorized, "Token 无效或已过期", 401);
        }

        if (await accountStatus.IsDisabledAsync(current.OpenId, cancellationToken))
        {
            return EndpointUsers.AccountDisabled();
        }

        var newToken = tokenService.CreateToken();
        var newTokenHash = tokenService.HashToken(newToken);
        var result = await store.RefreshTokenAsync(oldTokenHash, newTokenHash, cancellationToken);
        if (result is null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.Unauthorized, "Token 无效或已过期", 401);
        }

        var response = new WeChatLoginResponse(
            newToken,
            result.User.OpenId,
            false,
            SessionExpiresIn,
            result.User.Phone);

        return Results.Ok(ApiResponse.Ok(response));
    }

    private static async Task<IResult> BindPhoneAsync(
        HttpRequest httpRequest,
        BindWeChatPhoneRequest request,
        IOptions<WeChatOptions> options,
        IOptions<DemoOptions> demoOptions,
        IWeChatPhoneNumberClient phoneClient,
        ISessionTokenService tokenService,
        IExperienceUserStore store,
        IUserAccountStatusService accountStatus,
        CancellationToken cancellationToken)
    {
        var current = await EndpointUsers.RequireActiveUserAsync(
            httpRequest,
            new ActiveUserDependencies(tokenService, store, accountStatus),
            cancellationToken);
        if (current.Failure is not null) return current.Failure;

        // 演示手机号只来自 demo/roles.json 的固定角色，不开放任意号码绑定/修改入口。
        if (demoOptions.Value.Enabled)
        {
            return EndpointFailures.JsonFailure(
                ErrorCodes.Forbidden,
                "演示模式手机号来自固定演示角色，不支持绑定或修改",
                403);
        }

        var validationError = ValidatePhoneRequest(request, options.Value);
        if (validationError is not null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validationError, 400);
        }

        var tokenHash = tokenService.HashToken(EndpointSecurity.ReadBearerToken(httpRequest)!);
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            return await SavePhoneAsync(request.Phone.Trim(), tokenHash, store, cancellationToken);
        }

        var phoneResult = await phoneClient.GetPhoneNumberAsync(request.Code!, cancellationToken);
        if (!phoneResult.Succeeded || phoneResult.PhoneNumber is null)
        {
            return ToPhoneNumberFailure(phoneResult.ErrorCode, phoneResult.ErrorMessage);
        }

        return await SavePhoneAsync(
            phoneResult.PhoneNumber,
            tokenHash,
            store,
            cancellationToken);
    }

    private static async Task<IResult> SavePhoneAsync(
        string phone,
        string tokenHash,
        IExperienceUserStore store,
        CancellationToken cancellationToken)
    {
        var updated = await store.SavePhoneAsync(tokenHash, phone, cancellationToken);
        var response = new BindWeChatPhoneResponse(updated.OpenId, updated.Phone!);
        return Results.Ok(ApiResponse.Ok(response));
    }

    /**
     * 演示模式使用固定 code → 角色映射，由 IWeChatLoginClient 的演示适配器处理，
     * 因此不要求真实 AppId/AppSecret；真实模式仍必须在进入登录客户端前检查凭据，
     * 缺失时给明确配置错误而不是自动切 Mock。
     */
    private static string? ValidateLoginRequest(
        WeChatLoginRequest request,
        WeChatOptions options,
        bool demoEnabled)
    {
        if (!demoEnabled
            && (string.IsNullOrWhiteSpace(options.AppId) || string.IsNullOrWhiteSpace(options.AppSecret)))
        {
            return "缺少 WeChat:AppId 或 WeChat:AppSecret 配置";
        }

        return string.IsNullOrWhiteSpace(request.Code) ? "code 不能为空" : null;
    }

    private static string? ValidatePhoneRequest(
        BindWeChatPhoneRequest request,
        WeChatOptions options)
    {
        var hasCode = !string.IsNullOrWhiteSpace(request.Code);
        var hasPhone = !string.IsNullOrWhiteSpace(request.Phone);

        if (!hasCode && !hasPhone)
        {
            return "code 或 phone 不能为空";
        }

        if (hasCode && hasPhone)
        {
            return "code 与 phone 只能选择一种";
        }

        if (hasCode &&
            (string.IsNullOrWhiteSpace(options.AppId) || string.IsNullOrWhiteSpace(options.AppSecret)))
        {
            return "缺少 WeChat:AppId 或 WeChat:AppSecret 配置";
        }

        if (hasPhone && !MainlandPhonePattern.IsMatch(request.Phone!.Trim()))
        {
            return "手机号格式不正确";
        }

        return null;
    }

    private static IResult ToCodeSessionFailure(int errorCode, string errorMessage)
    {
        var apiCode = errorCode == 40029 ? ErrorCodes.ParameterInvalid : ErrorCodes.InternalServerError;
        var statusCode = errorCode == 40029 ? 400 : 502;
        var message = $"微信登录失败：{errorCode} {errorMessage}";

        return EndpointFailures.JsonFailure(apiCode, message, statusCode);
    }

    private static IResult ToPhoneNumberFailure(int errorCode, string errorMessage)
    {
        var apiCode = errorCode == 40029 ? ErrorCodes.ParameterInvalid : ErrorCodes.InternalServerError;
        var statusCode = errorCode == 40029 ? 400 : 502;
        var message = $"微信手机号获取失败：{errorCode} {errorMessage}";

        return EndpointFailures.JsonFailure(apiCode, message, statusCode);
    }
}
