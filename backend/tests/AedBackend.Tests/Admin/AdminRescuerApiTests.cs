using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;

namespace AedBackend.Tests.Admin;

public sealed class AdminRescuerApiTests : AdminApiTestBase
{
    private const string SyntheticIdNumber = "990000200001010012";

    private void SeedStandardRescuers()
    {
        SeedRescuers(
            new RescuerSeed(
                "rescuer-001", "演示急救员甲", "east", Level: "official", Phone: "13900000001",
                CertificateNo: "TESTCERT12345678", CertificateNoMasked: "TEST****5678"),
            new RescuerSeed(
                "rescuer-002", "演示急救员乙", "east", Level: "core", Phone: "13900000002",
                CertificateNo: "TESTCERT12345679", CertificateNoMasked: "TEST****5679"),
            new RescuerSeed(
                "rescuer-003", "演示急救员丙", "west", Level: "trainee", Phone: "13900000003",
                Lat: 31.2188, Lng: 121.4536,
                CertificateNo: "TESTCERT12345680", CertificateNoMasked: "TEST****5680"));
    }

    [Fact]
    public async Task ListRescuers_RequiresAdminSessionAndRejectsLegacyKey()
    {
        SeedStandardRescuers();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client.GetAsync("/api/admin/rescuers")).StatusCode);

        UseLegacyKey();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client.GetAsync("/api/admin/rescuers")).StatusCode);

        UseAdminKey(await BootstrapAndLoginAsync());
        var response = await Client.GetAsync("/api/admin/rescuers?pageSize=100");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, data.GetProperty("total").GetInt32());
        Assert.Equal(3, data.GetProperty("items").GetArrayLength());
        Assert.Equal("rescuer-001", data.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.Equal("rescuer-003", data.GetProperty("items")[2].GetProperty("id").GetString());
    }

    [Fact]
    public async Task ListRescuers_MasksPhoneAndExcludesIdNumber()
    {
        SeedStandardRescuers();
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.GetAsync("/api/admin/rescuers?pageSize=100");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;
        var first = data.GetProperty("items")[0];

        var phoneMasked = first.GetProperty("phoneMasked").GetString();
        Assert.Contains("****", phoneMasked);
        Assert.False(first.TryGetProperty("phone", out _));
        Assert.False(first.TryGetProperty("idNumber", out _));
    }

    [Fact]
    public async Task ListRescuers_FiltersByCampus()
    {
        SeedStandardRescuers();
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.GetAsync("/api/admin/rescuers?campus=west");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(data.GetProperty("items").EnumerateArray(),
            item => Assert.Equal("west", item.GetProperty("campus").GetString()));
        Assert.Equal(1, data.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task GetRescuerDetail_ReturnsMaskedSensitiveFields()
    {
        SeedStandardRescuers();
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.GetAsync("/api/admin/rescuers/rescuer-001");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("rescuer-001", data.GetProperty("id").GetString());
        Assert.Equal("演示急救员甲", data.GetProperty("name").GetString());
        Assert.Equal("female", data.GetProperty("gender").GetString());
        Assert.Equal("east", data.GetProperty("campus").GetString());
        Assert.True(data.GetProperty("skillCpr").GetBoolean());

        var phoneMasked = data.GetProperty("phoneMasked").GetString();
        Assert.Contains("****", phoneMasked);
        var phone = data.GetProperty("phone").GetString();
        Assert.DoesNotContain("*", phone);
        Assert.Equal("13900000001", phone);
        var idMasked = data.GetProperty("idNumberMasked").GetString();
        Assert.Contains("******", idMasked);
        var idNumber = data.GetProperty("idNumber").GetString();
        Assert.DoesNotContain("*", idNumber);
        Assert.NotNull(idNumber);
        Assert.Equal(18, idNumber.Length);
    }

    [Fact]
    public async Task GetRescuerDetail_Returns404ForMissingRescuer()
    {
        SeedStandardRescuers();
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.GetAsync("/api/admin/rescuers/rescuer-999");
        await AssertErrorAsync(response, HttpStatusCode.NotFound, ErrorCodes.RescuerNotFound);
    }

    [Fact]
    public async Task UpdateRescuer_UpdatesFieldsAndWritesAudit()
    {
        SeedStandardRescuers();
        UseAdminKey(await BootstrapAndLoginAsync());

        var updated = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new
        {
            name = "演示急救员甲（测试更新）",
            level = "official",
            qualificationStatus = "inactive",
            skillCpr = false
        });
        var item = (await ReadAsync<ApiResponse<JsonElement>>(updated)).Data;

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("演示急救员甲（测试更新）", item.GetProperty("name").GetString());
        Assert.Equal("official", item.GetProperty("level").GetString());
        Assert.Equal("inactive", item.GetProperty("qualificationStatus").GetString());
        Assert.False(item.GetProperty("skillCpr").GetBoolean());

        var logs = await Client.GetAsync("/api/admin/audit-logs?eventName=rescuer.updated&keyword=rescuer-001");
        var logData = (await ReadAsync<ApiResponse<JsonElement>>(logs)).Data;
        Assert.Equal(1, logData.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task UpdateRescuer_RejectsInvalidCampusLevelStatus()
    {
        SeedStandardRescuers();
        UseAdminKey(await BootstrapAndLoginAsync());

        // 校区校验由 config/site.json 驱动：不在配置中的 code 仍是既有参数错误。
        var invalidCampus = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new { campus = "beijing" });
        await AssertErrorAsync(invalidCampus, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var invalidLevel = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new { level = "super" });
        await AssertErrorAsync(invalidLevel, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var invalidStatus = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new { qualificationStatus = "unknown" });
        await AssertErrorAsync(invalidStatus, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var emptyName = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new { name = "" });
        await AssertErrorAsync(emptyName, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var validCampus = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new { campus = "west" });
        Assert.Equal(HttpStatusCode.OK, validCampus.StatusCode);
    }

    [Fact]
    public async Task UpdateRescuer_RecalculatesCertificateNoMasked()
    {
        SeedStandardRescuers();
        UseAdminKey(await BootstrapAndLoginAsync());

        var updated = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new
        {
            certificateNo = "TESTCERT99998888"
        });
        var item = (await ReadAsync<ApiResponse<JsonElement>>(updated)).Data;

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("TESTCERT99998888", item.GetProperty("certificateNo").GetString());
        Assert.Equal("TEST****8888", item.GetProperty("certificateNoMasked").GetString());
    }

    [Fact]
    public async Task UpdateRescuer_ClearsNullableLevelAndCertificate()
    {
        SeedStandardRescuers();
        UseAdminKey(await BootstrapAndLoginAsync());

        var updated = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new
        {
            level = (string?)null,
            certificateNo = (string?)null
        });
        var item = (await ReadAsync<ApiResponse<JsonElement>>(updated)).Data;

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("level").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("certificateNo").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("certificateNoMasked").ValueKind);
    }

    [Fact]
    public async Task UpdateRescuer_ValidatesIdNumberFormat()
    {
        SeedStandardRescuers();
        UseAdminKey(await BootstrapAndLoginAsync());

        var tooShort = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new { idNumber = "123" });
        await AssertErrorAsync(tooShort, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var badChecksum = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001",
            new { idNumber = "990000200001010010" });
        await AssertErrorAsync(badChecksum, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var valid = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001",
            new { idNumber = SyntheticIdNumber });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task UpdateRescuer_ClearsAssessmentResults()
    {
        SeedStandardRescuers();
        UseAdminKey(await BootstrapAndLoginAsync());

        var updated = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new
        {
            cprAssessmentResult = (string?)null,
            aedAssessmentResult = (string?)null,
            traumaAssessmentResult = (string?)null,
            cprAedCombinedResult = (string?)null
        });
        var item = (await ReadAsync<ApiResponse<JsonElement>>(updated)).Data;

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("cprAssessmentResult").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("aedAssessmentResult").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("traumaAssessmentResult").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("cprAedCombinedResult").ValueKind);
    }

    [Fact]
    public async Task UpdateRescuer_SetsAssessmentResults()
    {
        SeedStandardRescuers();
        UseAdminKey(await BootstrapAndLoginAsync());

        var updated = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new
        {
            cprAssessmentResult = "合格",
            aedAssessmentResult = "不合格",
            traumaAssessmentResult = "未考核"
        });
        var item = (await ReadAsync<ApiResponse<JsonElement>>(updated)).Data;

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("合格", item.GetProperty("cprAssessmentResult").GetString());
        Assert.Equal("不合格", item.GetProperty("aedAssessmentResult").GetString());
        Assert.Equal("未考核", item.GetProperty("traumaAssessmentResult").GetString());
    }

    [Fact]
    public async Task UpdateRescuer_RejectsInvalidAssessmentResult()
    {
        SeedStandardRescuers();
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new
        {
            cprAssessmentResult = "优秀"
        });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task UpdateRescuer_Returns404ForMissingRescuer()
    {
        SeedStandardRescuers();
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-999", new { name = "不存在" });
        await AssertErrorAsync(response, HttpStatusCode.NotFound, ErrorCodes.RescuerNotFound);
    }
}
