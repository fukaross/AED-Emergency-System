using System.Text.Json;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Help;

namespace AedBackend.Tests.Admin;

public sealed class AdminDashboardApiTests : AdminApiTestBase
{
    [Fact]
    public async Task Dashboard_ReturnsCountsAndRecentLogSummary()
    {
        SeedExperienceUsers(("openid-1", "REDACTED"), ("openid-2", "REDACTED"));
        SeedHelpRequests(
            Help("help-1", HelpStatuses.PendingReview),
            Help("help-2", HelpStatuses.Published) with { CreatedAt = DateTimeOffset.UtcNow });
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.GetAsync("/api/admin/dashboard");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
        var recentLog = data.GetProperty("recentLogs")[0];

        Assert.Equal(2, data.GetProperty("usersTotal").GetInt32());
        Assert.Equal(2, data.GetProperty("helpRequestsTotal").GetInt32());
        Assert.Equal(1, data.GetProperty("pendingReviewTotal").GetInt32());
        Assert.True(data.GetProperty("todayNewHelpRequests").GetInt32() >= 1);
        Assert.True(recentLog.TryGetProperty("eventName", out _));
        Assert.False(recentLog.TryGetProperty("metadata", out _));
    }

    [Fact]
    public async Task Dashboard_ReturnsChartSeries()
    {
        SeedExperienceUsers(("openid-1", "REDACTED"), ("openid-2", "REDACTED"));
        // r3 无发证日期，应被急救员增长聚合显式跳过
        SeedResponders(("r1", "2023-05-01"), ("r2", "2024-06-01"), ("r3", null));
        var today = DateTimeOffset.UtcNow;
        SeedHelpRequests(
            Help("help-1", HelpStatuses.Closed) with { CreatedAt = today },
            Help("help-2", HelpStatuses.Published) with { CreatedAt = today },
            Help("help-3", HelpStatuses.PendingReview) with { CreatedAt = today });
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.GetAsync("/api/admin/dashboard");
        var charts = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data.GetProperty("charts");

        var userGrowth = charts.GetProperty("userGrowth");
        Assert.Equal(30, userGrowth.GetArrayLength());
        Assert.Equal(2, userGrowth[29].GetProperty("total").GetInt32());

        var responderGrowth = charts.GetProperty("responderGrowth");
        Assert.Equal(30, responderGrowth.GetArrayLength());
        Assert.Equal(2, responderGrowth[29].GetProperty("total").GetInt32());

        var helpProcessing = charts.GetProperty("helpProcessing");
        Assert.Equal(14, helpProcessing.GetArrayLength());
        var lastDay = helpProcessing[13];
        Assert.Equal(1, lastDay.GetProperty("processed").GetInt32());
        Assert.Equal(2, lastDay.GetProperty("pending").GetInt32());

        Assert.Equal(3, charts.GetProperty("statusDistribution").GetArrayLength());
    }
}
