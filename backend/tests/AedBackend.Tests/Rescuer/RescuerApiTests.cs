using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Tests.Admin;

namespace AedBackend.Tests.Rescuer;

public sealed class RescuerApiTests : AdminApiTestBase
{
    [Fact]
    public async Task NearbyRescuers_RequiresLogin()
    {
        var response = await Client.GetAsync(
            "/api/rescuers/nearby?lat=REDACTED&lng=REDACTED");

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
        SeedLoggedInExperienceUser();

        var response = await Client.GetAsync(
            "/api/rescuers/nearby?lat=REDACTED&lng=REDACTED");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
        var first = data.GetProperty("items")[0];

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("guilinyang", data.GetProperty("campus").GetString());
        Assert.Equal("REDACTED校区", data.GetProperty("campusName").GetString());
        Assert.Equal(30000, data.GetProperty("autoOfflineRadius").GetInt32());
        Assert.True(data.GetProperty("viewerCampusDistance").GetInt32() < 10);
        Assert.True(data.GetProperty("items").GetArrayLength() > 0);
        Assert.Equal("guilinyang", first.GetProperty("campus").GetString());
        Assert.Equal("REDACTED校区", first.GetProperty("campusName").GetString());
        Assert.True(first.GetProperty("distance").GetInt32() >= 0);
        Assert.Contains("****", first.GetProperty("phoneMasked").GetString());
        Assert.True(first.GetProperty("skills").GetProperty("cpr").GetBoolean());
        Assert.False(first.TryGetProperty("phone", out _));
    }

    [Fact]
    public async Task NearbyRescuers_ReturnsEmptyItemsOutsideServiceRadius()
    {
        SeedLoggedInExperienceUser();

        var response = await Client.GetAsync("/api/rescuers/nearby?lat=30&lng=120");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(data.GetProperty("viewerCampusDistance").GetInt32() > 30000);
        Assert.Equal(0, data.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Detail_ReturnsCallablePhoneAndSkills()
    {
        SeedLoggedInExperienceUser();

        var response = await Client.GetAsync("/api/rescuers/rescuer-001");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("rescuer-001", data.GetProperty("id").GetString());
        Assert.Equal("REDACTED", data.GetProperty("phone").GetString());
        Assert.Equal("182****7261", data.GetProperty("phoneMasked").GetString());
        Assert.Equal("REDACTED校区", data.GetProperty("campusName").GetString());
        Assert.Equal("online", data.GetProperty("availabilityStatus").GetString());
        Assert.True(data.GetProperty("skills").GetProperty("aed").GetBoolean());
    }

    [Fact]
    public async Task AdminAvatarUpdate_IsVisibleInPublicRescuerApis()
    {
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
            "/api/rescuers/nearby?lat=REDACTED&lng=REDACTED");
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
        SeedLoggedInExperienceUser();

        var response = await Client.GetAsync("/api/rescuers/rescuer-999");

        await AssertErrorAsync(response, HttpStatusCode.NotFound, ErrorCodes.RescuerNotFound);
    }
}
