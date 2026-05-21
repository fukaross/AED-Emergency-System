namespace AedBackend.Core.Models.WeChat;

public sealed record WeChatLoginSession(string OpenId, string? UnionId);

public sealed record WeChatCodeSessionResult(
    bool Succeeded,
    WeChatLoginSession? Session,
    int ErrorCode,
    string ErrorMessage);

public sealed record WeChatPhoneNumberResult(
    bool Succeeded,
    string? PhoneNumber,
    int ErrorCode,
    string ErrorMessage);

public sealed record ExperienceUserRecord(
    string OpenId,
    string? UnionId,
    string? Phone,
    string SessionTokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastLoginAt,
    IReadOnlyDictionary<string, SubscriptionRecord> Subscriptions);

public sealed record SubscriptionRecord(
    string TemplateId,
    string Status,
    DateTimeOffset UpdatedAt);

public sealed record SaveLoginResult(
    ExperienceUserRecord User,
    bool IsNewUser);
