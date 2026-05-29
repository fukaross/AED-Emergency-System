using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AedBackend.Tests.Admin;

internal sealed class AdminTestApplication : WebApplicationFactory<Program>
{
    private readonly string dataRoot;

    public AdminTestApplication(string dataRoot)
    {
        this.dataRoot = dataRoot;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AED_PUSH_ADMIN_KEY"] = AdminApiTestBase.LegacyAdminKey,
                ["AED_ADMIN_SESSION_HASH_SECRET"] = "test-session-secret-value",
                ["WeChat:ExperienceUsersFilePath"] = Path.Combine(dataRoot, "wechat-experience-users.json"),
                ["WeChat:HelpRequestsFilePath"] = Path.Combine(dataRoot, "help-requests.json"),
                ["WeChat:AedDevicesFilePath"] = Path.Combine(dataRoot, "aed-devices.json"),
                ["WeChat:UploadsDirectory"] = Path.Combine(dataRoot, "uploads"),
                ["WeChat:RescuerProfilesFilePath"] = Path.Combine(dataRoot, "rescuer-profiles.json"),
                ["WeChat:KnowledgeSummaryFilePath"] = Path.Combine(dataRoot, "knowledge-summary.json"),
                ["WeChat:CoursesFilePath"] = Path.Combine(dataRoot, "courses.json"),
                ["WeChat:QuickActionsFilePath"] = Path.Combine(dataRoot, "quick-actions.json"),
                ["Admin:AdminUsersFilePath"] = Path.Combine(dataRoot, "admin-users.json"),
                ["Admin:AdminUserProfilesFilePath"] = Path.Combine(dataRoot, "admin-user-profiles.json"),
                ["Admin:AdminAuditLogsFilePath"] = Path.Combine(dataRoot, "admin-audit-logs.json")
            });
        });

        return base.CreateHost(builder);
    }
}
