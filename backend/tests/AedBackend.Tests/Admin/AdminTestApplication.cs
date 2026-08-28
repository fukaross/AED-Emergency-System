using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AedBackend.Tests.Admin;

internal sealed class AdminTestApplication : WebApplicationFactory<Program>
{
    private readonly string dataRoot;
    private readonly IReadOnlyDictionary<string, string?> extraConfiguration;
    private readonly Action<IServiceCollection>? configureServices;

    public AdminTestApplication(
        string dataRoot,
        IReadOnlyDictionary<string, string?>? extraConfiguration = null,
        Action<IServiceCollection>? configureServices = null)
    {
        this.dataRoot = dataRoot;
        this.extraConfiguration = extraConfiguration ?? new Dictionary<string, string?>();
        this.configureServices = configureServices;
    }

    /** 测试宿主移除 Windows EventLog，避免非管理员环境把真实端点异常遮蔽为日志权限错误。 */
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });
            configureServices?.Invoke(services);
        });
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var values = new Dictionary<string, string?>
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
                ["WeChat:RescueSessionsFilePath"] = Path.Combine(dataRoot, "rescue-sessions.json"),
                ["Admin:AdminUsersFilePath"] = Path.Combine(dataRoot, "admin-users.json"),
                ["Admin:AdminUserProfilesFilePath"] = Path.Combine(dataRoot, "admin-user-profiles.json"),
                ["Admin:AdminAuditLogsFilePath"] = Path.Combine(dataRoot, "admin-audit-logs.json"),
                ["Rescue:PresenceMonitorEnabled"] = "false"
            };
            foreach (var item in extraConfiguration) values[item.Key] = item.Value;
            config.AddInMemoryCollection(values);
        });
    }
}
