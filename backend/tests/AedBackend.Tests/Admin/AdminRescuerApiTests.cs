using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;

namespace AedBackend.Tests.Admin;

public sealed class AdminRescuerApiTests : AdminApiTestBase
{
    [Fact]
    public async Task ListRescuers_RequiresAdminSessionAndRejectsLegacyKey()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client.GetAsync("/api/admin/rescuers")).StatusCode);

        UseLegacyKey();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client.GetAsync("/api/admin/rescuers")).StatusCode);

        UseAdminKey(await BootstrapAndLoginAsync());
        var response = await Client.GetAsync("/api/admin/rescuers?pageSize=100");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(31, data.GetProperty("total").GetInt32());
        Assert.Equal(31, data.GetProperty("items").GetArrayLength());
        Assert.Equal("rescuer-001", data.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.Equal("rescuer-031", data.GetProperty("items")[30].GetProperty("id").GetString());
    }

    [Fact]
    public async Task ListRescuers_MasksPhoneAndExcludesIdNumber()
    {
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
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.GetAsync("/api/admin/rescuers?campus=fucheng");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(data.GetProperty("items").EnumerateArray(),
            item => Assert.Equal("fucheng", item.GetProperty("campus").GetString()));
        Assert.True(data.GetProperty("total").GetInt32() > 0);
        Assert.True(data.GetProperty("total").GetInt32() < 31);
    }

    [Fact]
    public async Task GetRescuerDetail_ReturnsMaskedSensitiveFields()
    {
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.GetAsync("/api/admin/rescuers/rescuer-001");
        var data = (await ReadAsync<ApiResponse<JsonElement>>(response)).Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("rescuer-001", data.GetProperty("id").GetString());
        Assert.Equal("REDACTED", data.GetProperty("name").GetString());
        Assert.Equal("女", data.GetProperty("gender").GetString());
        Assert.Equal("guilinyang", data.GetProperty("campus").GetString());
        Assert.True(data.GetProperty("skillCpr").GetBoolean());

        var phoneMasked = data.GetProperty("phoneMasked").GetString();
        Assert.Contains("****", phoneMasked);
        var phone = data.GetProperty("phone").GetString();
        Assert.DoesNotContain("*", phone);
        Assert.StartsWith("139", phone);
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
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.GetAsync("/api/admin/rescuers/rescuer-999");
        await AssertErrorAsync(response, HttpStatusCode.NotFound, ErrorCodes.RescuerNotFound);
    }

    [Fact]
    public async Task UpdateRescuer_UpdatesFieldsAndWritesAudit()
    {
        UseAdminKey(await BootstrapAndLoginAsync());

        var updated = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new
        {
            name = "REDACTED(测试更新)",
            level = "official",
            qualificationStatus = "inactive",
            skillCpr = false
        });
        var item = (await ReadAsync<ApiResponse<JsonElement>>(updated)).Data;

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("REDACTED(测试更新)", item.GetProperty("name").GetString());
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
        UseAdminKey(await BootstrapAndLoginAsync());

        var invalidCampus = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new { campus = "beijing" });
        await AssertErrorAsync(invalidCampus, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var invalidLevel = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new { level = "super" });
        await AssertErrorAsync(invalidLevel, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var invalidStatus = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new { qualificationStatus = "unknown" });
        await AssertErrorAsync(invalidStatus, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var emptyName = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new { name = "" });
        await AssertErrorAsync(emptyName, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
    }

    [Fact]
    public async Task UpdateRescuer_RecalculatesCertificateNoMasked()
    {
        UseAdminKey(await BootstrapAndLoginAsync());

        var updated = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new
        {
            certificateNo = "460000232004231009999"
        });
        var item = (await ReadAsync<ApiResponse<JsonElement>>(updated)).Data;

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("460000232004231009999", item.GetProperty("certificateNo").GetString());
        Assert.Equal("4600****9999", item.GetProperty("certificateNoMasked").GetString());
    }

    [Fact]
    public async Task UpdateRescuer_ClearsNullableLevelAndCertificate()
    {
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
        UseAdminKey(await BootstrapAndLoginAsync());

        var tooShort = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001", new { idNumber = "123" });
        await AssertErrorAsync(tooShort, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var badChecksum = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001",
            new { idNumber = "REDACTED" });
        await AssertErrorAsync(badChecksum, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);

        var valid = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-001",
            new { idNumber = "REDACTED" });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task UpdateRescuer_ClearsAssessmentResults()
    {
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
        UseAdminKey(await BootstrapAndLoginAsync());

        var response = await Client.PutAsJsonAsync("/api/admin/rescuers/rescuer-999", new { name = "不存在" });
        await AssertErrorAsync(response, HttpStatusCode.NotFound, ErrorCodes.RescuerNotFound);
    }
}
