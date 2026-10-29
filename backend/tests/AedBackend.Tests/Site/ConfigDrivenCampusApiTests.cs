using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Infrastructure.Site;
using AedBackend.Tests.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AedBackend.Tests.Site;

/**
 * 校区/服务半径完全由 SiteOptions 驱动：新增第三校区不需要改源码枚举，
 * 后端查询、显示名、Admin 校验收敛到同一份配置。
 */
public sealed class ConfigDrivenCampusApiTests : AdminApiTestBase
{
    private const double NorthLat = 31.3;
    private const double NorthLng = 121.5;

    private static readonly SiteOptions TripleCampusSite = new()
    {
        OrganizationName = "测试园区",
        AppTitle = "测试应用",
        AdminTitle = "测试管理端",
        CoordinateSystem = "gcj02",
        Map = new SiteMapOptions(new SiteGeoPoint(31.25, 121.48), "测试市"),
        Campuses =
        [
            new SiteCampusOptions("east", "东区", new SiteGeoPoint(31.245, 121.505)),
            new SiteCampusOptions("west", "西区", new SiteGeoPoint(31.218, 121.452)),
            new SiteCampusOptions("north", "北区", new SiteGeoPoint(NorthLat, NorthLng))
        ],
        ServiceRadiusKm = 12,
        RescuePhone = ""
    };

    protected override Action<IServiceCollection>? ConfigureServices => services =>
    {
        services.RemoveAll<SiteOptions>();
        services.AddSingleton(TripleCampusSite);
    };

    [Fact]
    public async Task NearbyRescuers_UsesThirdCampusAndRadiusFromConfiguration()
    {
        SeedRescuers(
            new RescuerSeed("rescuer-north", "演示急救员北区", "north", Lat: NorthLat, Lng: NorthLng),
            new RescuerSeed("rescuer-east", "演示急救员东区", "east"));
        SeedLoggedInExperienceUser();

        var response = await Client.GetAsync($"/api/rescuers/nearby?lat={NorthLat}&lng={NorthLng}");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("north", data.GetProperty("campus").GetString());
        Assert.Equal("北区", data.GetProperty("campusName").GetString());
        Assert.Equal(12000, data.GetProperty("autoOfflineRadius").GetInt32());
        Assert.Equal(1, data.GetProperty("items").GetArrayLength());
        Assert.Equal("north", data.GetProperty("items")[0].GetProperty("campus").GetString());
        Assert.Equal("北区", data.GetProperty("items")[0].GetProperty("campusName").GetString());
    }

    [Fact]
    public async Task RescuerDetail_ResolvesConfiguredCampusDisplayName()
    {
        SeedRescuers(new RescuerSeed("rescuer-north", "演示急救员北区", "north", Lat: NorthLat, Lng: NorthLng));
        SeedLoggedInExperienceUser();

        var response = await Client.GetAsync("/api/rescuers/rescuer-north");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("north", data.GetProperty("campus").GetString());
        Assert.Equal("北区", data.GetProperty("campusName").GetString());
    }

    [Fact]
    public async Task AdminRescuerUpdate_AcceptsConfiguredThirdCampusAndRejectsUnknownCode()
    {
        SeedRescuers(new RescuerSeed("rescuer-001", "演示急救员甲", "east"));
        UseAdminKey(await BootstrapAndLoginAsync());

        var accepted = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new { campus = "north" });
        var acceptedData = (await ReadAsync<ApiResponse<JsonElement>>(accepted)).Data;
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("north", acceptedData.GetProperty("campus").GetString());

        var rejected = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new { campus = "ghost" });
        await AssertErrorAsync(rejected, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
    }
}
