using System.Text.Json;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Models.WeChat;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal static partial class SqliteMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ExperienceUserEntity ToEntity(ExperienceUserRecord item)
    {
        return new ExperienceUserEntity
        {
            OpenId = item.OpenId,
            UnionId = item.UnionId,
            Phone = item.Phone,
            SessionTokenHash = item.SessionTokenHash,
            CreatedAt = item.CreatedAt,
            LastLoginAt = item.LastLoginAt,
            Subscriptions = item.Subscriptions.Values
                .Select(subscription => ToEntity(item.OpenId, subscription))
                .ToList()
        };
    }

    public static ExperienceUserRecord ToRecord(ExperienceUserEntity item)
    {
        return new ExperienceUserRecord(
            item.OpenId,
            item.UnionId,
            item.Phone,
            item.SessionTokenHash,
            item.CreatedAt,
            item.LastLoginAt,
            item.Subscriptions.ToDictionary(s => s.TemplateId, ToRecord));
    }

    public static AdminUserEntity ToEntity(AdminUserRecord item)
    {
        return new AdminUserEntity
        {
            Id = item.Id,
            Username = item.Username,
            NormalizedUsername = item.NormalizedUsername,
            PasswordHash = item.PasswordHash,
            Role = item.Role,
            Status = item.Status,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            SessionsRevokedAt = item.SessionsRevokedAt,
            LockedUntil = item.LockedUntil,
            FailedLoginCount = item.FailedLoginCount,
            Sessions = item.Sessions.Select(session => ToEntity(item.Id, session)).ToList()
        };
    }

    public static AdminUserRecord ToRecord(AdminUserEntity item)
    {
        return new AdminUserRecord
        {
            Id = item.Id,
            Username = item.Username,
            NormalizedUsername = item.NormalizedUsername,
            PasswordHash = item.PasswordHash,
            Role = item.Role,
            Status = item.Status,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            SessionsRevokedAt = item.SessionsRevokedAt,
            LockedUntil = item.LockedUntil,
            FailedLoginCount = item.FailedLoginCount,
            Sessions = item.Sessions.OrderBy(s => s.CreatedAt).Select(ToRecord).ToArray()
        };
    }

    public static AdminUserProfileEntity ToEntity(AdminUserProfileRecord item)
    {
        return new AdminUserProfileEntity
        {
            OpenId = item.OpenId,
            PrimaryRole = item.PrimaryRole,
            AccountStatus = item.AccountStatus,
            ResponderStatus = item.ResponderStatus,
            Certified = item.Certified,
            Level = item.Level,
            AdminRemark = item.AdminRemark,
            UpdatedAt = item.UpdatedAt
        };
    }

    public static AdminUserProfileRecord ToRecord(AdminUserProfileEntity item)
    {
        return new AdminUserProfileRecord
        {
            OpenId = item.OpenId,
            PrimaryRole = item.PrimaryRole,
            AccountStatus = item.AccountStatus,
            ResponderStatus = item.ResponderStatus,
            Certified = item.Certified,
            Level = item.Level,
            AdminRemark = item.AdminRemark,
            UpdatedAt = item.UpdatedAt
        };
    }

    public static AdminAuditLogEntity ToEntity(AdminAuditLogRecord item)
    {
        return new AdminAuditLogEntity
        {
            Id = item.Id,
            EventType = item.EventType,
            EventName = item.EventName,
            TargetId = item.TargetId,
            ActorType = item.ActorType,
            ActorId = item.ActorId,
            ActorName = item.ActorName,
            ActorRole = item.ActorRole,
            Summary = item.Summary,
            MetadataJson = item.Metadata is null ? null : JsonSerializer.Serialize(item.Metadata, JsonOptions),
            Latitude = item.Latitude,
            Longitude = item.Longitude,
            ClientPlatform = item.ClientPlatform,
            ClientVersion = item.ClientVersion,
            DeviceModel = item.DeviceModel,
            IpAddress = item.IpAddress,
            UserAgent = item.UserAgent,
            CreatedAt = item.CreatedAt
        };
    }

    public static AdminAuditLogRecord ToRecord(AdminAuditLogEntity item)
    {
        return new AdminAuditLogRecord
        {
            Id = item.Id,
            EventType = item.EventType,
            EventName = item.EventName,
            TargetId = item.TargetId,
            ActorType = item.ActorType,
            ActorId = item.ActorId,
            ActorName = item.ActorName,
            ActorRole = item.ActorRole,
            Summary = item.Summary,
            Metadata = DeserializeMetadata(item.MetadataJson),
            Latitude = item.Latitude,
            Longitude = item.Longitude,
            ClientPlatform = item.ClientPlatform,
            ClientVersion = item.ClientVersion,
            DeviceModel = item.DeviceModel,
            IpAddress = item.IpAddress,
            UserAgent = item.UserAgent,
            CreatedAt = item.CreatedAt
        };
    }

    private static ExperienceUserSubscriptionEntity ToEntity(string openId, SubscriptionRecord item)
    {
        return new ExperienceUserSubscriptionEntity
        {
            OpenId = openId,
            TemplateId = item.TemplateId,
            Status = item.Status,
            UpdatedAt = item.UpdatedAt
        };
    }

    private static SubscriptionRecord ToRecord(ExperienceUserSubscriptionEntity item)
    {
        return new SubscriptionRecord(item.TemplateId, item.Status, item.UpdatedAt);
    }

    private static AdminSessionEntity ToEntity(string adminUserId, AdminSessionEntry item)
    {
        return new AdminSessionEntity
        {
            Id = item.Id,
            AdminUserId = adminUserId,
            SessionHash = item.SessionHash,
            DeviceHash = item.DeviceHash,
            IpAddress = item.IpAddress,
            MaskedIpAddress = item.MaskedIpAddress,
            UserAgentSummary = item.UserAgentSummary,
            CreatedAt = item.CreatedAt,
            ExpiresAt = item.ExpiresAt,
            LastSeenAt = item.LastSeenAt
        };
    }

    private static AdminSessionEntry ToRecord(AdminSessionEntity item)
    {
        return new AdminSessionEntry
        {
            Id = item.Id,
            SessionHash = item.SessionHash,
            DeviceHash = item.DeviceHash,
            IpAddress = item.IpAddress,
            MaskedIpAddress = item.MaskedIpAddress,
            UserAgentSummary = item.UserAgentSummary,
            CreatedAt = item.CreatedAt,
            ExpiresAt = item.ExpiresAt,
            LastSeenAt = item.LastSeenAt
        };
    }

    private static IReadOnlyDictionary<string, object?>? DeserializeMetadata(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions);
    }
}
