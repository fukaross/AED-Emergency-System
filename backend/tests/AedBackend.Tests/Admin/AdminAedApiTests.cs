using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;

namespace AedBackend.Tests.Admin;

public sealed class AdminAedApiTests : AdminApiTestBase
{
    [Fact]
    public async Task ListDevices_RequiresAdminSessionAndRejectsLegacyKey()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/admin/aed-devices")).StatusCode);

        UseLegacyKey();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/admin/aed-devices")).StatusCode);

        UseAdminKey(await BootstrapAndLoginAsync());
        var response = await Client.GetAsync("/api/admin/aed-devices?status=offline&page=1&pageSize=10");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(data.GetProperty("total").GetInt32() >= 2);
        Assert.All(data.GetProperty("items").EnumerateArray(), item =>
            Assert.Equal("offline", item.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task CreateDevice_ValidatesDuplicateNameOfflineNoteAndWritesAudit()
    {
        UseAdminKey(await BootstrapAndLoginAsync());

        var missingNote = await Client.PostAsJsonAsync("/api/admin/aed-devices", CreatePayload(status: "offline"));
        await AssertErrorAsync(missingNote, HttpStatusCode.BadRequest, ErrorCodes.AedOfflineRequiresNote);

        var created = await Client.PostAsJsonAsync(
            "/api/admin/aed-devices",
            CreatePayload(status: "offline", statusNote: "测试离线"));
        var item = (await ReadAsync<ApiResponse<JsonElement>>(created)).Data;
        var id = item.GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal("测试 AED API", item.GetProperty("name").GetString());
        Assert.Equal("offline", item.GetProperty("status").GetString());

        var duplicate = await Client.PostAsJsonAsync("/api/admin/aed-devices", CreatePayload());
        await AssertErrorAsync(duplicate, HttpStatusCode.Conflict, ErrorCodes.AedNameDuplicate);

        var logs = await Client.GetAsync($"/api/admin/audit-logs?eventName=device.created&keyword={id}");
        var logData = (await ReadAsync<ApiResponse<JsonElement>>(logs)).Data;
        Assert.Equal(1, logData.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task UpdateDevice_MapsValidationErrorsAndWritesStatusChangedAudit()
    {
        UseAdminKey(await BootstrapAndLoginAsync());
        var created = await Client.PostAsJsonAsync("/api/admin/aed-devices", CreatePayload());
        var id = (await ReadAsync<ApiResponse<JsonElement>>(created)).Data.GetProperty("id").GetString();

        var invalidStatus = await Client.PutAsJsonAsync($"/api/admin/aed-devices/{id}", new { status = "broken" });
        await AssertErrorAsync(invalidStatus, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var missingNote = await Client.PutAsJsonAsync($"/api/admin/aed-devices/{id}", new { status = "offline", statusNote = "" });
        await AssertErrorAsync(missingNote, HttpStatusCode.BadRequest, ErrorCodes.AedOfflineRequiresNote);

        var updated = await Client.PutAsJsonAsync(
            $"/api/admin/aed-devices/{id}",
            new { status = "offline", statusNote = "例行检修" });
        var item = (await ReadAsync<ApiResponse<JsonElement>>(updated)).Data;

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("offline", item.GetProperty("status").GetString());
        Assert.Equal("例行检修", item.GetProperty("statusNote").GetString());

        var logs = await Client.GetAsync($"/api/admin/audit-logs?eventName=device.status_changed&keyword={id}");
        var logData = (await ReadAsync<ApiResponse<JsonElement>>(logs)).Data;
        Assert.Equal(1, logData.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task DeleteDevice_RemovesDeviceAndWritesAudit()
    {
        UseAdminKey(await BootstrapAndLoginAsync());
        var created = await Client.PostAsJsonAsync("/api/admin/aed-devices", CreatePayload());
        var id = (await ReadAsync<ApiResponse<JsonElement>>(created)).Data.GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.OK, (await Client.DeleteAsync($"/api/admin/aed-devices/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/admin/aed-devices/{id}")).StatusCode);

        var logs = await Client.GetAsync($"/api/admin/audit-logs?eventName=device.deleted&keyword={id}");
        var logData = (await ReadAsync<ApiResponse<JsonElement>>(logs)).Data;
        Assert.Equal(1, logData.GetProperty("total").GetInt32());
    }

    private static object CreatePayload(string status = "online", string? statusNote = null)
    {
        return new
        {
            name = "测试 AED API",
            lat = 19.9701,
            lng = 110.5101,
            image = "/uploads/device/test.webp",
            address = "测试楼一层",
            status,
            statusNote,
            lastMaintenance = "2026-05-28",
            nextMaintenance = "2026-06-28",
            maintainer = "测试维护员"
        };
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode statusCode,
        int errorCode)
    {
        var body = await ReadAsync<ApiResponse<JsonElement>>(response);
        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal(errorCode, body.Code);
    }
}
