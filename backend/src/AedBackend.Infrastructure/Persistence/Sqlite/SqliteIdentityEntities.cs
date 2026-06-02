namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class ExperienceUserEntity
{
    public string OpenId { get; set; } = string.Empty;
    public string? UnionId { get; set; }
    public string? Phone { get; set; }
    public string SessionTokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastLoginAt { get; set; }
    public List<ExperienceUserSubscriptionEntity> Subscriptions { get; set; } = new();
}

internal sealed class ExperienceUserSubscriptionEntity
{
    public string OpenId { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class AdminUserEntity
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? SessionsRevokedAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public int FailedLoginCount { get; set; }
    public List<AdminSessionEntity> Sessions { get; set; } = new();
}

internal sealed class AdminSessionEntity
{
    public string Id { get; set; } = string.Empty;
    public string AdminUserId { get; set; } = string.Empty;
    public string SessionHash { get; set; } = string.Empty;
    public string DeviceHash { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string MaskedIpAddress { get; set; } = string.Empty;
    public string UserAgentSummary { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}

internal sealed class AdminUserProfileEntity
{
    public string OpenId { get; set; } = string.Empty;
    public string PrimaryRole { get; set; } = string.Empty;
    public string AccountStatus { get; set; } = string.Empty;
    public string? ResponderStatus { get; set; }
    public bool Certified { get; set; }
    public string? Level { get; set; }
    public string? AdminRemark { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

internal sealed class AdminAuditLogEntity
{
    public string Id { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string? ActorType { get; set; }
    public string? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string? ActorRole { get; set; }
    public string? Summary { get; set; }
    public string? MetadataJson { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ClientPlatform { get; set; }
    public string? ClientVersion { get; set; }
    public string? DeviceModel { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
