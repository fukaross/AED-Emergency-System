using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Responder;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Admin;
using AedBackend.Tests.Persistence;

namespace AedBackend.Tests.User;

public sealed class UserArchiveApiTests
{
    private const string UserToken = "user-archive-token";
    private const string OpenId = "openid-archive";

    [Fact]
    public async Task Archives_RequireLoginAndSqliteProvider()
    {
        using var sqlite = await ArchiveFixture.CreateSqliteAsync();
        var unauthorized = await sqlite.Client.GetAsync("/api/user/archives");
        await AssertErrorAsync(unauthorized, HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);

        using var json = await ArchiveFixture.CreateJsonAsync();
        json.UseUserToken();
        var list = await json.Client.GetAsync("/api/user/archives");
        var detail = await json.Client.GetAsync("/api/user/archives/rescuer-001");
        var phone = await json.Client.GetAsync("/api/user/archives/rescuer-001/phone?purpose=test");

        await AssertErrorAsync(list, HttpStatusCode.NotImplemented, ErrorCodes.FeatureRequiresSqlite);
        await AssertErrorAsync(detail, HttpStatusCode.NotImplemented, ErrorCodes.FeatureRequiresSqlite);
        await AssertErrorAsync(phone, HttpStatusCode.NotImplemented, ErrorCodes.FeatureRequiresSqlite);
    }

    [Fact]
    public async Task ListArchives_FiltersPagesAndSeparatesSelfArchive()
    {
        using var fixture = await ArchiveFixture.CreateSqliteAsync(
            Responders(
                ProviderTempData.Responder("rescuer-self", "本人", "core", boundOpenId: OpenId),
                ProviderTempData.Responder("rescuer-001", "演示档案甲", "trainee"),
                ProviderTempData.Responder("rescuer-002", "演示档案乙", "core"),
                ProviderTempData.Responder("rescuer-off", "离线演示档案", "core", "inactive")));
        fixture.UseUserToken();

        var response = await fixture.Client.GetAsync("/api/user/archives?level=core&page=1&pageSize=1");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
        var item = data.GetProperty("items")[0];

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(data.GetProperty("canViewFullPhone").GetBoolean());
        Assert.Equal(1, data.GetProperty("total").GetInt32());
        Assert.Equal(1, data.GetProperty("pageSize").GetInt32());
        Assert.Equal("rescuer-self", data.GetProperty("selfArchive").GetProperty("id").GetString());
        Assert.Equal("rescuer-002", item.GetProperty("id").GetString());
        Assert.Equal("138****8000", item.GetProperty("phone").GetString());
        // 校区显示名来自 config/site.json（east → 东区），不再是源码分支。
        Assert.Equal("东区", item.GetProperty("college").GetString());
        Assert.False(item.TryGetProperty("idNumber", out _));
    }

    [Fact]
    public async Task ListArchives_SearchesKeywordAndRejectsInvalidPaging()
    {
        using var fixture = await ArchiveFixture.CreateSqliteAsync(
            Responders(
                ProviderTempData.Responder("rescuer-001", "演示档案甲"),
                ProviderTempData.Responder("rescuer-002", "演示档案乙")));
        fixture.UseUserToken();

        var search = await fixture.Client.GetAsync("/api/user/archives?keyword=演示档案乙&page=1&pageSize=20");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(search)).Data;

