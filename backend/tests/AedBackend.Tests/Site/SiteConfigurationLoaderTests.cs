using AedBackend.Infrastructure.Site;

namespace AedBackend.Tests.Site;

public sealed class SiteConfigurationLoaderTests
{
    [Fact]
    public void Load_ValidConfig_ReturnsSiteOptions()
    {
        var path = WriteSite(ValidSiteJson());
        try
        {
            var site = SiteConfigurationLoader.Load(path);

            Assert.Equal("示范园区", site.OrganizationName);
            Assert.Equal("gcj02", site.CoordinateSystem);
            Assert.Equal(2, site.Campuses.Count);
            Assert.Equal("east", site.Campuses[0].Code);
            Assert.Equal("东区", site.Campuses[0].Name);
            Assert.Equal(30000, site.ServiceRadiusMeters);
            Assert.Equal("上海", site.Map.SearchCity);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    /** 证明构建产物按固定相对路径存在且可加载（csproj Content Link 生效）。 */
    [Fact]
    public void LoadDefault_ShippedArtifactIsValid()
    {
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "config", "site.json");
        Assert.Equal(expectedPath, SiteConfigurationLoader.DefaultPath);
        Assert.True(File.Exists(expectedPath), $"未找到构建产物站点配置：{expectedPath}");

        var site = SiteConfigurationLoader.LoadDefault();

        Assert.False(string.IsNullOrWhiteSpace(site.OrganizationName));
        Assert.True(site.Campuses.Count >= 1);
        Assert.True(site.ServiceRadiusKm > 0);
    }

    [Fact]
    public void Load_FailsWhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-site-{Guid.NewGuid():N}", "site.json");

        var exception = Assert.Throws<SiteConfigurationException>(() => SiteConfigurationLoader.Load(path));

        Assert.Contains("缺少站点配置", exception.Message, StringComparison.Ordinal);
        Assert.Contains(Path.GetFullPath(path), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"site": {}}""")]
    [InlineData("""{"schemaVersion": 2, "site": {}}""")]
    [InlineData("""{"schemaVersion": "1", "site": {}}""")]
    public void Load_FailsOnMissingOrUnsupportedSchemaVersion(string json)
    {
        var path = WriteSite(json);
        try
        {
            var exception = Assert.Throws<SiteConfigurationException>(() => SiteConfigurationLoader.Load(path));
            Assert.Contains("schemaVersion", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Fact]
    public void Load_FailsWhenOrganizationNameIsEmpty()
    {
        var path = WriteSite(ValidSiteJson(organizationName: "  "));
        try
        {
            var exception = Assert.Throws<SiteConfigurationException>(() => SiteConfigurationLoader.Load(path));
            Assert.Contains("organizationName", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Fact]
    public void Load_FailsWhenNoCampusConfigured()
    {
        var path = WriteSite(ValidSiteJson(campuses: "[]"));
        try
        {
            var exception = Assert.Throws<SiteConfigurationException>(() => SiteConfigurationLoader.Load(path));
            Assert.Contains("至少需要一个校区", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Fact]
    public void Load_FailsOnDuplicateCampusCode()
    {
        const string campuses = """
            [
              {"code": "east", "name": "东区", "center": {"latitude": 31.245, "longitude": 121.505}},
              {"code": "east", "name": "东区副本", "center": {"latitude": 31.246, "longitude": 121.506}}
            ]
            """;
        var path = WriteSite(ValidSiteJson(campuses: campuses));
        try
        {
            var exception = Assert.Throws<SiteConfigurationException>(() => SiteConfigurationLoader.Load(path));
            Assert.Contains("校区 code 重复", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Theory]
    [InlineData("East")]
    [InlineData("east-")]
    [InlineData("east_1")]
    [InlineData("东区")]
    public void Load_FailsOnInvalidCampusCode(string code)
    {
        var campuses = $"[{{\"code\": \"{code}\", \"name\": \"东区\", "
            + "\"center\": {\"latitude\": 31.245, \"longitude\": 121.505}}]";
        var path = WriteSite(ValidSiteJson(campuses: campuses));
        try
        {
            var exception = Assert.Throws<SiteConfigurationException>(() => SiteConfigurationLoader.Load(path));
            Assert.Contains("校区 code 格式不合法", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Theory]
    [InlineData(91, 121.5)]
    [InlineData(31.2, 181)]
    public void Load_FailsOnInvalidCampusCoordinates(double latitude, double longitude)
    {
        var campuses = "[{\"code\": \"east\", \"name\": \"东区\", \"center\": "
            + $"{{\"latitude\": {latitude}, \"longitude\": {longitude}}}}}]";
        var path = WriteSite(ValidSiteJson(campuses: campuses));
        try
        {
            var exception = Assert.Throws<SiteConfigurationException>(() => SiteConfigurationLoader.Load(path));
            Assert.Contains("不合法", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Load_FailsOnNonPositiveServiceRadius(double radiusKm)
    {
        var path = WriteSite(ValidSiteJson(serviceRadiusKm: radiusKm));
        try
        {
            var exception = Assert.Throws<SiteConfigurationException>(() => SiteConfigurationLoader.Load(path));
            Assert.Contains("serviceRadiusKm", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Fact]
    public void Load_FailsWhenMapCenterIsInvalid()
    {
        var json = """
            {
              "schemaVersion": 1,
              "site": {
                "organizationName": "示范园区",
                "appTitle": "应用",
                "adminTitle": "管理端",
                "coordinateSystem": "gcj02",
                "map": { "center": { "latitude": 95, "longitude": 121.4 }, "searchCity": "" },
                "campuses": [
                  {"code": "east", "name": "东区", "center": {"latitude": 31.245, "longitude": 121.505}}
                ],
                "serviceRadiusKm": 30,
                "rescuePhone": ""
              }
            }
            """;
        var path = WriteSite(json);
        try
        {
            var exception = Assert.Throws<SiteConfigurationException>(() => SiteConfigurationLoader.Load(path));
            Assert.Contains("site.map.center.latitude", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Fact]
    public void ResolveNearestCampus_TieKeepsConfigurationOrder()
    {
        var path = WriteSite(ValidSiteJson(campuses: """
            [
              {"code": "first", "name": "第一校区", "center": {"latitude": 31.0, "longitude": 121.0}},
              {"code": "second", "name": "第二校区", "center": {"latitude": 31.0, "longitude": 121.0}}
            ]
            """));
        try
        {
            var site = SiteConfigurationLoader.Load(path);

            var (campus, distance) = site.ResolveNearestCampus(new SiteGeoPoint(31.0, 121.0));

            Assert.Equal("first", campus.Code);
            Assert.Equal(0, distance);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Fact]
    public void ResolveCampusName_UnknownCode_DoesNotFallbackToDefaultCampus()
    {
        var path = WriteSite(ValidSiteJson());
        try
        {
            var site = SiteConfigurationLoader.Load(path);

            Assert.Null(site.FindCampus("nowhere"));
            Assert.Equal("nowhere", site.ResolveCampusName("nowhere"));
            Assert.Equal("", site.ResolveCampusName(null));
        }
        finally
        {
            DeleteFile(path);
        }
    }

    [Fact]
    public void DistanceMeters_RoundsToWholeMeters()
    {
        var same = SiteOptions.DistanceMeters(new SiteGeoPoint(31.245, 121.505), new SiteGeoPoint(31.245, 121.505));
        var offset = SiteOptions.DistanceMeters(new SiteGeoPoint(31.245, 121.505), new SiteGeoPoint(31.246, 121.505));

        Assert.Equal(0, same);
        Assert.True(offset is > 100 and < 120);
    }

    private static string ValidSiteJson(
        string organizationName = "示范园区",
        string campuses = """
            [
              {"code": "east", "name": "东区", "center": {"latitude": 31.245, "longitude": 121.505}},
              {"code": "west", "name": "西区", "center": {"latitude": 31.218, "longitude": 121.452}}
            ]
            """,
        double serviceRadiusKm = 30)
    {
        return $$"""
            {
              "schemaVersion": 1,
              "site": {
                "organizationName": "{{organizationName}}",
                "appTitle": "AED 急救协作",
                "adminTitle": "AED 管理端",
                "coordinateSystem": "gcj02",
                "map": {
                  "center": { "latitude": 31.2315, "longitude": 121.4785 },
                  "searchCity": "上海"
                },
                "campuses": {{campuses}},
                "serviceRadiusKm": {{serviceRadiusKm}},
                "rescuePhone": ""
              }
            }
            """;
    }

    private static string WriteSite(string json)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"site-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "site.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static void DeleteFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
