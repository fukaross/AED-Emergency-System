using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Admin;
using AedBackend.Tests.Persistence;

namespace AedBackend.Tests.User;

public sealed class UserIdentityApiTests
{
    private const string UserToken = "user-identity-token";
    private const string OpenId = "openid-identity";

    [Fact]
    public async Task SchoolBinding_SubmitsListsAndBlocksDuplicatePending()
    {
        using var fixture = await Fixture.CreateAsync();
        fixture.UseUserToken();

        var first = await fixture.Client.PostAsJsonAsync("/api/user/school-bindings", SchoolPayload("20260101"));
        var firstData = (await ReadAsync<ApiResponse<JsonElement>>(first)).Data;
        var duplicate = await fixture.Client.PostAsJsonAsync("/api/user/school-bindings", SchoolPayload("20260102"));
        var list = await fixture.Client.GetAsync("/api/user/school-bindings?page=1&pageSize=1");
        var page = (await ReadAsync<ApiResponse<JsonElement>>(list)).Data;

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("submitted", firstData.GetProperty("status").GetString());
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal("王小明", page.GetProperty("items")[0].GetProperty("payload").GetProperty("name").GetString());
        await AssertErrorAsync(duplicate, HttpStatusCode.Conflict, ErrorCodes.PendingApplicationExists);
    }

    [Fact]
    public async Task ResponderClaim_SubmitsListsAndBlocksDuplicatePending()
    {
        using var fixture = await Fixture.CreateAsync();
        fixture.UseUserToken();

        var first = await fixture.Client.PostAsJsonAsync("/api/user/responder-claims", ClaimPayload());
        var firstData = (await ReadAsync<ApiResponse<JsonElement>>(first)).Data;
        var duplicate = await fixture.Client.PostAsJsonAsync("/api/user/responder-claims", ClaimPayload());
        var list = await fixture.Client.GetAsync("/api/user/responder-claims");
        var page = (await ReadAsync<ApiResponse<JsonElement>>(list)).Data;

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("submitted", firstData.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("items")[0].GetProperty("responderProfileId").ValueKind);
        Assert.Equal("王小明", page.GetProperty("items")[0].GetProperty("payload").GetProperty("name").GetString());
        await AssertErrorAsync(duplicate, HttpStatusCode.Conflict, ErrorCodes.PendingApplicationExists);
    }

    [Fact]
    public async Task IdentityEndpoints_RejectInvalidPayloadAndPaging()
    {
        using var fixture = await Fixture.CreateAsync();
        fixture.UseUserToken();

        var badSchool = await fixture.Client.PostAsJsonAsync(
            "/api/user/school-bindings",
            SchoolPayload(studentId: null));
        var badClaim = await fixture.Client.PostAsJsonAsync(
            "/api/user/responder-claims",
            ClaimPayload(idNumberLast6: "abc"));
        var badPage = await fixture.Client.GetAsync("/api/user/school-bindings?page=0");

        await AssertErrorAsync(badSchool, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
        await AssertErrorAsync(badClaim, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
        await AssertErrorAsync(badPage, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task IdentityEndpoints_RequireSqliteProvider()
    {
        using var fixture = await Fixture.CreateJsonAsync();
        fixture.UseUserToken();

        var school = await fixture.Client.GetAsync("/api/user/school-bindings");
        var claim = await fixture.Client.GetAsync("/api/user/responder-claims");

        await AssertErrorAsync(school, HttpStatusCode.NotImplemented, ErrorCodes.FeatureRequiresSqlite);
        await AssertErrorAsync(claim, HttpStatusCode.NotImplemented, ErrorCodes.FeatureRequiresSqlite);
    }

    private static object SchoolPayload(string? studentId = "20260101")
    {
        return new
        {
            personType = "student",
            name = "王小明",
            college = "教育学院",
            className = "急救 1 班",
            studentId,
            phone = "13800138000",
            note = "本人校园身份绑定"
        };
    }

    private static object ClaimPayload(string idNumberLast6 = "10011X")
    {
        return new
        {
            name = "王小明",
            idNumberLast6,
            phone = "13800138000",
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

        public static async Task<Fixture> CreateAsync()
        {
            var temp = new ProviderTempData();
            await temp.WriteMinimalJsonAsync();
            await temp.WriteUserJsonAsync(OpenId, "13800138000", UserToken);
            await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
            return new Fixture(temp, new AdminTestApplication(temp.Root, SqliteConfig(temp)));
        }

        public static async Task<Fixture> CreateJsonAsync()
        {
            var temp = new ProviderTempData();
            await temp.WriteMinimalJsonAsync();
            await temp.WriteUserJsonAsync(OpenId, "13800138000", UserToken);
            return new Fixture(temp, new AdminTestApplication(temp.Root));
        }

        public void UseUserToken()
        {
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
