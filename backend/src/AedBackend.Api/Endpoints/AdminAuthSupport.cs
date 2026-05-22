using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.Models.Admin;
using AedBackend.Infrastructure.Admin;

namespace AedBackend.Api.Endpoints;

internal static class AdminAuthSupport
{
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
        HttpContext context,
        DateTimeOffset now)
    {
        return new AdminSessionEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            SessionHash = AdminSessionCrypto.HashSessionKey(sessionKey, secret),
            MaskedIpAddress = AdminWebContext.MaskIpAddress(AdminEndpointHelpers.GetClientIp(context)),
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
        string currentSessionId)
    {
        return new AdminSessionListItem(
            session.Id,
            session.Id == currentSessionId,
            session.MaskedIpAddress,
            session.UserAgentSummary,
            session.CreatedAt,
            session.ExpiresAt,
            session.LastSeenAt);
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
}
