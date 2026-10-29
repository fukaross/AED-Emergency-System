using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Persistence;

namespace AedBackend.Tests.Admin;

public sealed class AdminVolunteerApplicationApiTests
{
    private const string UserToken = "user-test-token";
    private const string OpenId = "openid-volunteer";

    [Fact]
    public async Task ListApplications_RequiresAdminSessionAndRejectsLegacyKey()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.SubmitApplicationAsync("第一条");

        var unauthorized = await fixture.Client.GetAsync("/api/admin/volunteer-applications");
        await AssertErrorAsync(unauthorized, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);

        fixture.UseLegacyKey();
        var legacy = await fixture.Client.GetAsync("/api/admin/volunteer-applications");
        await AssertErrorAsync(legacy, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);

        fixture.UseAdminKey();
        var response = await fixture.Client.GetAsync("/api/admin/volunteer-applications");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, data.GetProperty("total").GetInt32());
        Assert.Equal("王小明", data.GetProperty("items")[0].GetProperty("applicantName").GetString());
        Assert.Contains("****", data.GetProperty("items")[0].GetProperty("phoneMasked").GetString());
    }

    [Fact]
    public async Task ListApplications_FiltersPaginatesAndOrdersByCreatedAt()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.SubmitApplicationAsync("旧申请");
        await Task.Delay(10);
        var newestId = await fixture.SubmitApplicationAsync("新申请");
        fixture.UseAdminKey();

        await fixture.Client.PutAsJsonAsync(
            $"/api/admin/volunteer-applications/{newestId}/reject",
            new { reviewNote = "材料不完整" });
        var response = await fixture.Client.GetAsync(
            "/api/admin/volunteer-applications?status=rejected&keyword=新申请&page=1&pageSize=1");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, data.GetProperty("total").GetInt32());
        Assert.Equal(newestId.ToString(), data.GetProperty("items")[0].GetProperty("applicationId").GetString());
        Assert.Equal("rejected", data.GetProperty("items")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetApplication_ReturnsDetailAndMissingReturnsNotFound()
    {
        using var fixture = await Fixture.CreateAsync();
        var id = await fixture.SubmitApplicationAsync("详情申请");
        fixture.UseAdminKey();

        var response = await fixture.Client.GetAsync($"/api/admin/volunteer-applications/{id}");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
        var missing = await fixture.Client.GetAsync("/api/admin/volunteer-applications/999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(id.ToString(), data.GetProperty("applicationId").GetString());
        Assert.Equal("详情申请", data.GetProperty("payload").GetProperty("joinReason").GetString());
        await AssertErrorAsync(missing, HttpStatusCode.NotFound, ErrorCodes.VolunteerApplicationNotFound);
    }

    [Fact]
    public async Task ApproveApplication_CreatesResponderDraftAndWritesAudit()
    {
        using var fixture = await Fixture.CreateAsync();
        var id = await fixture.SubmitApplicationAsync("通过申请");
        await fixture.UpdateUserProfileNameAsync("志愿者王小明");
        fixture.UseAdminKey();

        var response = await fixture.Client.PutAsJsonAsync(
            $"/api/admin/volunteer-applications/{id}/approve",
            new { campus = "guilinyang", lat = REDACTED, lng = REDACTED });
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
        var rescuerId = data.GetProperty("createdRescuerId").GetString();
        var detail = await fixture.Client.GetAsync($"/api/admin/rescuers/{rescuerId}");
        var rescuer = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("approved", data.GetProperty("application").GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(rescuerId));
        Assert.Equal("王小明", rescuer.GetProperty("name").GetString());
        Assert.Equal("inactive", rescuer.GetProperty("qualificationStatus").GetString());
        Assert.Equal("trainee", rescuer.GetProperty("level").GetString());
        Assert.Equal(OpenId, rescuer.GetProperty("boundOpenId").GetString());
        Assert.Equal("志愿者王小明", rescuer.GetProperty("boundUserName").GetString());
        Assert.Equal("volunteer_application", rescuer.GetProperty("source").GetString());
        Assert.Equal(id, rescuer.GetProperty("sourceApplicationId").GetInt32());

        var updated = await fixture.Client.PutAsJsonAsync($"/api/admin/rescuers/{rescuerId}", new { level = "official" });
        var updatedRescuer = (await ReadAsync<ApiResponse<JsonElement>>(updated)).Data;
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(OpenId, updatedRescuer.GetProperty("boundOpenId").GetString());
        Assert.Equal("志愿者王小明", updatedRescuer.GetProperty("boundUserName").GetString());
        Assert.Equal("volunteer_application", updatedRescuer.GetProperty("source").GetString());
        Assert.Equal(id, updatedRescuer.GetProperty("sourceApplicationId").GetInt32());

        var logs = await fixture.Client.GetAsync(
            $"/api/admin/audit-logs?eventName=volunteer_application.approved&keyword={id}");
        var logData = (await ReadAsync<ApiResponse<JsonElement>>(logs)).Data;
        var logText = logData.ToString();
        Assert.Equal(1, logData.GetProperty("total").GetInt32());
        Assert.DoesNotContain("REDACTED", logText);
        Assert.DoesNotContain("REDACTED", logText);
    }

    [Fact]
    public async Task RejectApplication_RequiresNoteAndBlocksRepeatedReview()
    {
        using var fixture = await Fixture.CreateAsync();
        var id = await fixture.SubmitApplicationAsync("驳回申请");
        fixture.UseAdminKey();

        var missingNote = await fixture.Client.PutAsJsonAsync(
            $"/api/admin/volunteer-applications/{id}/reject",
            new { reviewNote = "" });
        await AssertErrorAsync(missingNote, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var rejected = await fixture.Client.PutAsJsonAsync(
            $"/api/admin/volunteer-applications/{id}/reject",
            new { reviewNote = "材料不完整" });
        var repeated = await fixture.Client.PutAsJsonAsync(
            $"/api/admin/volunteer-applications/{id}/approve",
            new { campus = "guilinyang", lat = REDACTED, lng = REDACTED });
        var data = (await ReadAsync<ApiResponse<JsonElement>>(rejected)).Data;

        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("rejected", data.GetProperty("application").GetProperty("status").GetString());
        Assert.Equal("材料不完整", data.GetProperty("application").GetProperty("reviewNote").GetString());
        await AssertErrorAsync(repeated, HttpStatusCode.Conflict, ErrorCodes.VolunteerApplicationAlreadyProcessed);
    }

    [Fact]
    public async Task ApproveApplication_RollsBackWhenResponderCreationFails()
    {
        using var fixture = await Fixture.CreateWithForcedResponderIdAsync("rescuer-001");
        var id = await fixture.SubmitApplicationAsync("回滚申请");
        fixture.UseAdminKey();

        var response = await fixture.Client.PutAsJsonAsync(
            $"/api/admin/volunteer-applications/{id}/approve",
            new { campus = "guilinyang", lat = REDACTED, lng = REDACTED });
        var detail = await fixture.Client.GetAsync($"/api/admin/volunteer-applications/{id}");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(detail)).Data;

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("submitted", data.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("createdRescuerId").ValueKind);
    }

    [Fact]
    public async Task AdminVolunteerEndpoints_RequireSqliteProvider()
    {
        using var fixture = await Fixture.CreateJsonAsync();
        fixture.UseAdminKey();

        var response = await fixture.Client.GetAsync("/api/admin/volunteer-applications");

        await AssertErrorAsync(response, HttpStatusCode.NotImplemented, ErrorCodes.FeatureRequiresSqlite);
    }

    private static object CreatePayload(string joinReason)
    {
        return new
        {
            idPhoto = Attachment("/uploads/volunteer/id-photo.jpg"),
            name = "王小明",
            gender = "male",
            birthDate = "2000-01-01",
            phone = "REDACTED",
            email = "user@example.com",
            politicalStatus = "群众",
            education = "本科",
            occupation = "学生",
            professionalTitle = "无",
            emergencyContactName = "李老师",
            emergencyContactPhone = "REDACTED",
            qualificationImages = new[] { Attachment("/uploads/volunteer/qualification.jpg") },
            volunteerCertificateImages = new[] { Attachment("/uploads/volunteer/cert.jpg") },
            idNumber = "REDACTED",
            idCardFront = Attachment("/uploads/volunteer/id-front.jpg"),
            idCardBack = Attachment("/uploads/volunteer/id-back.jpg"),
            homeAddress = "海南省海口市",
            personalResume = "参加过校园急救培训",
            joinReason,
            commitmentAccepted = true
        };
    }

    private static object Attachment(string url)
    {
        return new { url, fileId = (string?)null, name = "image.jpg" };
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var result = await response.Content.ReadFromJsonAsync<T>();
        return result ?? throw new InvalidOperationException("Response body was empty.");
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

    private sealed class Fixture : IDisposable
    {
        private readonly AdminTestApplication app;

        private Fixture(ProviderTempData temp, AdminTestApplication app)
        {
            Temp = temp;
            this.app = app;
            Client = app.CreateClient();
        }

        public ProviderTempData Temp { get; }
        public HttpClient Client { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var temp = new ProviderTempData();
            await temp.WriteValidDataAsync();
            await temp.WriteUserJsonAsync(OpenId, "REDACTED", UserToken);
            await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
            var app = new AdminTestApplication(temp.Root, SqliteConfig(temp));
            var fixture = new Fixture(temp, app);
            fixture.UseAdminKey(ProviderTempData.AdminSessionKey);
            return fixture;
        }

        public static async Task<Fixture> CreateWithForcedResponderIdAsync(string responderId)
        {
            var temp = new ProviderTempData();
            await temp.WriteValidDataAsync();
            await temp.WriteUserJsonAsync(OpenId, "REDACTED", UserToken);
            await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
            var config = SqliteConfig(temp);
            config["Test:ForcedResponderId"] = responderId;
            var fixture = new Fixture(temp, new AdminTestApplication(temp.Root, config));
            fixture.UseAdminKey(ProviderTempData.AdminSessionKey);
            return fixture;
        }

        public static async Task<Fixture> CreateJsonAsync()
        {
            var temp = new ProviderTempData();
            await temp.WriteValidDataAsync();
            var fixture = new Fixture(temp, new AdminTestApplication(temp.Root));
            fixture.UseAdminKey(ProviderTempData.AdminSessionKey);
            return fixture;
        }

        public async Task<int> SubmitApplicationAsync(string joinReason)
        {
            UseUserToken();
            var response = await Client.PostAsJsonAsync(
                "/api/user/volunteer-applications",
                CreatePayload(joinReason));
            var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
            return int.Parse(data.GetProperty("applicationId").GetString()!);
        }

        public async Task UpdateUserProfileNameAsync(string name)
        {
            UseUserToken();
            var response = await Client.PutAsJsonAsync("/api/user/profile", new { name });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        public void UseAdminKey(string key = ProviderTempData.AdminSessionKey)
        {
            Client.DefaultRequestHeaders.Authorization = null;
            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
            Client.DefaultRequestHeaders.Add("X-Aed-Admin-Key", key);
        }

        public void UseLegacyKey()
        {
            UseAdminKey(AdminApiTestBase.LegacyAdminKey);
        }

        private void UseUserToken()
        {
            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", UserToken);
        }

        public void Dispose()
        {
            Client.Dispose();
            app.Dispose();
            Temp.Dispose();
        }

        private static Dictionary<string, string?> SqliteConfig(ProviderTempData temp)
        {
            return new Dictionary<string, string?>
            {
                ["Data:Provider"] = "Sqlite",
                ["Data:SqlitePath"] = temp.DbPath
            };
        }
    }
}
