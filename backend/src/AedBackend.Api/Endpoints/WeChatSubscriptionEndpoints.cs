using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.WeChat;

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
        [AsParameters] WeChatSubscriptionEndpointRequest endpoint)
    {
        var current = await EndpointUsers.RequireActiveUserAsync(
            endpoint.HttpRequest,
            endpoint.ActiveUserDependencies,
            endpoint.CancellationToken);
        if (current.Failure is not null) return current.Failure;

        var validationError = ValidateRequest(endpoint.Body);
        if (validationError is not null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validationError, 400);
        }

        var token = EndpointSecurity.ReadBearerToken(endpoint.HttpRequest)!;
        var tokenHash = endpoint.TokenService.HashToken(token);
        await endpoint.UserStore.SaveSubscriptionAsync(
            tokenHash,
            endpoint.Body,
            endpoint.CancellationToken);
        return Results.Ok(ApiResponse.Ok(new
        {
            endpoint.Body.TemplateId,
            endpoint.Body.Status
        }));
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
