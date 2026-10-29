using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Admin;
using AedBackend.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AedBackend.Tests.User;

public sealed class UserApiTests
{
    private const string UserToken = "user-test-token";
    private const string OpenId = "openid-abc123";

    [Fact]
    public async Task Profile_RequiresLoginAndReturnsDefaults()
    {
        using var fixture = await UserApiFixture.CreateAsync();
        var unauthorized = await fixture.Client.GetAsync("/api/user/profile");
        await AssertErrorAsync(unauthorized, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);

        fixture.UseUserToken();
        var response = await fixture.Client.GetAsync("/api/user/profile");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OpenId, data.GetProperty("id").GetString());
        Assert.Equal("微信用户 abc123", data.GetProperty("name").GetString());
        Assert.Equal(string.Empty, data.GetProperty("avatarUrl").GetString());
        Assert.Equal("unknown", data.GetProperty("gender").GetString());
        Assert.Equal("13800138000", data.GetProperty("phone").GetString());
        Assert.Equal("student", data.GetProperty("primaryRole").GetString());
        Assert.False(data.GetProperty("certified").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("schoolProfile").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("rescuerSummary").ValueKind);
    }

    [Fact]
    public async Task Profile_ReturnsAdminPrimaryRoleAndBlocksDisabledAccount()
    {
        using var fixture = await UserApiFixture.CreateAsync();
        fixture.UseAdminKey();
        var update = await fixture.Client.PutAsJsonAsync(
            $"/api/admin/users/{OpenId}",
            new { primaryRole = "teacher" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        fixture.UseUserToken();
        var profile = await fixture.Client.GetAsync("/api/user/profile");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(profile)).Data;
        Assert.Equal("teacher", data.GetProperty("primaryRole").GetString());

        fixture.UseAdminKey();
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PutAsJsonAsync(
            $"/api/admin/users/{OpenId}",
            new { accountStatus = "disabled" })).StatusCode);

        fixture.UseUserToken();
        await AssertErrorAsync(
            await fixture.Client.GetAsync("/api/user/profile"),
            HttpStatusCode.Forbidden,
            ErrorCodes.AccountDisabled);
        await AssertErrorAsync(
            await fixture.Client.PostAsync("/api/auth/refresh", null),
            HttpStatusCode.Forbidden,
            ErrorCodes.AccountDisabled);
        await AssertErrorAsync(
            await fixture.Client.PostAsJsonAsync(
                "/api/auth/wechat-phone",
                new { phone = "13900139000" }),
            HttpStatusCode.Forbidden,
            ErrorCodes.AccountDisabled);
        await AssertErrorAsync(
            await fixture.Client.PostAsJsonAsync(
                "/api/wechat/subscriptions",
                new { templateId = "disabled-template", status = "accept" }),
            HttpStatusCode.Forbidden,
            ErrorCodes.AccountDisabled);

        fixture.UseAdminKey();
        var disabledDetail = await fixture.Client.GetAsync($"/api/admin/users/{OpenId}");
        var disabledData = (await ReadAsync<ApiResponse<JsonElement>>(disabledDetail)).Data;
        Assert.False(disabledData.GetProperty("subscriptions").TryGetProperty(
            "disabled-template",
            out _));

        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PutAsJsonAsync(
            $"/api/admin/users/{OpenId}",
            new { accountStatus = "active" })).StatusCode);

        fixture.UseUserToken();
        var restored = await fixture.Client.GetAsync("/api/user/profile");
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    [Fact]
    public async Task WeChatLogin_BlocksDisabledAccount()
    {
        using var fixture = await UserApiFixture.CreateAsync(withFakeLogin: true);
        fixture.UseAdminKey();
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PutAsJsonAsync(
            $"/api/admin/users/{OpenId}",
            new { accountStatus = "disabled" })).StatusCode);

        fixture.ClearHeaders();
        var disabled = await fixture.Client.PostAsJsonAsync(
            "/api/auth/wechat-login",
            new { code = "login-code" });
        await AssertErrorAsync(disabled, HttpStatusCode.Forbidden, ErrorCodes.AccountDisabled);

        fixture.UseAdminKey();
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PutAsJsonAsync(
            $"/api/admin/users/{OpenId}",
            new { accountStatus = "active" })).StatusCode);

        fixture.ClearHeaders();
        var restored = await fixture.Client.PostAsJsonAsync(
            "/api/auth/wechat-login",
            new { code = "login-code" });
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    [Fact]
    public async Task Subscription_AuthenticatesBeforePayloadValidation()
    {
        using var fixture = await UserApiFixture.CreateAsync();
        fixture.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/wechat/subscriptions",
            new { templateId = string.Empty, status = "invalid" });

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task Profile_UpdateReturnsPersistedFields()
    {
        using var fixture = await UserApiFixture.CreateAsync();
        fixture.UseUserToken();

        var update = await fixture.Client.PutAsJsonAsync("/api/user/profile", new
        {
            name = "  王小明  ",
            avatarUrl = "/uploads/avatar/a.jpg",
            gender = "male",
            phone = "13900139000"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var response = await fixture.Client.GetAsync("/api/user/profile");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
        Assert.Equal("王小明", data.GetProperty("name").GetString());
        Assert.Equal("/uploads/avatar/a.jpg", data.GetProperty("avatarUrl").GetString());
        Assert.Equal("male", data.GetProperty("gender").GetString());
        Assert.Equal("13900139000", data.GetProperty("phone").GetString());
    }

    [Fact]
    public async Task Profile_UpdateRejectsInvalidFields()
    {
        using var fixture = await UserApiFixture.CreateAsync();
        fixture.UseUserToken();

        var badGender = await fixture.Client.PutAsJsonAsync("/api/user/profile", new { gender = "secret" });
        var badPhone = await fixture.Client.PutAsJsonAsync("/api/user/profile", new { phone = "110" });
        var badName = await fixture.Client.PutAsJsonAsync("/api/user/profile", new { name = new string('a', 41) });
        var badAvatar = await fixture.Client.PutAsJsonAsync("/api/user/profile", new { avatarUrl = "wxfile://tmp/a.jpg" });

        await AssertErrorAsync(badGender, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
        await AssertErrorAsync(badPhone, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
        await AssertErrorAsync(badName, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
        await AssertErrorAsync(badAvatar, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task VolunteerApplication_SubmitsAndListsCurrentUserItems()
    {
        using var fixture = await UserApiFixture.CreateAsync();
        fixture.UseUserToken();

        var first = await fixture.Client.PostAsJsonAsync(
            "/api/user/volunteer-applications",
            CreatePayload("第一轮申请"));
        var firstData = (await ReadAsync<ApiResponse<JsonElement>>(first)).Data;
        await Task.Delay(10);
        var second = await fixture.Client.PostAsJsonAsync(
            "/api/user/volunteer-applications",
            CreatePayload("第二轮申请", firstData.GetProperty("applicationId").GetString()));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var list = await fixture.Client.GetAsync("/api/user/volunteer-applications");
        var page = (await ReadAsync<ApiResponse<JsonElement>>(list)).Data;
        var items = page.GetProperty("items");

        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(2, page.GetProperty("total").GetInt32());
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(20, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, page.GetProperty("totalPages").GetInt32());
        Assert.Equal("submitted", firstData.GetProperty("status").GetString());
        Assert.Equal("第二轮申请", items[0].GetProperty("payload").GetProperty("joinReason").GetString());
        Assert.Equal(2, items[0].GetProperty("attemptNumber").GetInt32());
        Assert.Equal(1, items[1].GetProperty("attemptNumber").GetInt32());
    }

    [Fact]
    public async Task VolunteerApplication_ListPaginatesCurrentUserItems()
    {
        using var fixture = await UserApiFixture.CreateAsync();
        fixture.UseUserToken();

        await fixture.Client.PostAsJsonAsync(
            "/api/user/volunteer-applications",
            CreatePayload("第一页第二条"));
        await Task.Delay(10);
        await fixture.Client.PostAsJsonAsync(
            "/api/user/volunteer-applications",
            CreatePayload("第一页第一条"));

        var list = await fixture.Client.GetAsync("/api/user/volunteer-applications?page=2&pageSize=1");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(list)).Data;

        Assert.Equal(2, data.GetProperty("total").GetInt32());
        Assert.Equal(2, data.GetProperty("page").GetInt32());
        Assert.Equal(1, data.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, data.GetProperty("totalPages").GetInt32());
        Assert.Equal("第一页第二条", data.GetProperty("items")[0]
            .GetProperty("payload").GetProperty("joinReason").GetString());
    }

    [Fact]
    public async Task VolunteerApplication_ListRejectsInvalidPaging()
    {
        using var fixture = await UserApiFixture.CreateAsync();
        fixture.UseUserToken();

        var badPage = await fixture.Client.GetAsync("/api/user/volunteer-applications?page=0");
        var badPageSize = await fixture.Client.GetAsync("/api/user/volunteer-applications?pageSize=101");

        await AssertErrorAsync(badPage, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
        await AssertErrorAsync(badPageSize, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task VolunteerApplication_RejectsMissingFieldsAndLocalImages()
    {
        using var fixture = await UserApiFixture.CreateAsync();
        fixture.UseUserToken();

        var missingName = CreatePayload("申请", name: string.Empty);
        var localImage = CreatePayload("申请", idPhotoUrl: "wxfile://tmp/a.jpg");

        await AssertErrorAsync(
            await fixture.Client.PostAsJsonAsync("/api/user/volunteer-applications", missingName),
            HttpStatusCode.BadRequest,
            ErrorCodes.ParameterInvalid);
        await AssertErrorAsync(
            await fixture.Client.PostAsJsonAsync("/api/user/volunteer-applications", localImage),
            HttpStatusCode.BadRequest,
            ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task UserEndpoints_RequireSqliteProvider()
    {
        using var fixture = await UserApiFixture.CreateJsonAsync();
        fixture.UseUserToken();

        var profile = await fixture.Client.GetAsync("/api/user/profile");
        var applications = await fixture.Client.GetAsync("/api/user/volunteer-applications");

        await AssertErrorAsync(profile, HttpStatusCode.NotImplemented, ErrorCodes.FeatureRequiresSqlite);
        await AssertErrorAsync(applications, HttpStatusCode.NotImplemented, ErrorCodes.FeatureRequiresSqlite);
    }

    private static object CreatePayload(
        string joinReason,
        string? derivedFromId = null,
        string name = "王小明",
        string idPhotoUrl = "/uploads/volunteer/id-photo.jpg")
    {
        var derivedFromValue = string.IsNullOrWhiteSpace(derivedFromId)
            ? (int?)null
            : int.Parse(derivedFromId);
        return new
        {
            idPhoto = Attachment(idPhotoUrl),
            name,
            gender = "male",
            birthDate = "2000-01-01",
            phone = "13800138000",
            email = "user@example.com",
            politicalStatus = "群众",
            education = "本科",
            occupation = "学生",
            professionalTitle = "无",
            emergencyContactName = "李老师",
            emergencyContactPhone = "13900139000",
            qualificationImages = new[] { Attachment("/uploads/volunteer/qualification.jpg") },
            volunteerCertificateImages = new[] { Attachment("/uploads/volunteer/cert.jpg") },
            idNumber = "990000199001010017",
            idCardFront = Attachment("/uploads/volunteer/id-front.jpg"),
            idCardBack = Attachment("/uploads/volunteer/id-back.jpg"),
            homeAddress = "演示市演示区",
            personalResume = "参加过校园急救培训",
            joinReason,
            commitmentAccepted = true,
            derivedFromId = derivedFromValue
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

    private sealed class UserApiFixture : IDisposable
    {
        private readonly ProviderTempData temp;
        private readonly AdminTestApplication app;

        private UserApiFixture(ProviderTempData temp, AdminTestApplication app)
        {
            this.temp = temp;
            this.app = app;
            Client = app.CreateClient();
        }

        public HttpClient Client { get; }

        public static async Task<UserApiFixture> CreateAsync(bool withFakeLogin = false)
        {
            var temp = new ProviderTempData();
            await temp.WriteValidDataAsync();
            await temp.WriteUserJsonAsync(OpenId, "13800138000", UserToken);
            await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
            var app = new AdminTestApplication(
                temp.Root,
                SqliteConfig(temp, withFakeLogin),
                withFakeLogin ? ConfigureFakeLogin : null);
            return new UserApiFixture(temp, app);
        }

        public static async Task<UserApiFixture> CreateJsonAsync()
        {
            var temp = new ProviderTempData();
            await temp.WriteMinimalJsonAsync();
            await temp.WriteUserJsonAsync(OpenId, "13800138000", UserToken);
            return new UserApiFixture(temp, new AdminTestApplication(temp.Root));
        }

        public void UseUserToken()
        {
            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", UserToken);
        }

        public void UseAdminKey()
        {
            Client.DefaultRequestHeaders.Authorization = null;
            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
            Client.DefaultRequestHeaders.Add("X-Aed-Admin-Key", ProviderTempData.AdminSessionKey);
        }

        public void ClearHeaders()
        {
            Client.DefaultRequestHeaders.Authorization = null;
            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
        }

        public void Dispose()
        {
            Client.Dispose();
            app.Dispose();
            temp.Dispose();
        }

        private static Dictionary<string, string?> SqliteConfig(
            ProviderTempData temp,
            bool withFakeLogin = false)
        {
            var config = new Dictionary<string, string?>
            {
                ["Data:Provider"] = "Sqlite",
                ["Data:SqlitePath"] = temp.DbPath
            };
            if (withFakeLogin)
            {
                config["WeChat:AppId"] = "test-app-id";
                config["WeChat:AppSecret"] = "test-app-secret";
            }
            return config;
        }

        private static void ConfigureFakeLogin(IServiceCollection services)
        {
            services.RemoveAll<IWeChatLoginClient>();
            services.AddSingleton<IWeChatLoginClient>(new FakeWeChatLoginClient(OpenId));
        }
    }

    private sealed class FakeWeChatLoginClient : IWeChatLoginClient
    {
        private readonly string openId;

        public FakeWeChatLoginClient(string openId)
        {
            this.openId = openId;
        }

        public Task<WeChatCodeSessionResult> CodeToSessionAsync(
            string code,
            CancellationToken cancellationToken)
        {
            var result = new WeChatCodeSessionResult(
                true,
                new WeChatLoginSession(openId, null),
                0,
                string.Empty);
            return Task.FromResult(result);
        }
    }
}
