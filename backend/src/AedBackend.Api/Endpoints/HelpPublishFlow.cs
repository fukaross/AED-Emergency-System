using System.Security.Cryptography;
using AedBackend.Core.DTOs.Help;
using AedBackend.Core.DTOs.WeChat;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Upload;
using AedBackend.Infrastructure.WeChat;

namespace AedBackend.Api.Endpoints;

internal sealed record HelpNotificationInput
{
    public required HelpRequestRecord Record { get; init; }
    public required PublishHelpRequest Request { get; init; }
    public required ExperienceUserRecord CurrentUser { get; init; }
    public required WeChatOptions Options { get; init; }
    public required IExperienceUserStore UserStore { get; init; }
    public required IUserAccountStatusService AccountStatus { get; init; }
    public required IResponderStore ResponderStore { get; init; }
    public required IWeChatSubscribeMessageSender Sender { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}

internal static class HelpPublishFlow
{
    private const int TemplateThingMaxLength = 20;
    private const int ChinaUtcOffsetHours = 8;
    private const int MaxHelpImageCount = 3;

    public static string? ValidatePublishRequest(
        PublishHelpRequest request,
        WeChatOptions options,
        ExperienceUserRecord? currentUser)
    {
        if (string.IsNullOrWhiteSpace(options.SubscribeTemplateId)) return "缺少订阅模板配置";
        if (currentUser is null) return "请先登录";
        if (string.IsNullOrWhiteSpace(currentUser.Phone)) return "请先授权联系电话";
        if (request.Images?.Count > MaxHelpImageCount) return "images 最多 3 项";
        return ValidateHelpFields(request);
    }

    public static string? ValidateReport(
        HelpReportRequest request,
        WeChatOptions options,
        UploadOptions uploadOptions)
    {
        if (request.Images is null || request.Images.Count == 0) return "images 至少 1 项";
        if (string.IsNullOrWhiteSpace(request.RecordText)) return "recordText 不能为空";
        return request.Images.All(item => IsUploadedImageUrl(item, options, uploadOptions))
            ? null
            : "images 必须是已上传图片 URL";
    }

    public static HelpRequestRecord CreateRecord(
        PublishHelpRequest request,
        ExperienceUserRecord currentUser)
    {
        var now = DateTimeOffset.UtcNow;
        return new HelpRequestRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            RequestNo = $"HELP{now.ToUnixTimeMilliseconds()}",
            UserId = currentUser.OpenId,
            RequestChannel = request.RequestChannel == "guest" ? "guest" : "user",
            Lat = request.Lat,
            Lng = request.Lng,
            Address = request.Address.Trim(),
            Content = request.Content.Trim(),
            Type = request.Type,
            CreatedAt = now,
            RequesterPhone = currentUser.Phone!,
            Images = request.Images?.ToArray() ?? [],
            Timeline = [new HelpTimelineRecord(HelpStatuses.Published, now, null)],
            GuestSession = CreateGuestSession(request)
        };
    }

    public static PublishHelpResponse ToPublishResponse(
        HelpRequestRecord record,
        IReadOnlyList<BroadcastRecipientResult> recipients)
    {
        return new PublishHelpResponse(
            record.Id,
            record.RequestNo,
            record.Status,
            record.CreatedAt,
            record.GuestSession?.FollowupCode,
            record.GuestSession?.CloseToken,
            new HelpNotificationResult(recipients.Count, recipients));
    }

    /**
     * 推送收窄（BackEnd.md §4.5A）：仅认证急救员（档案绑定且资格 active）可收到求助推送，
     * 排除求助发布者本人，不做地理过滤；落地页为会话页。
     */
    public static async Task<IReadOnlyList<BroadcastRecipientResult>> SendNotificationsAsync(
        HelpNotificationInput input)
    {
        var profiles = await input.ResponderStore.GetAllAsync(input.CancellationToken);
        var certifiedOpenIds = profiles
            .Where(p => p.QualificationStatus == "active" && !string.IsNullOrWhiteSpace(p.BoundOpenId))
            .Select(p => p.BoundOpenId!)
            .ToHashSet(StringComparer.Ordinal);

        var users = await input.UserStore.ListUsersAsync(input.CancellationToken);
        var data = CreateTemplateData(input.Request, input.CurrentUser, input.Record.CreatedAt);
        var page = $"pages-sub/rescue/session?id={input.Record.Id}";
        var results = new List<BroadcastRecipientResult>();

        foreach (var target in users)
        {
            if (target.OpenId == input.Record.UserId
                || !certifiedOpenIds.Contains(target.OpenId)
                || !IsTemplateAccepted(target, input.Options.SubscribeTemplateId)
                || await input.AccountStatus.IsDisabledAsync(target.OpenId, input.CancellationToken))
            {
                continue;
            }

            results.Add(await SendNotificationAsync(
                target,
                input.Options,
                input.Sender,
                page,
                data,
                input.CancellationToken));
        }

        return results;
    }

