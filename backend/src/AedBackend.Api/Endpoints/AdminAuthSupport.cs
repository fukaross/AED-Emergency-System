using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Services.Admin;
using AedBackend.Infrastructure.Admin;

namespace AedBackend.Api.Endpoints;

internal static class AdminAuthSupport
{
    private const int MinDeviceIdLength = 16;
    private const int MaxDeviceIdLength = 128;
    private static readonly TimeSpan SessionDuration = TimeSpan.FromDays(7);

    public static AdminUserRecord NewAdmin(
        string username,
        string role,
        DateTimeOffset now)
    {
        var trimmed = username.Trim();
        return new AdminUserRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = trimmed,
            NormalizedUsername = NormalizeUsername(trimmed),
            Role = role,
            Status = AdminStatuses.Active,
            CreatedAt = now
        };
    }

    public static AdminSessionEntry NewSession(
        string sessionKey,
        string secret,
        string deviceHash,
        HttpContext context,
        DateTimeOffset now)
    {
        var ipAddress = AdminEndpointHelpers.GetClientIp(context);
        return new AdminSessionEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            SessionHash = AdminSessionCrypto.HashSessionKey(sessionKey, secret),
            DeviceHash = deviceHash,
            IpAddress = ipAddress ?? string.Empty,
            MaskedIpAddress = AdminWebContext.MaskIpAddress(ipAddress),
            UserAgentSummary = AdminWebContext.SummarizeUserAgent(AdminEndpointHelpers.GetUserAgent(context.Request)),
            CreatedAt = now,
            ExpiresAt = now.Add(SessionDuration),
            LastSeenAt = now
        };
    }

    public static IEnumerable<AdminSessionEntry> ActiveSessions(
        AdminUserRecord user,
        DateTimeOffset now)
    {
        return user.Sessions.Where(session =>
            session.ExpiresAt > now
            && (user.SessionsRevokedAt is null || session.CreatedAt > user.SessionsRevokedAt));
    }

    public static AdminSessionListItem ToSessionItem(
        AdminSessionEntry session,
        string currentSessionId,
        bool includeFullIp)
    {
        return new AdminSessionListItem(
            session.Id,
            session.Id == currentSessionId,
            session.MaskedIpAddress,
            includeFullIp ? session.IpAddress : null,
            session.UserAgentSummary,
            session.CreatedAt,
            session.ExpiresAt,
            session.LastSeenAt);
    }

    public static string HashDeviceId(string deviceId, string secret)
    {
        return AdminSessionCrypto.HashSessionKey(deviceId.Trim(), secret);
    }

    public static string? ValidateDeviceId(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return "deviceId 不能为空";
        var length = deviceId.Trim().Length;
        return length is < MinDeviceIdLength or > MaxDeviceIdLength
            ? $"deviceId 长度必须在 {MinDeviceIdLength} 到 {MaxDeviceIdLength} 位之间"
            : null;
    }

    public static int CountDeviceSlots(IEnumerable<AdminSessionEntry> sessions)
    {
        return sessions.Select(DeviceSlotKey).Distinct(StringComparer.Ordinal).Count();
    }

    public static bool HasDeviceSession(IEnumerable<AdminSessionEntry> sessions, string deviceHash)
    {
        return sessions.Any(session => session.DeviceHash == deviceHash);
    }

    public static bool IsNewDeviceOverLimit(
        IEnumerable<AdminSessionEntry> activeSessions,
        string deviceHash,
        int maxDeviceSlots)
    {
        var sessions = activeSessions.ToArray();
        return !HasDeviceSession(sessions, deviceHash)
            && CountDeviceSlots(sessions) >= maxDeviceSlots;
    }

    public static IReadOnlyList<AdminSessionEntry> ReplaceDeviceSession(
        IEnumerable<AdminSessionEntry> activeSessions,
        string deviceHash,
        AdminSessionEntry session)
    {
        return activeSessions
            .Where(item => item.DeviceHash != deviceHash)
            .Append(session)
            .ToArray();
    }

    public static string NormalizeUsername(string username)
    {
        return username.Trim().ToUpperInvariant();
    }

    public static string? ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username)) return "username 不能为空";
        return string.IsNullOrWhiteSpace(password) || password.Length < 8
            ? "password 长度不能少于 8 位"
            : null;
    }

    public static Task AppendAuditAsync(
        IAdminAuditLogStore auditLogs,
        HttpContext context,
        string eventName,
        string targetId,
        CancellationToken cancellationToken)
    {
        var audit = AdminEndpointHelpers.CreateAudit("admin", eventName, null, targetId, eventName, context);
        return auditLogs.AppendAsync(audit, cancellationToken);
    }

    private static string DeviceSlotKey(AdminSessionEntry session)
    {
        return string.IsNullOrWhiteSpace(session.DeviceHash)
            ? $"legacy:{session.Id}"
            : session.DeviceHash;
    }
}
