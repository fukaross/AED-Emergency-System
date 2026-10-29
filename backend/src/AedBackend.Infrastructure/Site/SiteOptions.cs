namespace AedBackend.Infrastructure.Site;

/** GCJ-02 坐标点；站点配置与档案坐标使用同一坐标系。 */
public readonly record struct SiteGeoPoint(double Latitude, double Longitude);

public sealed record SiteCampusOptions(string Code, string Name, SiteGeoPoint Center);

public sealed record SiteMapOptions(SiteGeoPoint Center, string SearchCity);

/**
 * 共享站点配置（CONTRACT-001）。唯一真源是仓库根 config/site.json，
 * 后端构建时以 Content 链接复制到输出目录 config/site.json，运行时由
 * SiteConfigurationLoader 读取，不再在源码里保存校区/机构常量。
 */
public sealed class SiteOptions
{
    public const string SectionName = "site";

    public required string OrganizationName { get; init; }
    public required string AppTitle { get; init; }
    public required string AdminTitle { get; init; }
    public required string CoordinateSystem { get; init; }
    public required SiteMapOptions Map { get; init; }
    public required IReadOnlyList<SiteCampusOptions> Campuses { get; init; }
    public required double ServiceRadiusKm { get; init; }
    public required string RescuePhone { get; init; }

    /** 服务半径（整数米）；距离比较规则见契约：distance > radius 才超界。 */
    public int ServiceRadiusMeters => (int)Math.Round(ServiceRadiusKm * 1000, MidpointRounding.AwayFromZero);

    public SiteCampusOptions? FindCampus(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return Campuses.FirstOrDefault(campus =>
            campus.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    /** 未知 code 不静默落入某个默认校区：调用方需自行决定错误/降级语义。 */
    public string ResolveCampusName(string? code)
    {
        return FindCampus(code)?.Name ?? code ?? string.Empty;
    }

    /** 最近校区；同距时按配置数组顺序稳定选择（与 CONTRACT-001 一致）。 */
    public (SiteCampusOptions Campus, int DistanceMeters) ResolveNearestCampus(SiteGeoPoint point)
    {
        var best = Campuses[0];
        var bestDistance = DistanceMeters(point, best.Center);
        for (var i = 1; i < Campuses.Count; i++)
        {
            var distance = DistanceMeters(point, Campuses[i].Center);
            if (distance >= bestDistance) continue;
            best = Campuses[i];
            bestDistance = distance;
        }

        return (best, bestDistance);
    }

    /** 与既有实现一致的 Haversine 距离（地球半径 6371 km，整数米四舍五入）。 */
    public static int DistanceMeters(SiteGeoPoint from, SiteGeoPoint to)
    {
        const double earthRadiusMeters = 6371000;
        var lat1 = ToRadians(from.Latitude);
        var lat2 = ToRadians(to.Latitude);
        var latDelta = ToRadians(to.Latitude - from.Latitude);
        var lngDelta = ToRadians(to.Longitude - from.Longitude);

        var a = Math.Sin(latDelta / 2) * Math.Sin(latDelta / 2)
            + Math.Cos(lat1) * Math.Cos(lat2)
            * Math.Sin(lngDelta / 2) * Math.Sin(lngDelta / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return (int)Math.Round(earthRadiusMeters * c);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