    private static string? ValidateHelpFields(PublishHelpRequest request)
    {
        if (request.Lat is < -90 or > 90) return "lat 不合法";
        if (request.Lng is < -180 or > 180) return "lng 不合法";
        if (string.IsNullOrWhiteSpace(request.Address)) return "address 不能为空";
        if (string.IsNullOrWhiteSpace(request.Content)) return "content 不能为空";
        return request.Type is "help_hand" or "call_120" or "call_school" or "general"
            ? null
            : "type 仅支持 help_hand / call_120 / call_school / general";
    }

    private static bool IsUploadedImageUrl(
        string value,
        WeChatOptions options,
        UploadOptions uploadOptions)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && IsAllowedUploadHost(value, options, uploadOptions)
            && uri.AbsolutePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedUploadHost(
        string value,
        WeChatOptions options,
        UploadOptions uploadOptions)
    {
        if (value.StartsWith(options.PublicBaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            return true;
        var ossBase = uploadOptions.Oss.PublicBaseUrl;
        return !string.IsNullOrWhiteSpace(ossBase)
            && value.StartsWith(ossBase.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    private static HelpGuestSessionRecord? CreateGuestSession(PublishHelpRequest request)
    {
        if (request.RequestChannel != "guest") return null;
        return new HelpGuestSessionRecord(
            request.GuestName,
            CreateCode("Q", 3),
            CreateCode("CLOSE", 4),
            CreateCode("ANON", 8),
            request.CallbackAllowed ?? false,
            request.PrivacyConsentAt);
    }

    private static async Task<BroadcastRecipientResult> SendNotificationAsync(
        ExperienceUserRecord target,
        WeChatOptions options,
        IWeChatSubscribeMessageSender sender,
        string page,
        IReadOnlyDictionary<string, SubscribeMessageDataValue> data,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendNotificationCoreAsync(
                target,
                options,
                sender,
                page,
                data,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return new BroadcastRecipientResult(target.OpenId, false, -1, ex.Message, null);
        }
    }

    private static async Task<BroadcastRecipientResult> SendNotificationCoreAsync(
        ExperienceUserRecord target,
        WeChatOptions options,
        IWeChatSubscribeMessageSender sender,
        string page,
        IReadOnlyDictionary<string, SubscribeMessageDataValue> data,
        CancellationToken cancellationToken)
    {
        var command = new SubscribeMessageCommand(
            target.OpenId,
            options.SubscribeTemplateId,
            page,
            options.DefaultMiniProgramState,
            data);
        var result = await sender.SendAsync(command, cancellationToken);
        return new BroadcastRecipientResult(
            target.OpenId,
            result.Sent,
            result.WeChatErrorCode,
            result.WeChatErrorMessage,
            result.MessageId);
    }

    private static IReadOnlyDictionary<string, SubscribeMessageDataValue> CreateTemplateData(
        PublishHelpRequest request,
        ExperienceUserRecord currentUser,
        DateTimeOffset createdAt)
    {
        return new Dictionary<string, SubscribeMessageDataValue>
        {
            ["thing2"] = new(CreateTemplateThing(request.Content)),
            ["time3"] = new(ToTemplateTime(createdAt)),
            ["thing4"] = new(CreateTemplateThing(request.GuestName ?? "微信用户")),
            ["phone_number5"] = new(currentUser.Phone!),
            ["thing6"] = new(CreateTemplateThing(request.Address))
        };
    }

    private static bool IsTemplateAccepted(ExperienceUserRecord user, string templateId)
    {
        return user.Subscriptions.TryGetValue(templateId, out var subscription)
            && subscription.Status is "accept" or "acceptWithAudio";
    }

    private static string CreateTemplateThing(string value)
    {
        var text = value.Trim();
        return text.Length <= TemplateThingMaxLength ? text : text[..TemplateThingMaxLength];
    }

    private static string ToTemplateTime(DateTimeOffset timestamp)
    {
        return timestamp.ToOffset(TimeSpan.FromHours(ChinaUtcOffsetHours))
            .ToString("yyyy年M月d日 HH:mm:ss");
    }

    private static string CreateCode(string prefix, int byteCount)
    {
        return $"{prefix}{Convert.ToHexString(RandomNumberGenerator.GetBytes(byteCount))}";
    }
}
