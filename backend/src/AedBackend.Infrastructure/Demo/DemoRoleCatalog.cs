using System.Text.Json;

namespace AedBackend.Infrastructure.Demo;

/**
 * demo/roles.json 的只读后端产物（csproj 以 Content 链接为 config/demo-roles.json）。
 * 它是演示登录 code → OpenId 的唯一来源；业务代码不得另写一套演示身份表。
 */
public sealed class DemoRoleCatalog
{
    public const int SupportedSchemaVersion = 1;
    public const string RelativePath = "config/demo-roles.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Lazy<DemoRolesDocument> document;

    public DemoRoleCatalog() : this(DefaultPath)
    {
    }

    public DemoRoleCatalog(string path)
    {
        SourcePath = Path.GetFullPath(path);
        document = new Lazy<DemoRolesDocument>(() => LoadFrom(SourcePath));
    }

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "config", "demo-roles.json");

    public string SourcePath { get; }

    public DemoRolesDocument Document => document.Value;

    /** 启动期提前加载，配置缺失/不合法时尽早失败。 */
    public void EnsureLoaded()
    {
        _ = Document;
    }

    /** 精确匹配登录 code；未知 code 返回 null，由调用方按既有参数错误处理。 */
    public DemoRoleDefinition? FindByLoginCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return Document.DemoRoles.FirstOrDefault(role =>
            role.LoginCode.Equals(code, StringComparison.Ordinal));
    }

    public DemoRoleDefinition? FindByRole(string role)
    {
        return Document.DemoRoles.FirstOrDefault(item =>
            item.Role.Equals(role, StringComparison.Ordinal));
    }

    public static DemoRoleCatalog LoadOrThrow(string path)
    {
        var catalog = new DemoRoleCatalog(path);
        catalog.EnsureLoaded();
        return catalog;
    }

    private static DemoRolesDocument LoadFrom(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new DemoConfigurationException(
                $"缺少演示角色注册表：{fullPath}。演示数据唯一来源是仓库 demo/roles.json（构建时链接为 config/demo-roles.json）。");
        }

        DemoRolesDocument? parsed;
        try
        {
            using var stream = File.OpenRead(fullPath);
            parsed = JsonSerializer.Deserialize<DemoRolesDocument>(stream, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new DemoConfigurationException($"演示角色注册表不是合法 JSON：{fullPath}", exception);
        }

        if (parsed is null) throw new DemoConfigurationException($"演示角色注册表为空：{fullPath}");
        Validate(parsed, fullPath);
        return parsed;
    }

    private static void Validate(DemoRolesDocument document, string fullPath)
    {
        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            throw new DemoConfigurationException(
                $"演示角色注册表 schemaVersion={document.SchemaVersion} 不受支持（当前仅支持 {SupportedSchemaVersion}）：{fullPath}");
        }

        if (document.DemoRoles.Count == 0)
        {
            throw new DemoConfigurationException($"演示角色注册表没有 demoRoles：{fullPath}");
        }

        var loginCodes = new HashSet<string>(StringComparer.Ordinal);
        var openIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in document.DemoRoles)
        {
            if (string.IsNullOrWhiteSpace(role.Role)
                || string.IsNullOrWhiteSpace(role.LoginCode)
                || string.IsNullOrWhiteSpace(role.OpenId)
                || string.IsNullOrWhiteSpace(role.Campus)
                || string.IsNullOrWhiteSpace(role.AccountStatus))
            {
                throw new DemoConfigurationException(
                    $"演示角色缺少 role/loginCode/openId/campus/accountStatus 之一：{Describe(role)}（{fullPath}）");
            }

            if (!loginCodes.Add(role.LoginCode))
            {
                throw new DemoConfigurationException($"演示角色 loginCode 重复：{role.LoginCode}（{fullPath}）");
            }

            if (!openIds.Add(role.OpenId))
            {
                throw new DemoConfigurationException($"演示角色 openId 重复：{role.OpenId}（{fullPath}）");
            }

            if (role.SchoolBinding is { } binding
                && (string.IsNullOrWhiteSpace(binding.PersonType)
                    || string.IsNullOrWhiteSpace(binding.Name)
                    || string.IsNullOrWhiteSpace(binding.College)))
            {
                throw new DemoConfigurationException(
                    $"演示角色 {role.Role} 的 schoolBinding 缺少 personType/name/college（{fullPath}）");
            }
        }

        foreach (var required in new[] { DemoRoleNames.Requester, DemoRoleNames.ActiveRescuer, DemoRoleNames.Unqualified })
        {
            if (!document.DemoRoles.Any(role => role.Role.Equals(required, StringComparison.Ordinal)))
            {
                throw new DemoConfigurationException($"演示角色注册表缺少必需角色 {required}：{fullPath}");
            }
        }

        var negativeOpenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sample in document.NegativeSamples)
        {
            if (string.IsNullOrWhiteSpace(sample.OpenId))
            {
                throw new DemoConfigurationException($"演示负向样本缺少 openId：{sample.Id}（{fullPath}）");
            }

            if (!negativeOpenIds.Add(sample.OpenId))
            {
                throw new DemoConfigurationException($"演示负向样本 openId 重复：{sample.OpenId}（{fullPath}）");
            }
        }
    }

    private static string Describe(DemoRoleDefinition role)
    {
        return string.IsNullOrWhiteSpace(role.Role) ? "(未命名角色)" : role.Role;
    }
}

public static class DemoRoleNames
{
    public const string Requester = "requester";
    public const string ActiveRescuer = "active-rescuer";
    public const string Unqualified = "unqualified";
}

public sealed record DemoRolesDocument
{
    public int SchemaVersion { get; init; }
    public string? DataVersion { get; init; }
    public List<DemoRoleDefinition> DemoRoles { get; init; } = [];
    public List<DemoNegativeSample> NegativeSamples { get; init; } = [];
}

public sealed record DemoRoleDefinition
{
    public string Role { get; init; } = string.Empty;
    public string LoginCode { get; init; } = string.Empty;
    public string OpenId { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Campus { get; init; } = string.Empty;
    public string PrimaryRole { get; init; } = string.Empty;
    public string AccountStatus { get; init; } = string.Empty;
    public string? ResponderProfileId { get; init; }
    public string? QualificationStatus { get; init; }
    public bool ExpectedCertifiedResponder { get; init; }
    public DemoSchoolBinding? SchoolBinding { get; init; }
}

public sealed record DemoSchoolBinding
{
    public string PersonType { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string College { get; init; } = string.Empty;
    public string? ClassName { get; init; }
    public string? StudentId { get; init; }
    public string? StaffId { get; init; }
    public string? Phone { get; init; }
    public string? Note { get; init; }
}

public sealed record DemoNegativeSample
{
    public string Id { get; init; } = string.Empty;
    public string OpenId { get; init; } = string.Empty;
    public string? ResponderProfileId { get; init; }
    public string AccountStatus { get; init; } = string.Empty;
}
