using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.DTOs.Common;

namespace AedBackend.Tests.Admin;

public sealed class AdminClassroomApiTests : AdminApiTestBase
{
    private async Task<string> AuthAsync(string username = "root")
    {
        var adminKey = await BootstrapAndLoginAsync(username);
        UseAdminKey(adminKey);
        return adminKey;
    }

    [Fact]
    public async Task KnowledgeSummary_Get_RequiresAuth()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client.GetAsync("/api/admin/knowledge-summary")).StatusCode);
    }

    [Fact]
    public async Task KnowledgeSummary_Put_UpdatesSuccessfully()
    {
        await AuthAsync();

        var putResp = await Client.PutAsJsonAsync("/api/admin/knowledge-summary",
            new { items = new[] { new { key = "test", title = "Test", value = 100, sortOrder = 1 } } });
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var getResp = await Client.GetAsync("/api/admin/knowledge-summary");
        var body = await ReadAsync<ApiResponse<JsonElement>>(getResp);
        Assert.Single(body.Data.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Courses_List_ReturnsCourses()
    {
        await AuthAsync();

        var response = await Client.GetAsync("/api/admin/courses");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadAsync<ApiResponse<JsonElement>>(response);
        Assert.True(body.Data.GetProperty("total").GetInt32() >= 1);
    }

    [Fact]
    public async Task Courses_CreateAndDelete_Workflow()
    {
        await AuthAsync();

        var createResp = await Client.PostAsJsonAsync("/api/admin/courses", new
        {
            title = "Admin 测试课程",
            category = "emergency",
            type = "article",
            pinned = false,
            sortOrder = 99,
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.OK, createResp.StatusCode);
        var created = await ReadAsync<ApiResponse<JsonElement>>(createResp);
        var courseId = created.Data.GetProperty("id").GetString()!;

        var deleteResp = await Client.DeleteAsync($"/api/admin/courses/{courseId}");
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync($"/api/admin/courses/{courseId}")).StatusCode);
    }

    [Fact]
    public async Task Courses_Update_RejectsEmptyRequiredFields()
    {
        await AuthAsync();

        var titleResp = await Client.PutAsJsonAsync("/api/admin/courses/course-01", new
        {
            title = " "
        });
        Assert.Equal(HttpStatusCode.BadRequest, titleResp.StatusCode);

        var categoryResp = await Client.PutAsJsonAsync("/api/admin/courses/course-01", new
        {
            category = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, categoryResp.StatusCode);
    }

    [Fact]
    public async Task QuickActions_List_IncludesDisabled()
    {
        await AuthAsync();

        var response = await Client.GetAsync("/api/admin/quick-actions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadAsync<ApiResponse<JsonElement>>(response);
        Assert.True(body.Data.GetArrayLength() >= 2);
        Assert.True(body.Data[0].TryGetProperty("icon", out _));
    }

    [Fact]
    public async Task QuickActions_CreateAndUpdate_Workflow()
    {
        await AuthAsync();

        var createResp = await Client.PostAsJsonAsync("/api/admin/quick-actions", new
        {
            title = "Admin 测试步骤",
            icon = "/test.png",
            summary = "测试摘要",
            sortOrder = 10,
            isEnabled = true,
            steps = new[]
            {
                new { order = 1, title = "步骤1", description = "描述1", image = "/test.jpg", keyPoints = new[] { "要点A" } }
            }
        });
        Assert.Equal(HttpStatusCode.OK, createResp.StatusCode);
        var created = await ReadAsync<ApiResponse<JsonElement>>(createResp);
        var actionId = created.Data.GetProperty("id").GetString()!;

        var updateResp = await Client.PutAsJsonAsync($"/api/admin/quick-actions/{actionId}", new
        {
            title = "更新后的标题"
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        var deleteResp = await Client.DeleteAsync($"/api/admin/quick-actions/{actionId}");
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);
    }

    [Fact]
    public async Task QuickActions_Update_RejectsEmptyRequiredFields()
    {
        await AuthAsync();

        var titleResp = await Client.PutAsJsonAsync("/api/admin/quick-actions/qa-01", new
        {
            title = " "
        });
        Assert.Equal(HttpStatusCode.BadRequest, titleResp.StatusCode);

        var iconResp = await Client.PutAsJsonAsync("/api/admin/quick-actions/qa-01", new
        {
            icon = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, iconResp.StatusCode);
    }
}
