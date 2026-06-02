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
    string? Name,
    string? AvatarUrl,
    string? Gender,
    string SessionTokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastLoginAt,
    IReadOnlyDictionary<string, SubscriptionRecord> Subscriptions);

public sealed record UpdateExperienceUserProfilePatch(
    PatchValue<string?> Name,
    PatchValue<string?> AvatarUrl,
    PatchValue<string?> Gender,
    PatchValue<string?> Phone);

public readonly record struct PatchValue<T>(bool HasValue, T? Value)
{
    public static PatchValue<T> Missing => new(false, default);

    public static PatchValue<T> Present(T? value) => new(true, value);
}

public sealed record SubscriptionRecord(
    string TemplateId,
    string Status,
    DateTimeOffset UpdatedAt);

public sealed record SaveLoginResult(
    ExperienceUserRecord User,
    bool IsNewUser);
