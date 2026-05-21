using System.Text.Json.Serialization;

namespace AedBackend.Core.DTOs.WeChat;

public sealed record SubscribeMessageDataValue([property: JsonPropertyName("value")] string Value);

public sealed record SendSubscribeMessageRequest(
    string OpenId,
    string? TemplateId,
    string? Page,
    string? MiniProgramState,
    IReadOnlyDictionary<string, SubscribeMessageDataValue> Data);

public sealed record SubscribeMessageCommand(
    string OpenId,
    string TemplateId,
    string? Page,
    string MiniProgramState,
    IReadOnlyDictionary<string, SubscribeMessageDataValue> Data);

public sealed record SendSubscribeMessageResult(
    bool Sent,
    int WeChatErrorCode,
    string WeChatErrorMessage,
    string? MessageId);

public sealed record WeChatLoginRequest(string Code);

public sealed record WeChatLoginResponse(
    string Token,
    string OpenId,
    bool IsNewUser,
    string? Phone);

public sealed record BindWeChatPhoneRequest(
    string? Code,
    string? Phone);

public sealed record BindWeChatPhoneResponse(
    string OpenId,
    string Phone);

public sealed record SaveSubscribeDecisionRequest(
    string TemplateId,
    string Status);

public sealed record ExperienceUserSummary(
    string OpenId,
    string? Phone,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastLoginAt,
    IReadOnlyDictionary<string, string> Subscriptions);

public sealed record BroadcastSubscribeMessageRequest(
    string? TemplateId,
    string? Page,
    string? MiniProgramState,
    IReadOnlyDictionary<string, SubscribeMessageDataValue> Data);

public sealed record BroadcastSubscribeMessageResult(
    int TargetCount,
    IReadOnlyList<BroadcastRecipientResult> Recipients);

public sealed record BroadcastRecipientResult(
    string OpenId,
    bool Sent,
    int WeChatErrorCode,
    string WeChatErrorMessage,
    string? MessageId);
