using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Course;
using AedBackend.Core.DTOs.Knowledge;

namespace AedBackend.Tests.Admin;

public sealed class ClassroomApiTests : AdminApiTestBase
{
    [Fact]
    public async Task GetKnowledgeSummary_ReturnsSeededStats()
    {
        var response = await Client.GetAsync("/api/knowledge/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadAsync<ApiResponse<KnowledgeSummaryResponse>>(response);
        Assert.Equal(0, body.Code);
        Assert.NotNull(body.Data);
        Assert.Equal(4, body.Data.Items.Count);
        Assert.Contains(body.Data.Items, i => i.Key == "aed_count" && Math.Abs(i.Value - 24) < 0.01);
        Assert.Contains(body.Data.Items, i => i.Key == "trained_people");
    }

    [Fact]
    public async Task ListCourses_ReturnsSeededCourses()
    {
        var response = await Client.GetAsync("/api/courses");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadAsync<ApiResponse<CourseListPage>>(response);
        Assert.Equal(0, body.Code);
        Assert.NotNull(body.Data);

        var data = body.Data;
        Assert.True(data.Items.Count >= 2);
        Assert.Equal(20, data.PageSize);
        Assert.Contains(data.Items, c => c.Id == "course-01" && c.Title == "AED 使用教程");
        Assert.Contains(data.Items, c => c.Id == "course-02" && c.Title == "心肺复苏教程");
        Assert.All(data.Items, c => Assert.NotNull(c.CacheVersion));
    }

    [Fact]
    public async Task ListCourses_SupportsPaging()
    {
        var response = await Client.GetAsync("/api/courses?page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadAsync<ApiResponse<CourseListPage>>(response);
        Assert.Equal(0, body.Code);
        Assert.NotNull(body.Data);
        Assert.Single(body.Data.Items);
        Assert.True(body.Data.Total >= 4);
    }

    [Fact]
    public async Task GetCourseDetail_ReturnsFullDetail()
    {
        var response = await Client.GetAsync("/api/courses/course-01");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadAsync<ApiResponse<CourseDetailResponse>>(response);
        Assert.Equal(0, body.Code);
        Assert.NotNull(body.Data);
        Assert.Equal("course-01", body.Data.Id);
        Assert.Equal("AED 使用教程", body.Data.Title);
        Assert.NotEmpty(body.Data.Content);
        Assert.NotNull(body.Data.CacheVersion);
        Assert.NotEmpty(body.Data.Disclaimer);
        Assert.Empty(body.Data.Videos);
        Assert.Single(body.Data.Steps);
    }

    [Fact]
    public async Task GetCourseDetail_Returns404_WhenNotFound()
    {
        var response = await Client.GetAsync("/api/courses/course-99");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await ReadAsync<ApiResponse<JsonElement>>(response);
        Assert.Equal(2002, body.Code);
    }

    [Fact]
    public async Task IncrementCourseView_IncrementsAndReturnsNewCount()
    {
        var first = await Client.PostAsync("/api/courses/course-01/views", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await ReadAsync<ApiResponse<CourseViewResponse>>(first);
        Assert.Equal(0, firstBody.Code);
        Assert.NotNull(firstBody.Data);

        var second = await Client.PostAsync("/api/courses/course-01/views", null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await ReadAsync<ApiResponse<CourseViewResponse>>(second);
        Assert.Equal(firstBody.Data.ViewCount + 1, secondBody.Data!.ViewCount);

        var detail = await Client.GetAsync("/api/courses/course-01");
        var detailBody = await ReadAsync<ApiResponse<CourseDetailResponse>>(detail);
        Assert.Equal(secondBody.Data.ViewCount, detailBody.Data!.ViewCount);
    }

    [Fact]
    public async Task IncrementCourseView_Returns404_WhenNotFound()
    {
        var response = await Client.PostAsync("/api/courses/course-99/views", null);
        await AssertErrorAsync(response, HttpStatusCode.NotFound, 2002);
    }

    [Fact]
    public async Task ListQuickActions_ReturnsSeededActions()
    {
        var response = await Client.GetAsync("/api/quick-actions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadAsync<ApiResponse<JsonElement>>(response);
        Assert.Equal(0, body.Code);
        var data = body.Data;
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.True(data.GetArrayLength() >= 2);

        var first = data[0];
        Assert.Equal("qa-01", first.GetProperty("id").GetString());
        Assert.Equal("心肺复苏", first.GetProperty("title").GetString());
        Assert.True(first.GetProperty("steps").GetArrayLength() >= 2);
    }

    [Fact]
    public async Task GetQuickActionDetail_ReturnsFullDetail()
    {
        var response = await Client.GetAsync("/api/quick-actions/qa-01");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadAsync<ApiResponse<QuickActionResponse>>(response);
        Assert.Equal(0, body.Code);
        Assert.NotNull(body.Data);
        Assert.Equal("qa-01", body.Data.Id);
        Assert.Equal("心肺复苏", body.Data.Title);
        Assert.True(body.Data.Steps.Count >= 2);
        Assert.Contains(body.Data.Steps, s => s.Title == "判断意识");
    }

    [Fact]
    public async Task GetQuickActionDetail_Returns404_WhenNotFound()
    {
        var response = await Client.GetAsync("/api/quick-actions/qa-99");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await ReadAsync<ApiResponse<JsonElement>>(response);
        Assert.Equal(2002, body.Code);
    }
}
