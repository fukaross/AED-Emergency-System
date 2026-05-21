using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.WeChat;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

public static class WeChatDevEndpoints
{
    public static IEndpointRouteBuilder MapWeChatDevEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dev/wechat")
            .WithTags("WeChat Dev");

        group.MapGet("/experience-users", ListExperienceUsersAsync)
            .WithName("ListExperienceUsers");

        group.MapPost("/subscribe-message/send", SendSubscribeMessageAsync)
            .WithName("SendDevSubscribeMessage");

        group.MapPost("/subscribe-message/broadcast", BroadcastSubscribeMessageAsync)
            .WithName("BroadcastDevSubscribeMessage");

        return app;
    }

    private static async Task<IResult> ListExperienceUsersAsync(
        HttpRequest httpRequest,
        IOptions<WeChatOptions> options,
        IExperienceUserStore store,
        CancellationToken cancellationToken)
    {
        var authFailure = ValidateAdmin(httpRequest, options.Value);
        if (authFailure is not null)
        {
            return authFailure;
        }

        var users = await store.ListUsersAsync(cancellationToken);
        var summaries = users.Select(CreateSummary).ToArray();
        return Results.Ok(ApiResponse.Ok(summaries));
    }

    private static async Task<IResult> SendSubscribeMessageAsync(
        HttpRequest httpRequest,
        SendSubscribeMessageRequest request,
        IOptions<WeChatOptions> options,
        IWeChatSubscribeMessageSender sender,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var authFailure = ValidateAdmin(httpRequest, settings);
        if (authFailure is not null)
        {
            return authFailure;
        }

        var validationError = ValidateRequest(request, settings);
        if (validationError is not null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validationError, 400);
        }

        var command = CreateCommand(request, settings);
        var result = await sender.SendAsync(command, cancellationToken);
        return ToSendResult(result);
    }

    private static async Task<IResult> BroadcastSubscribeMessageAsync(
        HttpRequest httpRequest,
        BroadcastSubscribeMessageRequest request,
        IOptions<WeChatOptions> options,
        IExperienceUserStore store,
        IWeChatSubscribeMessageSender sender,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var authFailure = ValidateAdmin(httpRequest, settings);
        if (authFailure is not null)
        {
            return authFailure;
        }

        var validationError = ValidateBroadcastRequest(request, settings);
        if (validationError is not null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validationError, 400);
        }

        var users = await store.ListUsersAsync(cancellationToken);
        var targets = SelectBroadcastTargets(users, request.TemplateId ?? settings.SubscribeTemplateId);
        var recipients = await SendBroadcastAsync(targets, request, settings, sender, cancellationToken);
        var result = new BroadcastSubscribeMessageResult(targets.Count, recipients);

        return Results.Ok(ApiResponse.Ok(result));
    }

    private static IResult? ValidateAdmin(HttpRequest request, WeChatOptions options)
    {
        if (!EndpointSecurity.HasAdminKey(options))
        {
            return EndpointFailures.JsonFailure(
                ErrorCodes.InternalServerError,
                "缺少 WeChat:AdminKey 配置",
                500);
        }

        return EndpointSecurity.IsAdminAuthorized(request, options)
            ? null
            : EndpointFailures.JsonFailure(ErrorCodes.Unauthorized, "缺少有效的推送调试密钥", 401);
    }

    private static string? ValidateRequest(SendSubscribeMessageRequest request, WeChatOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AppId) || string.IsNullOrWhiteSpace(options.AppSecret))
        {
            return "缺少 WeChat:AppId 或 WeChat:AppSecret 配置";
        }

        if (string.IsNullOrWhiteSpace(request.OpenId))
        {
            return "openId 不能为空";
        }

        if (string.IsNullOrWhiteSpace(request.TemplateId) && string.IsNullOrWhiteSpace(options.SubscribeTemplateId))
        {
            return "templateId 不能为空，或配置 WeChat:SubscribeTemplateId";
        }

        if (request.Data.Count == 0)
        {
            return "data 不能为空";
        }

        return ValidateMiniProgramState(request.MiniProgramState ?? options.DefaultMiniProgramState);
    }

    private static string? ValidateMiniProgramState(string? state)
    {
        return state is "developer" or "trial" or "formal"
            ? null
            : "miniprogramState 仅支持 developer / trial / formal";
    }

    private static string? ValidateBroadcastRequest(
        BroadcastSubscribeMessageRequest request,
        WeChatOptions options)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateId) && string.IsNullOrWhiteSpace(options.SubscribeTemplateId))
        {
            return "templateId 不能为空，或配置 WeChat:SubscribeTemplateId";
        }

        if (request.Data.Count == 0)
        {
            return "data 不能为空";
        }

        return ValidateMiniProgramState(request.MiniProgramState ?? options.DefaultMiniProgramState);
    }

    private static SubscribeMessageCommand CreateCommand(
        SendSubscribeMessageRequest request,
        WeChatOptions options)
    {
        return new SubscribeMessageCommand(
            request.OpenId,
            request.TemplateId ?? options.SubscribeTemplateId,
            request.Page,
            request.MiniProgramState ?? options.DefaultMiniProgramState,
            request.Data);
    }

    private static SubscribeMessageCommand CreateCommand(
        string openId,
        BroadcastSubscribeMessageRequest request,
        WeChatOptions options)
    {
        return new SubscribeMessageCommand(
            openId,
            request.TemplateId ?? options.SubscribeTemplateId,
            request.Page,
            request.MiniProgramState ?? options.DefaultMiniProgramState,
            request.Data);
    }

    private static IResult ToSendResult(SendSubscribeMessageResult result)
    {
        if (result.Sent)
        {
            return Results.Ok(ApiResponse.Ok(result));
        }

        var apiCode = result.WeChatErrorCode == WeChatErrorCodes.UserRefused
            ? ErrorCodes.SubscribeMessageNotGranted
            : ErrorCodes.InternalServerError;

        var statusCode = result.WeChatErrorCode == WeChatErrorCodes.UserRefused ? 409 : 502;
        var response = new ApiResponse<SendSubscribeMessageResult>(
            apiCode,
            result.WeChatErrorMessage,
            result);

        return Results.Json(response, statusCode: statusCode);
    }

    private static ExperienceUserSummary CreateSummary(ExperienceUserRecord user)
    {
        var subscriptions = user.Subscriptions.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Status);

        return new ExperienceUserSummary(
            user.OpenId,
            user.Phone,
            user.CreatedAt,
            user.LastLoginAt,
            subscriptions);
    }

    private static IReadOnlyList<ExperienceUserRecord> SelectBroadcastTargets(
        IReadOnlyList<ExperienceUserRecord> users,
        string templateId)
    {
        return users
            .Where(user => IsTemplateAccepted(user, templateId))
            .ToArray();
    }

    private static bool IsTemplateAccepted(ExperienceUserRecord user, string templateId)
    {
        return user.Subscriptions.TryGetValue(templateId, out var subscription)
            && subscription.Status is "accept" or "acceptWithAudio";
    }

    private static async Task<IReadOnlyList<BroadcastRecipientResult>> SendBroadcastAsync(
        IReadOnlyList<ExperienceUserRecord> targets,
        BroadcastSubscribeMessageRequest request,
        WeChatOptions options,
        IWeChatSubscribeMessageSender sender,
        CancellationToken cancellationToken)
    {
        var recipients = new List<BroadcastRecipientResult>();

        foreach (var target in targets)
        {
            var command = CreateCommand(target.OpenId, request, options);
            var result = await sender.SendAsync(command, cancellationToken);
            recipients.Add(CreateBroadcastResult(target.OpenId, result));
        }

        return recipients;
    }

    private static BroadcastRecipientResult CreateBroadcastResult(
        string openId,
        SendSubscribeMessageResult result)
    {
        return new BroadcastRecipientResult(
            openId,
            result.Sent,
            result.WeChatErrorCode,
            result.WeChatErrorMessage,
            result.MessageId);
    }
}
