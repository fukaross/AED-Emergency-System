using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.WeChat;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Api.Endpoints;

public static class WeChatSubscriptionEndpoints
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.Ordinal)
    {
        "accept",
        "acceptWithAudio",
        "reject",
        "ban",
        "filter"
    };

    public static IEndpointRouteBuilder MapWeChatSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/wechat")
            .WithTags("WeChat");

        group.MapPost("/subscriptions", SaveSubscriptionAsync)
            .WithName("SaveWeChatSubscription");

        return app;
    }

    private static async Task<IResult> SaveSubscriptionAsync(
        HttpRequest httpRequest,
        SaveSubscribeDecisionRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore store,
        CancellationToken cancellationToken)
    {
        var token = EndpointSecurity.ReadBearerToken(httpRequest);
        if (string.IsNullOrWhiteSpace(token))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.Unauthorized, "缺少登录 token", 401);
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validationError, 400);
        }

        var tokenHash = tokenService.HashToken(token);
        var user = await store.FindByTokenHashAsync(tokenHash, cancellationToken);
        if (user is null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.Unauthorized, "登录 token 无效", 401);
        }

        await store.SaveSubscriptionAsync(tokenHash, request, cancellationToken);
        return Results.Ok(ApiResponse.Ok(new { request.TemplateId, request.Status }));
    }

    private static string? ValidateRequest(SaveSubscribeDecisionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateId))
        {
            return "templateId 不能为空";
        }

        return ValidStatuses.Contains(request.Status)
            ? null
            : "status 仅支持 accept / acceptWithAudio / reject / ban / filter";
    }
}
