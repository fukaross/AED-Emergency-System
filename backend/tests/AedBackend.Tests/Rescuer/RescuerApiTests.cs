using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Tests.Admin;

namespace AedBackend.Tests.Rescuer;

public sealed class RescuerApiTests : AdminApiTestBase
{
    private const double EastLat = 31.245;
    private const double EastLng = 121.505;

    [Fact]
    public async Task NearbyRescuers_RequiresLogin()
    {
        var response = await Client.GetAsync(
            $"/api/rescuers/nearby?lat={EastLat}&lng={EastLng}");

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task NearbyRescuers_ValidatesCoordinates()
    {
        SeedLoggedInExperienceUser();

        var response = await Client.GetAsync("/api/rescuers/nearby?lat=abc&lng=110");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task NearbyRescuers_ReturnsCampusScopedContract()
    {
        SeedRescuers(
            SyntheticRescuer("rescuer-east-a", "演示急救员甲", "east"),
            SyntheticRescuer("rescuer-east-b", "演示急救员乙", "east"),
            SyntheticRescuer("rescuer-west", "演示急救员丙", "west", lat: 31.2188, lng: 121.4536));
        SeedLoggedInExperienceUser();

        var response = await Client.GetAsync(
            $"/api/rescuers/nearby?lat={EastLat}&lng={EastLng}");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
        var first = data.GetProperty("items")[0];

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // 校区 code/名称与服务半径全部来自 config/site.json，不是源码常量。
        Assert.Equal("east", data.GetProperty("campus").GetString());
        Assert.Equal("东区", data.GetProperty("campusName").GetString());
        Assert.Equal(30000, data.GetProperty("autoOfflineRadius").GetInt32());
        Assert.True(data.GetProperty("viewerCampusDistance").GetInt32() < 10);
        Assert.Equal(2, data.GetProperty("items").GetArrayLength());
        Assert.Equal("east", first.GetProperty("campus").GetString());
        Assert.Equal("东区", first.GetProperty("campusName").GetString());
        Assert.True(first.GetProperty("distance").GetInt32() >= 0);
        Assert.Contains("****", first.GetProperty("phoneMasked").GetString());
        Assert.True(first.GetProperty("skills").GetProperty("cpr").GetBoolean());
        Assert.False(first.TryGetProperty("phone", out _));
    }

    [Fact]
    public async Task NearbyRescuers_ReturnsEmptyItemsOutsideServiceRadius()
    {
        SeedRescuers(SyntheticRescuer("rescuer-east-a", "演示急救员甲", "east"));
        SeedLoggedInExperienceUser();

        var response = await Client.GetAsync("/api/rescuers/nearby?lat=30&lng=120");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(data.GetProperty("viewerCampusDistance").GetInt32() > 30000);
        Assert.Equal(0, data.GetProperty("items").GetArrayLength());
    }

    /**
     * 30 km 边界语义保留：到所属校区中心 > serviceRadiusKm*1000 才判离线，
     * 等于边界仍在线。坐标取自 demo 合成边界样本，服务半径来自 config/site.json。
     */
    [Fact]
    public async Task NearbyRescuers_ServiceRadiusBoundary_KeepsEdgeOnlineAndFarOffline()
    {
        SeedRescuers(
            SyntheticRescuer("rescuer-near", "演示急救员近点", "east"),
            SyntheticRescuer("rescuer-edge", "演示急救员边界", "east", lat: 31.054033127, lng: 121.727690418),
            SyntheticRescuer("rescuer-far", "演示急救员远端", "east", lat: 31.549165599, lng: 121.634936175));
        SeedLoggedInExperienceUser();

        var response = await Client.GetAsync(
            $"/api/rescuers/nearby?lat={EastLat}&lng={EastLng}");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
        var items = data.GetProperty("items").EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetString()!, item => item);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(30000, data.GetProperty("autoOfflineRadius").GetInt32());
        Assert.Equal(3, items.Count);
        Assert.Equal("online", items["rescuer-near"].GetProperty("availabilityStatus").GetString());
        Assert.Equal("online", items["rescuer-edge"].GetProperty("availabilityStatus").GetString());
        Assert.Equal("offline", items["rescuer-far"].GetProperty("availabilityStatus").GetString());
        Assert.Equal(30000, items["rescuer-edge"].GetProperty("distance").GetInt32());
        Assert.Equal(36000, items["rescuer-far"].GetProperty("distance").GetInt32());
    }

    [Fact]
    public async Task Detail_ReturnsCallablePhoneAndSkills()
    {
        SeedRescuers(SyntheticRescuer("rescuer-001", "演示急救员甲", "east", phone: "13900000001"));
        SeedLoggedInExperienceUser();

        var response = await Client.GetAsync("/api/rescuers/rescuer-001");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("rescuer-001", data.GetProperty("id").GetString());
        Assert.Equal("13900000001", data.GetProperty("phone").GetString());
        Assert.Equal("139****0001", data.GetProperty("phoneMasked").GetString());
        Assert.Equal("东区", data.GetProperty("campusName").GetString());
        Assert.Equal("online", data.GetProperty("availabilityStatus").GetString());
        Assert.True(data.GetProperty("skills").GetProperty("aed").GetBoolean());
    }

    [Fact]
    public async Task AdminAvatarUpdate_IsVisibleInPublicRescuerApis()
    {
        SeedRescuers(SyntheticRescuer("rescuer-001", "演示急救员甲", "east"));
        UseAdminKey(await BootstrapAndLoginAsync());
        const string avatarUrl = "https://cdn.example.test/uploads/rescuer/avatar-new.jpg";

        var updated = await Client.PutAsJsonAsync(
            "/api/admin/rescuers/rescuer-001",
            new { avatarUrl });
        var adminData = (await ReadAsync<ApiResponse<JsonElement>>(updated)).Data;

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(avatarUrl, adminData.GetProperty("avatarUrl").GetString());

        SeedLoggedInExperienceUser();
        var nearby = await Client.GetAsync(
            $"/api/rescuers/nearby?lat={EastLat}&lng={EastLng}");
        var nearbyData = (await ReadAsync<ApiResponse<JsonElement>>(nearby)).Data;
        var item = nearbyData.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("id").GetString() == "rescuer-001");

        Assert.Equal(HttpStatusCode.OK, nearby.StatusCode);
        Assert.Equal(avatarUrl, item.GetProperty("avatarUrl").GetString());

        var detail = await Client.GetAsync("/api/rescuers/rescuer-001");
        var detailData = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(avatarUrl, detailData.GetProperty("avatarUrl").GetString());
    }

    [Fact]
    public async Task Detail_Returns404ForMissingRescuer()
    {
        SeedRescuers(SyntheticRescuer("rescuer-001", "演示急救员甲", "east"));
        SeedLoggedInExperienceUser();

        var response = await Client.GetAsync("/api/rescuers/rescuer-999");

        await AssertErrorAsync(response, HttpStatusCode.NotFound, ErrorCodes.RescuerNotFound);
    }

    private static RescuerSeed SyntheticRescuer(
        string id,
        string name,
        string campus,
        double lat = EastLat,
        double lng = EastLng,
        string phone = "13900000001")
    {
        return new RescuerSeed(id, name, campus, Lat: lat, Lng: lng, Phone: phone);
    }
}
