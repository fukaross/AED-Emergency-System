using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Help;

namespace AedBackend.Tests.Admin;

public sealed class AdminHelpApiTests : AdminApiTestBase
{
    [Fact]
    public async Task ListHelpRequests_FiltersByStatusKeywordAndPaginates()
    {
        SeedHelpRequests(
            Help("help-1", content: "心肺复苏协助", address: "图书馆", requestNo: "HELP1"),
            Help("help-2", HelpStatuses.PendingReview, content: "跌倒求助", address: "操场", requestNo: "HELP2"));
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.GetAsync("/api/admin/help-requests?status=pending_review&keyword=操场&page=1&pageSize=1");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, data.GetProperty("total").GetInt32());
        Assert.Equal("help-2", data.GetProperty("items")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Detail_IncludesRequestImagesAndReportImages()
    {
        SeedHelpRequests(Help("help-1"));
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.GetAsync("/api/admin/help-requests/help-1");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal("/uploads/help/a.jpg", data.GetProperty("images")[0].GetString());
        Assert.Equal(1, data.GetProperty("reportersCount").GetInt32());
        Assert.Equal("reporter-1", data.GetProperty("reporters")[0].GetProperty("openId").GetString());
        Assert.Equal("/uploads/report/b.jpg", data.GetProperty("reports")[0].GetProperty("images")[0].GetString());
        Assert.Equal("reporter-1", data.GetProperty("reports")[0].GetProperty("reporterOpenId").GetString());
    }

    [Theory]
    [InlineData(HelpStatuses.Closed, "help.close")]
    [InlineData(HelpStatuses.FalseAlarm, "help.false_alarm")]
    public async Task CloseHelpRequest_UpdatesStatusAndWritesAudit(string status, string eventName)
    {
        SeedHelpRequests(Help("help-1"));
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.PutAsJsonAsync(
            "/api/admin/help-requests/help-1/close",
            new { status, note = "已核验" });
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(status, data.GetProperty("status").GetString());

        var logs = await Client.GetAsync($"/api/admin/audit-logs?eventName={eventName}");
        var logData = (await ReadAsync<ApiResponse<JsonElement>>(logs)).Data;
        Assert.Equal(1, logData.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task CloseHelpRequest_RejectsInvalidStatusAndClosedRecord()
    {
        SeedHelpRequests(Help("help-1") with { ClosedAt = DateTimeOffset.Parse("2026-05-23T02:00:00+00:00"), Status = HelpStatuses.Closed });
        UseAdminKey(await BootstrapAndLoginAsync());

        var invalid = await Client.PutAsJsonAsync(
            "/api/admin/help-requests/help-1/close",
            new { status = "published", note = "bad" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var closed = await Client.PutAsJsonAsync(
            "/api/admin/help-requests/help-1/close",
            new { status = HelpStatuses.FalseAlarm, note = "again" });
        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
    }
}
