using System.Security.Cryptography;
using System.Text;
using AedBackend.Core.Constants;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Services.Admin;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Admin;

public sealed class AdminAuthorizationService : IAdminAuthorizationService
{
    private readonly IAdminUserStore adminUsers;
    private readonly AdminOptions adminOptions;
    private readonly WeChatOptions weChatOptions;

    public AdminAuthorizationService(
        IAdminUserStore adminUsers,
        IOptions<AdminOptions> adminOptions,
        IOptions<WeChatOptions> weChatOptions)
    {
        this.adminUsers = adminUsers;
        this.adminOptions = adminOptions.Value;
        this.weChatOptions = weChatOptions.Value;
    }

    public async Task<AdminAuthorizationResult> RequireAdminSessionAsync(
        string? adminKey,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adminOptions.SessionHashSecret))
        {
            return AdminAuthorizationResult.Failure(
                500,
                ErrorCodes.InternalServerError,
                "缺少 Admin:SessionHashSecret 配置");
        }

        if (string.IsNullOrWhiteSpace(adminKey))
        {
            return AdminAuthorizationResult.Failure(
                401,
                ErrorCodes.Unauthorized,
                "缺少有效的管理端会话");
        }

        var sessionHash = AdminSessionCrypto.HashSessionKey(
            adminKey,
            adminOptions.SessionHashSecret);
        var users = await adminUsers.ListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        foreach (var user in users)
        {
            var match = user.Sessions.FirstOrDefault(session =>
                IsActiveSession(session, user.SessionsRevokedAt, now)
                && AdminSessionCrypto.FixedTimeEquals(session.SessionHash, sessionHash));
            if (match is null) continue;

            if (user.Status != AdminStatuses.Active)
            {
                return AdminAuthorizationResult.Failure(
                    403,
                    ErrorCodes.Forbidden,
                    "管理员账号已停用");
            }

            await TouchSessionAsync(user, match, ipAddress, userAgent, now, cancellationToken);
            return AdminAuthorizationResult.Success(
                new AdminPrincipal(user.Id, user.Username, user.Role, user.Status, match.Id));
        }

        return AdminAuthorizationResult.Failure(
            401,
            ErrorCodes.Unauthorized,
            "管理端会话无效或已过期");
    }

    public async Task<AdminAuthorizationResult> RequireLegacyOrAdminAsync(
        string? adminKey,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (IsLegacyAdminKey(adminKey))
        {
            return AdminAuthorizationResult.Success(
                new AdminPrincipal("legacy", "legacy-admin-key", AdminRoles.SuperAdmin, AdminStatuses.Active, "legacy"));
        }

        return await RequireAdminSessionAsync(adminKey, ipAddress, userAgent, cancellationToken);
    }

    public bool IsLegacyAdminKey(string? adminKey)
    {
        if (string.IsNullOrWhiteSpace(adminKey) || string.IsNullOrWhiteSpace(weChatOptions.AdminKey))
        {
            return false;
        }

        return AdminSessionCrypto.FixedTimeEquals(adminKey, weChatOptions.AdminKey);
    }

    private static bool IsActiveSession(
        AdminSessionEntry session,
        DateTimeOffset? revokedAt,
        DateTimeOffset now)
    {
        return session.ExpiresAt > now
            && (revokedAt is null || session.CreatedAt > revokedAt.Value);
    }

    private async Task TouchSessionAsync(
        AdminUserRecord user,
        AdminSessionEntry session,
        string? ipAddress,
        string? userAgent,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (session.LastSeenAt > now.AddHours(-1))
        {
            return;
        }

        await adminUsers.UpdateAsync(
            user.Id,
            current => current with
            {
                Sessions = current.Sessions.Select(item => item.Id == session.Id
                    ? item with
                    {
                        LastSeenAt = now,
                        MaskedIpAddress = AdminWebContext.MaskIpAddress(ipAddress),
                        UserAgentSummary = AdminWebContext.SummarizeUserAgent(userAgent)
                    }
                    : item).ToArray()
            },
            cancellationToken);
    }
}

public static class AdminSessionCrypto
{
    public static string GenerateSessionKey()
    {
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    public static string HashSessionKey(string sessionKey, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var valueBytes = Encoding.UTF8.GetBytes(sessionKey);
        return Convert.ToHexString(HMACSHA256.HashData(keyBytes, valueBytes));
    }

    public static bool FixedTimeEquals(string actual, string expected)
    {
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return actualBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

public static class AdminWebContext
{
    public static string MaskIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress)) return string.Empty;

        var parts = ipAddress.Split('.');
        return parts.Length == 4
            ? $"{parts[0]}.{parts[1]}.*.*"
            : ipAddress.Length <= 4 ? "****" : $"{ipAddress[..4]}***";
    }

    public static string SummarizeUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return string.Empty;

        var trimmed = userAgent.Trim();
        return trimmed.Length <= 80 ? trimmed : trimmed[..80];
    }
}
