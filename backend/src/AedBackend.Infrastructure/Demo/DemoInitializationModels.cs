namespace AedBackend.Infrastructure.Demo;

/** init-demo-data 的输入。默认源路径都由构建产物的只读副本提供。 */
public sealed record DemoInitializationRequest
{
    public required string DataRoot { get; init; }
    public string DemoDataRoot { get; init; } = DemoDataLayout.DefaultDataRoot;
    public string RolesFilePath { get; init; } = DemoRoleCatalog.DefaultPath;
    public string SiteConfigPath { get; init; } = Site.SiteConfigurationLoader.DefaultPath;
    public IReadOnlyList<string> AdditionalAllowedRoots { get; init; } = [];
}

public static class DemoDataLayout
{
    /** 迁移器要求的 11 个 JSON 输入（demo/data 原样链接到配置目录）。 */
    public static readonly IReadOnlyList<string> RequiredFiles =
    [
        "wechat-experience-users.json",
        "admin-users.json",
        "admin-user-profiles.json",
        "admin-audit-logs.json",
        "aed-devices.json",
        "rescuer-profiles.json",
        "knowledge-summary.json",
        "courses.json",
        "quick-actions.json",
        "help-requests.json",
        "rescue-sessions.json"
    ];

    public const string DatabaseFileName = "aed.db";
    public const string UploadsDirectoryName = "uploads";

    public static string DefaultDataRoot => Path.Combine(AppContext.BaseDirectory, "config", "demo-data");
}

/** 单个演示角色的就绪对账结果。 */
public sealed record DemoRoleReadiness(
    string Role,
    string OpenId,
    string Campus,
    string AccountStatus,
    string? ResponderProfileId,
    bool Certified,
    bool SchoolBindingApproved);

public sealed record DemoInitializationReport(
    string DataRoot,
    string DatabasePath,
    string UploadsPath,
    string DataVersion,
    IReadOnlyList<DemoRoleReadiness> Roles,
    IReadOnlyDictionary<string, int> Counts);

/** 初始化拒绝执行或对账失败；消息是可操作原因，不含任何秘密。 */
public sealed class DemoInitializationException : Exception
{
    public DemoInitializationException(string message) : base(message)
    {
    }

    public DemoInitializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
