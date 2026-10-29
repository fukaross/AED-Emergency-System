using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Persistence;

namespace AedBackend.Tests.Admin;

public sealed class AdminUserIdentityApiTests
{
    private const string UserToken = "identity-admin-user-token";
    private const string OpenId = "openid-admin-identity";

    [Fact]
    public async Task SchoolProfiles_CreateUpdateListAndRejectInvalidPersonType()
    {
        using var fixture = await Fixture.CreateAsync();
        fixture.UseAdminKey();

        var created = await fixture.Client.PostAsJsonAsync("/api/admin/school-profiles", SchoolProfile("20260101"));
        var createdData = (await ReadAsync<ApiResponse<JsonElement>>(created)).Data;
        var profileId = createdData.GetProperty("profileId").GetString();
        var invalid = await fixture.Client.PostAsJsonAsync("/api/admin/school-profiles", SchoolProfile("20260102", "all"));
        var updated = await fixture.Client.PutAsJsonAsync($"/api/admin/school-profiles/{profileId}", SchoolProfile("20260101", college: "信息学院"));
        var list = await fixture.Client.GetAsync("/api/admin/school-profiles?personType=student&keyword=信息学院");
        var page = (await ReadAsync<ApiResponse<JsonElement>>(list)).Data;

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("信息学院", page.GetProperty("items")[0].GetProperty("college").GetString());
        await AssertErrorAsync(invalid, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task SchoolBindingReview_ApprovesWithNewProfileAndAggregatesProfile()
    {
        using var fixture = await Fixture.CreateAsync();
        var applicationId = await fixture.SubmitSchoolBindingAsync("20260101");
        fixture.UseAdminKey();

        var approved = await fixture.Client.PutAsJsonAsync(
            $"/api/admin/school-bindings/{applicationId}/approve",
            new { newProfile = SchoolProfile("20260101"), reviewNote = "核验通过" });
        var data = (await ReadAsync<ApiResponse<JsonElement>>(approved)).Data;
        var repeated = await fixture.Client.PutAsJsonAsync(
            $"/api/admin/school-bindings/{applicationId}/reject",
            new { reviewNote = "重复处理" });
        fixture.UseUserToken();
        var profile = await fixture.Client.GetAsync("/api/user/profile");
        var profileData = (await ReadAsync<ApiResponse<JsonElement>>(profile)).Data;

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("approved", data.GetProperty("application").GetProperty("status").GetString());
        Assert.True(data.GetProperty("application").GetProperty("schoolProfileId").GetInt32() > 0);
        Assert.Equal("教育学院", profileData.GetProperty("schoolProfile").GetProperty("college").GetString());
        await AssertErrorAsync(repeated, HttpStatusCode.Conflict, ErrorCodes.SchoolBindingAlreadyProcessed);
    }

    [Fact]
    public async Task SchoolBindingReview_RequiresProfileOrNewProfile()
    {
        using var fixture = await Fixture.CreateAsync();
        var applicationId = await fixture.SubmitSchoolBindingAsync("20260101");
        fixture.UseAdminKey();

        var approved = await fixture.Client.PutAsJsonAsync(
            $"/api/admin/school-bindings/{applicationId}/approve",
            new { reviewNote = "缺少档案" });

        await AssertErrorAsync(approved, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task SchoolProfileDelete_UnlinksApprovedBindingAndAllowsReapply()
    {
        using var fixture = await Fixture.CreateAsync();
        var applicationId = await fixture.SubmitSchoolBindingAsync("20260101");
        fixture.UseAdminKey();

        var approved = await fixture.Client.PutAsJsonAsync(
            $"/api/admin/school-bindings/{applicationId}/approve",
            new { newProfile = SchoolProfile("20260101"), reviewNote = "核验通过" });
        var approvedData = (await ReadAsync<ApiResponse<JsonElement>>(approved)).Data;
        var profileId = approvedData.GetProperty("application").GetProperty("schoolProfileId").GetInt32();
        var normalAdminKey = await fixture.CreateAndLoginAdminAsync("operator");

        fixture.UseAdminKey(normalAdminKey);
        var deleted = await fixture.Client.DeleteAsync($"/api/admin/school-profiles/{profileId}");
        var binding = await fixture.Client.GetAsync($"/api/admin/school-bindings/{applicationId}");
        var bindingData = (await ReadAsync<ApiResponse<JsonElement>>(binding)).Data;
        fixture.UseUserToken();
        var profile = await fixture.Client.GetAsync("/api/user/profile");
        var profileData = (await ReadAsync<ApiResponse<JsonElement>>(profile)).Data;
        var reapply = await fixture.Client.PostAsJsonAsync("/api/user/school-bindings", SchoolBindingPayload("20260102"));

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, binding.StatusCode);
        Assert.True(bindingData.GetProperty("schoolProfileId").ValueKind == JsonValueKind.Null);
        Assert.True(profileData.GetProperty("schoolProfile").ValueKind == JsonValueKind.Null);
        Assert.Equal(HttpStatusCode.OK, reapply.StatusCode);
    }

    [Fact]
    public async Task SchoolBindingDelete_RequiresSuperAdmin()
    {
        using var fixture = await Fixture.CreateAsync();
        var normalAdminKey = await fixture.CreateAndLoginAdminAsync("operator");
        var applicationId = await fixture.SubmitSchoolBindingAsync("20260101");

        fixture.UseAdminKey(normalAdminKey);
        var forbidden = await fixture.Client.DeleteAsync($"/api/admin/school-bindings/{applicationId}");
        fixture.UseAdminKey();
        var deleted = await fixture.Client.DeleteAsync($"/api/admin/school-bindings/{applicationId}");
        var missing = await fixture.Client.GetAsync($"/api/admin/school-bindings/{applicationId}");

        await AssertErrorAsync(forbidden, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        await AssertErrorAsync(missing, HttpStatusCode.NotFound, ErrorCodes.SchoolBindingNotFound);
    }

    [Fact]
    public async Task ResponderClaimReview_ApprovesUnboundResponderAndAggregatesSummary()
    {
        using var fixture = await Fixture.CreateAsync();
        var applicationId = await fixture.SubmitResponderClaimAsync();
        fixture.UseAdminKey();

        var approved = await fixture.Client.PutAsJsonAsync(
            $"/api/admin/responder-claims/{applicationId}/approve",
            new { responderProfileId = "rescuer-001", reviewNote = "核验通过" });
        var data = (await ReadAsync<ApiResponse<JsonElement>>(approved)).Data;
        var repeated = await fixture.Client.PutAsJsonAsync(
            $"/api/admin/responder-claims/{applicationId}/reject",
            new { reviewNote = "重复处理" });
        fixture.UseUserToken();
        var profile = await fixture.Client.GetAsync("/api/user/profile");
        var profileData = (await ReadAsync<ApiResponse<JsonElement>>(profile)).Data;

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("approved", data.GetProperty("application").GetProperty("status").GetString());
        Assert.Equal("rescuer-001", data.GetProperty("application").GetProperty("responderProfileId").GetString());
        Assert.Equal("rescuer-001", profileData.GetProperty("rescuerSummary").GetProperty("responderProfileId").GetString());
        Assert.True(profileData.GetProperty("certified").GetBoolean());
        await AssertErrorAsync(repeated, HttpStatusCode.Conflict, ErrorCodes.ResponderClaimAlreadyProcessed);
    }

    [Fact]
    public async Task ResponderClaimReview_RejectsAlreadyBoundResponder()
    {
        using var fixture = await Fixture.CreateAsync(boundResponder: true);
        var applicationId = await fixture.SubmitResponderClaimAsync();
        fixture.UseAdminKey();

        var approved = await fixture.Client.PutAsJsonAsync(
            $"/api/admin/responder-claims/{applicationId}/approve",
            new { responderProfileId = "rescuer-001", reviewNote = "核验通过" });

        await AssertErrorAsync(approved, HttpStatusCode.Conflict, ErrorCodes.ResponderAlreadyBound);
    }

    [Fact]
    public async Task AdminIdentityEndpoints_RequireSqliteProvider()
    {
        using var fixture = await Fixture.CreateJsonAsync();
        fixture.UseAdminKey();

        var profiles = await fixture.Client.GetAsync("/api/admin/school-profiles");
        var bindings = await fixture.Client.GetAsync("/api/admin/school-bindings");
        var claims = await fixture.Client.GetAsync("/api/admin/responder-claims");
        var profileDelete = await fixture.Client.DeleteAsync("/api/admin/school-profiles/1");
        var bindingDelete = await fixture.Client.DeleteAsync("/api/admin/school-bindings/1");

        await AssertErrorAsync(profiles, HttpStatusCode.NotImplemented, ErrorCodes.FeatureRequiresSqlite);
        await AssertErrorAsync(bindings, HttpStatusCode.NotImplemented, ErrorCodes.FeatureRequiresSqlite);
        await AssertErrorAsync(claims, HttpStatusCode.NotImplemented, ErrorCodes.FeatureRequiresSqlite);
        await AssertErrorAsync(profileDelete, HttpStatusCode.NotImplemented, ErrorCodes.FeatureRequiresSqlite);
        await AssertErrorAsync(bindingDelete, HttpStatusCode.NotImplemented, ErrorCodes.FeatureRequiresSqlite);
    }

    private static object SchoolProfile(
        string studentId,
        string personType = "student",
        string college = "教育学院")
    {
        return new
        {
            personType,
            name = "王小明",
            college,
            className = "急救 1 班",
            studentId,
            phone = "REDACTED",
            status = "active"
        };
    }

    private static object SchoolBindingPayload(string studentId)
    {
        return new
        {
            personType = "student",
            name = "王小明",
            college = "教育学院",
            className = "急救 1 班",
            studentId,
            phone = "REDACTED",
            note = "绑定校园身份"
        };
    }

    private static object ClaimPayload()
    {
        return new
        {
            name = "张三",
            idNumberLast6 = "100011",
            phone = "REDACTED",
            certificateNo = "CERT20260001",
            note = "认领已有急救员档案"
        };
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
        private readonly ProviderTempData temp;
        private readonly AdminTestApplication app;

        private Fixture(ProviderTempData temp, AdminTestApplication app)
        {
            this.temp = temp;
            this.app = app;
            Client = app.CreateClient();
        }

        public HttpClient Client { get; }

        public static async Task<Fixture> CreateAsync(bool boundResponder = false)
        {
            var temp = new ProviderTempData();
            await temp.WriteValidDataAsync();
            await temp.WriteUserJsonAsync(OpenId, "REDACTED", UserToken);
            if (boundResponder) await temp.WriteBoundResponderJsonAsync(OpenId);
            await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
            return new Fixture(temp, new AdminTestApplication(temp.Root, SqliteConfig(temp)));
        }

        public static async Task<Fixture> CreateJsonAsync()
        {
            var temp = new ProviderTempData();
            await temp.WriteValidDataAsync();
            return new Fixture(temp, new AdminTestApplication(temp.Root));
        }

        public async Task<int> SubmitSchoolBindingAsync(string studentId)
        {
            UseUserToken();
            var response = await Client.PostAsJsonAsync("/api/user/school-bindings", SchoolBindingPayload(studentId));
            var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
            return int.Parse(data.GetProperty("applicationId").GetString()!);
        }

        public async Task<int> SubmitResponderClaimAsync()
        {
            UseUserToken();
            var response = await Client.PostAsJsonAsync("/api/user/responder-claims", ClaimPayload());
            var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
            return int.Parse(data.GetProperty("applicationId").GetString()!);
        }

        public async Task<string> CreateAndLoginAdminAsync(string username)
        {
            UseAdminKey();
            var created = await Client.PostAsJsonAsync(
                "/api/admin/admins",
                new { username, password = "Password123!" });
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);

            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
            var login = await Client.PostAsJsonAsync(
                "/api/admin/auth/login",
                new { username, password = "Password123!", deviceId = $"test-device-{username}-0001" });
            var body = await ReadAsync<ApiResponse<JsonElement>>(login);
            return body.Data.GetProperty("adminKey").GetString()!;
        }

        public void UseAdminKey(string key = ProviderTempData.AdminSessionKey)
        {
            Client.DefaultRequestHeaders.Authorization = null;
            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
            Client.DefaultRequestHeaders.Add("X-Aed-Admin-Key", key);
        }

        public void UseUserToken()
        {
            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", UserToken);
        }

        public void Dispose()
        {
            Client.Dispose();
            app.Dispose();
            temp.Dispose();
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
