using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;

namespace AedBackend.Tests.Admin;

public sealed class AdminClassroomApiTests : AdminApiTestBase
{
    private const int MaxImageSizeBytes = 5 * 1024 * 1024;
    private const int MaxVideoSizeBytes = 100 * 1024 * 1024;
    private const int OverDefaultRequestLimitVideoSizeBytes = 31 * 1024 * 1024;
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly byte[] WebpBytes =
        [0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50, 5];
    private static readonly byte[] Mp4Bytes =
        [0, 0, 0, 24, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D, 1, 2, 3, 4];

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
    public async Task Courses_CreateVideoCourse_SavesMultipleVideos()
    {
        await AuthAsync();

        var response = await Client.PostAsJsonAsync("/api/admin/courses", new
        {
            title = "多视频课程",
            category = "emergency",
            type = "video",
            pinned = false,
            sortOrder = 10,
            isEnabled = true,
            videos = new[]
            {
                new { order = 2, title = "", url = "https://example.com/b.mp4" },
                new { order = 1, title = "第一段", url = "/uploads/classroom-videos/a.mp4" }
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await ReadAsync<ApiResponse<JsonElement>>(response);
        var videos = created.Data.GetProperty("videos").EnumerateArray().ToArray();

        Assert.Equal("/uploads/classroom-videos/a.mp4", created.Data.GetProperty("videoUrl").GetString());
        Assert.Equal("第一段", videos[0].GetProperty("title").GetString());
        Assert.Equal("视频 2", videos[1].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Courses_CreateVideoCourse_RejectsMissingVideos()
    {
        await AuthAsync();

        var response = await Client.PostAsJsonAsync("/api/admin/courses", new
        {
            title = "空视频课程",
            category = "emergency",
            type = "video",
            pinned = false,
            sortOrder = 10,
            isEnabled = true,
            videos = Array.Empty<object>()
        });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task Courses_UpdateArticleCourse_ClearsVideos()
    {
        await AuthAsync();

        var createResp = await Client.PostAsJsonAsync("/api/admin/courses", new
        {
            title = "待清空视频课程",
            category = "emergency",
            type = "video",
            pinned = false,
            sortOrder = 10,
            isEnabled = true,
            videos = new[] { new { order = 1, title = "视频", url = "https://example.com/a.mp4" } }
        });
        var created = await ReadAsync<ApiResponse<JsonElement>>(createResp);
        var courseId = created.Data.GetProperty("id").GetString()!;

        var updateResp = await Client.PutAsJsonAsync($"/api/admin/courses/{courseId}", new { type = "article" });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await ReadAsync<ApiResponse<JsonElement>>(updateResp);

        Assert.True(updated.Data.GetProperty("videoUrl").ValueKind is JsonValueKind.Null);
        Assert.Empty(updated.Data.GetProperty("videos").EnumerateArray());
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

    [Fact]
    public async Task ClassroomUpload_CourseRequiresAdminSession()
    {
        var response = await UploadImageAsync("course", PngBytes, "image/png", "course.png");
        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task ClassroomUpload_DeduplicatesRepeatedCourseImage()
    {
        await AuthAsync();

        var first = await UploadAndReadDataAsync("course", PngBytes, "image/png", "course.png");
        var second = await UploadAndReadDataAsync("course", PngBytes, "image/png", "course.png");
        var files = Directory.GetFiles(Path.Combine(UploadsRoot, "classroom"));

        Assert.Equal(first.GetProperty("url").GetString(), second.GetProperty("url").GetString());
        Assert.Contains("/uploads/classroom/", first.GetProperty("url").GetString());
        Assert.Equal(64, first.GetProperty("fileId").GetString()?.Length);
        Assert.Single(files);
    }

    [Fact]
    public async Task ClassroomUpload_DeduplicatesAcrossCourseAndQuickAction()
    {
        await AuthAsync();

        var course = await UploadAndReadDataAsync("course", WebpBytes, "image/webp", "course.webp");
        var quickAction = await UploadAndReadDataAsync(
            "quick-action",
            WebpBytes,
            "image/webp",
            "quick-action.webp");
        var files = Directory.GetFiles(Path.Combine(UploadsRoot, "classroom"));

        Assert.Equal(course.GetProperty("url").GetString(), quickAction.GetProperty("url").GetString());
        Assert.Single(files);
    }

    [Fact]
    public async Task ClassroomUpload_KeepsDifferentImagesSeparate()
    {
        await AuthAsync();
        var changedPng = PngBytes.ToArray();
        changedPng[^1] = 9;

        var first = await UploadAndReadDataAsync("course", PngBytes, "image/png", "course.png");
        var second = await UploadAndReadDataAsync("course", changedPng, "image/png", "course.png");
        var files = Directory.GetFiles(Path.Combine(UploadsRoot, "classroom"));

        Assert.NotEqual(first.GetProperty("url").GetString(), second.GetProperty("url").GetString());
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public async Task ClassroomUpload_RejectsInvalidFiles()
    {
        await AuthAsync();
        var tooLarge = new byte[MaxImageSizeBytes + 1];
        Array.Copy(PngBytes, tooLarge, PngBytes.Length);

        var notImage = await UploadImageAsync("course", [1, 2, 3], "image/png", "bad.png");
        var wrongMime = await UploadImageAsync("course", PngBytes, "image/jpeg", "bad.png");
        var large = await UploadImageAsync("course", tooLarge, "image/png", "large.png");

        await AssertErrorAsync(notImage, HttpStatusCode.BadRequest, ErrorCodes.FileTypeNotAllowed);
        await AssertErrorAsync(wrongMime, HttpStatusCode.BadRequest, ErrorCodes.FileTypeNotAllowed);
        await AssertErrorAsync(large, HttpStatusCode.BadRequest, ErrorCodes.FileTooLarge);
    }

    [Fact]
    public async Task ClassroomVideoUpload_RequiresAdminSession()
    {
        var response = await UploadVideoAsync("course", Mp4Bytes, "video/mp4", "course.mp4");
        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task ClassroomVideoUpload_RejectsLegacyKey()
    {
        UseLegacyKey();

        var response = await UploadVideoAsync("course", Mp4Bytes, "video/mp4", "course.mp4");
        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task ClassroomVideoUpload_DeduplicatesRepeatedMp4()
    {
        await AuthAsync();

        var first = await UploadVideoAndReadDataAsync("course", Mp4Bytes, "video/mp4", "course.mp4");
        var second = await UploadVideoAndReadDataAsync("course", Mp4Bytes, "video/mp4", "course.mp4");
        var files = Directory.GetFiles(Path.Combine(UploadsRoot, "classroom-videos"));

        Assert.Equal(first.GetProperty("url").GetString(), second.GetProperty("url").GetString());
        Assert.Contains("/uploads/classroom-videos/", first.GetProperty("url").GetString());
        Assert.EndsWith(".mp4", first.GetProperty("url").GetString());
        Assert.Single(files);
    }

    [Fact]
    public async Task ClassroomVideoUpload_AllowsMp4OverDefaultRequestLimit()
    {
        await AuthAsync();
        var bytes = CreateLargeMp4Bytes(OverDefaultRequestLimitVideoSizeBytes);

        var video = await UploadVideoAndReadDataAsync("course", bytes, "video/mp4", "large-ok.mp4");
        var files = Directory.GetFiles(Path.Combine(UploadsRoot, "classroom-videos"));

        Assert.Contains("/uploads/classroom-videos/", video.GetProperty("url").GetString());
        Assert.Single(files);
    }

    [Fact]
    public async Task ClassroomVideoUpload_RejectsInvalidFiles()
    {
        await AuthAsync();

        var badScene = await UploadVideoAsync("quick-action", Mp4Bytes, "video/mp4", "course.mp4");
        var wrongMime = await UploadVideoAsync("course", Mp4Bytes, "video/quicktime", "course.mp4");
        var notMp4 = await UploadVideoAsync("course", [1, 2, 3], "video/mp4", "bad.mp4");
        var large = await UploadLargeVideoAsync();

        await AssertErrorAsync(badScene, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
        await AssertErrorAsync(wrongMime, HttpStatusCode.BadRequest, ErrorCodes.FileTypeNotAllowed);
        await AssertErrorAsync(notMp4, HttpStatusCode.BadRequest, ErrorCodes.FileTypeNotAllowed);
        await AssertErrorAsync(large, HttpStatusCode.BadRequest, ErrorCodes.FileTooLarge);
    }

    private async Task<JsonElement> UploadAndReadDataAsync(
        string scene,
        byte[] bytes,
        string contentType,
        string fileName)
    {
        var response = await UploadImageAsync(scene, bytes, contentType, fileName);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync<ApiResponse<JsonElement>>(response);
        return body.Data;
    }

    private async Task<JsonElement> UploadVideoAndReadDataAsync(
        string scene,
        byte[] bytes,
        string contentType,
        string fileName)
    {
        var response = await UploadVideoAsync(scene, bytes, contentType, fileName);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync<ApiResponse<JsonElement>>(response);
        return body.Data;
    }

    private async Task<HttpResponseMessage> UploadImageAsync(
        string scene,
        byte[] bytes,
        string contentType,
        string fileName)
    {
        using var content = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        content.Add(new StringContent(scene), "scene");
        return await Client.PostAsync("/api/upload/image", content);
    }

    private async Task<HttpResponseMessage> UploadVideoAsync(
        string scene,
        byte[] bytes,
        string contentType,
        string fileName)
    {
        using var content = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        content.Add(new StringContent(scene), "scene");
        return await Client.PostAsync("/api/upload/video", content);
    }

    private async Task<HttpResponseMessage> UploadLargeVideoAsync()
    {
        var bytes = CreateLargeMp4Bytes(MaxVideoSizeBytes + 1);
        using var content = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(file, "file", "large.mp4");
        content.Add(new StringContent("course"), "scene");
        return await Client.PostAsync("/api/upload/video", content);
    }

    private static byte[] CreateLargeMp4Bytes(int size)
    {
        var bytes = new byte[size];
        Array.Copy(Mp4Bytes, bytes, Mp4Bytes.Length);
        return bytes;
    }
}
