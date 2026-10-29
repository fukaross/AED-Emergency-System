using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AedBackend.Infrastructure.Site;

/**
 * 读取并校验 CONTRACT-001 的共享站点配置。
 * 运行时固定从 AppContext.BaseDirectory/config/site.json 读取，不依赖 shell 当前目录；
 * 先校验 schemaVersion，再绑定根 site 对象，最后做字段级校验。
 */
public static class SiteConfigurationLoader
{
    public const int SupportedSchemaVersion = 1;
    public const string RelativePath = "config/site.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /** 校区 code：小写字母/数字/短横线，首尾不能是短横线。 */
    private static readonly Regex CampusCodePattern = new(
        "^[a-z0-9]([a-z0-9-]*[a-z0-9])?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "config", "site.json");

    public static SiteOptions LoadDefault() => Load(DefaultPath);

    public static SiteOptions Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new SiteConfigurationException(
                $"缺少站点配置文件：{fullPath}。请保留仓库根 config/site.json，不要在源码中写死机构/校区常量。");
        }

        var json = ReadText(fullPath);
        using var document = ParseDocument(json, fullPath);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new SiteConfigurationException($"站点配置必须是 JSON 对象：{fullPath}");
        }

        ValidateSchemaVersion(root, fullPath);

        if (!root.TryGetProperty("site", out var siteElement)
            || siteElement.ValueKind != JsonValueKind.Object)
        {
            throw new SiteConfigurationException($"站点配置缺少根 site 对象：{fullPath}");
        }

        SiteSection? site;
        try
        {
            site = siteElement.Deserialize<SiteSection>(JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new SiteConfigurationException($"站点配置 site 字段类型不合法：{fullPath}", exception);
        }

        if (site is null)
        {
            throw new SiteConfigurationException($"站点配置 site 对象为空：{fullPath}");
        }

        return Build(site, fullPath);
    }

    private static string ReadText(string fullPath)
    {
        try
        {
            return File.ReadAllText(fullPath);
        }
        catch (IOException exception)
        {
            throw new SiteConfigurationException($"无法读取站点配置文件：{fullPath}", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new SiteConfigurationException($"无权读取站点配置文件：{fullPath}", exception);
        }
    }

    private static JsonDocument ParseDocument(string json, string fullPath)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new SiteConfigurationException($"站点配置不是合法 JSON：{fullPath}", exception);
        }
    }

    private static void ValidateSchemaVersion(JsonElement root, string fullPath)
    {
        if (!root.TryGetProperty("schemaVersion", out var versionElement)
            || versionElement.ValueKind != JsonValueKind.Number
            || !versionElement.TryGetInt32(out var version))
        {
            throw new SiteConfigurationException(
                $"站点配置缺少合法的整数 schemaVersion（期望 {SupportedSchemaVersion}）：{fullPath}");
        }

        if (version != SupportedSchemaVersion)
        {
            throw new SiteConfigurationException(
                $"站点配置 schemaVersion={version} 不受支持（当前仅支持 {SupportedSchemaVersion}）：{fullPath}");
        }
    }

    private static SiteOptions Build(SiteSection site, string fullPath)
    {
        var organizationName = RequireText(site.OrganizationName, "site.organizationName", fullPath);
        var appTitle = RequireText(site.AppTitle, "site.appTitle", fullPath);
        var adminTitle = RequireText(site.AdminTitle, "site.adminTitle", fullPath);
        var coordinateSystem = RequireText(site.CoordinateSystem, "site.coordinateSystem", fullPath);

        if (site.Map is null)
        {
            throw new SiteConfigurationException($"站点配置缺少 site.map：{fullPath}");
        }

        var mapCenter = ValidatePoint(site.Map.Center, "site.map.center", fullPath);
        var searchCity = site.Map.SearchCity ?? string.Empty;

        if (site.Campuses is null || site.Campuses.Count == 0)
        {
            throw new SiteConfigurationException($"站点配置至少需要一个校区（site.campuses）：{fullPath}");
        }

        var campuses = new List<SiteCampusOptions>(site.Campuses.Count);
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var campus in site.Campuses)
        {
            if (campus is null) throw new SiteConfigurationException($"site.campuses 存在空元素：{fullPath}");
            var code = RequireText(campus.Code, "site.campuses[].code", fullPath);
            if (!CampusCodePattern.IsMatch(code))
            {
                throw new SiteConfigurationException(
                    $"校区 code 格式不合法（仅允许小写字母/数字/短横线，首尾不能是短横线）：{code}（{fullPath}）");
            }

            if (!seenCodes.Add(code))
            {
                throw new SiteConfigurationException($"校区 code 重复：{code}（{fullPath}）");
            }

            var name = RequireText(campus.Name, $"site.campuses[{code}].name", fullPath);
            var center = ValidatePoint(campus.Center, $"site.campuses[{code}].center", fullPath);
            campuses.Add(new SiteCampusOptions(code, name, center));
        }

        if (double.IsNaN(site.ServiceRadiusKm) || double.IsInfinity(site.ServiceRadiusKm)
            || site.ServiceRadiusKm <= 0)
        {
            throw new SiteConfigurationException(
                $"site.serviceRadiusKm 必须是大于 0 的有限数值：{FormatNumber(site.ServiceRadiusKm)}（{fullPath}）");
        }

        return new SiteOptions
        {
            OrganizationName = organizationName,
            AppTitle = appTitle,
            AdminTitle = adminTitle,
            CoordinateSystem = coordinateSystem,
            Map = new SiteMapOptions(mapCenter, searchCity),
            Campuses = campuses,
            ServiceRadiusKm = site.ServiceRadiusKm,
            RescuePhone = site.RescuePhone ?? string.Empty
        };
    }

    private static SiteGeoPoint ValidatePoint(GeoPointSection? point, string field, string fullPath)
    {
        if (point is null)
        {
            throw new SiteConfigurationException($"站点配置缺少 {field}：{fullPath}");
        }

        if (!IsFinite(point.Latitude) || point.Latitude is < -90 or > 90)
        {
            throw new SiteConfigurationException($"{field}.latitude 不合法：{FormatNumber(point.Latitude)}（{fullPath}）");
        }

        if (!IsFinite(point.Longitude) || point.Longitude is < -180 or > 180)
        {
            throw new SiteConfigurationException($"{field}.longitude 不合法：{FormatNumber(point.Longitude)}（{fullPath}）");
        }

        return new SiteGeoPoint(point.Latitude, point.Longitude);
    }

    private static string RequireText(string? value, string field, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new SiteConfigurationException($"站点配置 {field} 不能为空：{fullPath}");
        }

        return value.Trim();
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static string FormatNumber(double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private sealed class SiteSection
    {
        public string? OrganizationName { get; init; }
        public string? AppTitle { get; init; }
        public string? AdminTitle { get; init; }
        public string? CoordinateSystem { get; init; }
        public MapSection? Map { get; init; }
        public List<CampusSection?>? Campuses { get; init; }
        public double ServiceRadiusKm { get; init; }
        public string? RescuePhone { get; init; }
    }

    private sealed class MapSection
    {
        public GeoPointSection? Center { get; init; }
        public string? SearchCity { get; init; }
    }

    private sealed class CampusSection
    {
        public string? Code { get; init; }
        public string? Name { get; init; }
        public GeoPointSection? Center { get; init; }
    }

    private sealed class GeoPointSection
    {
        public double Latitude { get; init; }
        public double Longitude { get; init; }
    }
}
