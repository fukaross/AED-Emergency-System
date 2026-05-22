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
}
