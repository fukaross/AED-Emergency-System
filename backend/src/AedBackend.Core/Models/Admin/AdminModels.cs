namespace AedBackend.Core.Models.Admin;

public static class AdminRoles
{
    public const string SuperAdmin = "super_admin";
    public const string Admin = "admin";
}

public static class AdminStatuses
{
    public const string Active = "active";
    public const string Disabled = "disabled";
}

public static class AdminUserFields
{
    public static readonly IReadOnlySet<string> PrimaryRoles =
        new HashSet<string> { "student", "teacher", "staff", "admin" };

    public static readonly IReadOnlySet<string> AccountStatuses =
        new HashSet<string> { "active", "disabled" };

    public static readonly IReadOnlySet<string> ResponderStatuses =
        new HashSet<string> { "active", "inactive" };

    public static readonly IReadOnlySet<string> Levels =
        new HashSet<string> { "honor", "trainee", "official", "core" };
}

public sealed record AdminUserRecord
{
    public string Id { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string NormalizedUsername { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string Role { get; init; } = AdminRoles.Admin;
    public string Status { get; init; } = AdminStatuses.Active;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public DateTimeOffset? SessionsRevokedAt { get; init; }
    public DateTimeOffset? LockedUntil { get; init; }
    public int FailedLoginCount { get; init; }
    public IReadOnlyList<AdminSessionEntry> Sessions { get; init; } = [];
}

public sealed record AdminSessionEntry
{
    public string Id { get; init; } = string.Empty;
    public string SessionHash { get; init; } = string.Empty;
    public string MaskedIpAddress { get; init; } = string.Empty;
    public string UserAgentSummary { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
}

public sealed record AdminPrincipal(
    string Id,
    string Username,
    string Role,
    string Status,
    string SessionId);

public sealed record AdminUserProfileRecord
{
    public string OpenId { get; init; } = string.Empty;
    public string PrimaryRole { get; init; } = "student";
    public string AccountStatus { get; init; } = AdminStatuses.Active;
    public string? ResponderStatus { get; init; }
    public bool Certified { get; init; }
    public string? Level { get; init; }
    public string? AdminRemark { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed record AdminAuditLogRecord
{
    public string Id { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string EventName { get; init; } = string.Empty;
    public string? TargetId { get; init; }
    public string? ActorType { get; init; }
    public string? ActorId { get; init; }
    public string? ActorName { get; init; }
    public string? ActorRole { get; init; }
    public string? Summary { get; init; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? ClientPlatform { get; init; }
    public string? ClientVersion { get; init; }
    public string? DeviceModel { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
