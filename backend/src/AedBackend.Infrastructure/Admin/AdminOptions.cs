using Microsoft.Extensions.Configuration;

namespace AedBackend.Infrastructure.Admin;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string SessionHashSecret { get; set; } = string.Empty;
    public string AdminUsersFilePath { get; set; } = "data/admin-users.json";
    public string AdminUserProfilesFilePath { get; set; } = "data/admin-user-profiles.json";
    public string AdminAuditLogsFilePath { get; set; } = "data/admin-audit-logs.json";

    public void ApplyEnvironmentAliases(IConfiguration configuration)
    {
        SessionHashSecret = FirstNonEmpty(
            configuration["AED_ADMIN_SESSION_HASH_SECRET"],
            SessionHashSecret);
    }

    private static string FirstNonEmpty(string? first, string second)
    {
        return string.IsNullOrWhiteSpace(first) ? second : first;
    }
}