        Assert.Equal(1, data.GetProperty("total").GetInt32());
        Assert.Equal("演示档案乙", data.GetProperty("items")[0].GetProperty("name").GetString());
        await AssertErrorAsync(
            await fixture.Client.GetAsync("/api/user/archives?page=0"),
            HttpStatusCode.BadRequest,
            ErrorCodes.ParameterInvalid);
        await AssertErrorAsync(
            await fixture.Client.GetAsync("/api/user/archives?pageSize=101"),
            HttpStatusCode.BadRequest,
            ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task Detail_ReturnsMaskedProfileAndCertificateProjection()
    {
        using var fixture = await ArchiveFixture.CreateSqliteAsync();
        fixture.UseUserToken();

        var response = await fixture.Client.GetAsync("/api/user/archives/rescuer-001");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
        var profile = data.GetProperty("profile");
        var certificate = data.GetProperty("certificateRecords")[0];

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("rescuer-001", data.GetProperty("id").GetString());
        Assert.Equal("female", profile.GetProperty("gender").GetString());
        Assert.Equal("138****8000", profile.GetProperty("phoneMasked").GetString());
        Assert.Contains("******", profile.GetProperty("idMasked").GetString());
        Assert.False(profile.TryGetProperty("phone", out _));
        Assert.False(profile.TryGetProperty("idNumber", out _));
        Assert.Equal("FIXTURE-001", certificate.GetProperty("id").GetString());
        Assert.Equal("TESTCERT0001", certificate.GetProperty("certificateNo").GetString());
        Assert.True(certificate.GetProperty("isCurrent").GetBoolean());
    }

    [Fact]
    public async Task Detail_Returns404ForMissingArchive()
    {
        using var fixture = await ArchiveFixture.CreateSqliteAsync();
        fixture.UseUserToken();

        var response = await fixture.Client.GetAsync("/api/user/archives/rescuer-999");
        await AssertErrorAsync(response, HttpStatusCode.NotFound, ErrorCodes.RescuerNotFound);
    }

    [Fact]
    public async Task Phone_RequiresPurposeReturnsPhoneAndWritesAudit()
    {
        using var fixture = await ArchiveFixture.CreateSqliteAsync();
        fixture.UseUserToken();

        await AssertErrorAsync(
            await fixture.Client.GetAsync("/api/user/archives/rescuer-001/phone?purpose=%20"),
            HttpStatusCode.BadRequest,
            ErrorCodes.ParameterInvalid);

        var response = await fixture.Client.GetAsync(
            "/api/user/archives/rescuer-001/phone?purpose=emergency");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("13800138000", data.GetProperty("phone").GetString());

        fixture.UseAdminKey();
        var logs = await fixture.Client.GetAsync(
            "/api/admin/audit-logs?eventName=archive.view_phone&keyword=rescuer-001");
        var logData = (await ReadAsync<ApiResponse<JsonElement>>(logs)).Data;
        var log = logData.GetProperty("items")[0];

        Assert.Equal(1, logData.GetProperty("total").GetInt32());
        Assert.Equal("archive", log.GetProperty("eventType").GetString());
        Assert.Equal(OpenId, log.GetProperty("actorId").GetString());
        Assert.DoesNotContain("13800138000", log.GetRawText());
    }

    private static ResponderProfileRecord[] Responders(params ResponderProfileRecord[] items) => items;

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

    private sealed class ArchiveFixture : IDisposable
    {
        private readonly ProviderTempData temp;
        private readonly AdminTestApplication app;

        private ArchiveFixture(ProviderTempData temp, AdminTestApplication app)
        {
            this.temp = temp;
            this.app = app;
            Client = app.CreateClient();
        }

        public HttpClient Client { get; }

        public static async Task<ArchiveFixture> CreateSqliteAsync(
            ResponderProfileRecord[]? responders = null)
        {
            var temp = new ProviderTempData();
            await temp.WriteValidDataAsync();
            await temp.WriteUserJsonAsync(OpenId, "13800138000", UserToken);
            if (responders is not null) await temp.WriteResponderJsonAsync(responders);
            await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
            return new ArchiveFixture(temp, new AdminTestApplication(temp.Root, SqliteConfig(temp)));
        }

        public static async Task<ArchiveFixture> CreateJsonAsync()
        {
            var temp = new ProviderTempData();
            await temp.WriteValidDataAsync();
            await temp.WriteUserJsonAsync(OpenId, "13800138000", UserToken);
            return new ArchiveFixture(temp, new AdminTestApplication(temp.Root));
        }

        public void UseUserToken()
        {
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", UserToken);
            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
        }

        public void UseAdminKey()
        {
            Client.DefaultRequestHeaders.Authorization = null;
            Client.DefaultRequestHeaders.Remove("X-Aed-Admin-Key");
            Client.DefaultRequestHeaders.Add("X-Aed-Admin-Key", ProviderTempData.AdminSessionKey);
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
