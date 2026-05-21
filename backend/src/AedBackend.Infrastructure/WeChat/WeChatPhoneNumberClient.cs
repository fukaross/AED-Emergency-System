using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Infrastructure.WeChat;

public sealed class WeChatPhoneNumberClient(
    HttpClient httpClient,
    WeChatAccessTokenClient accessTokenClient)
    : IWeChatPhoneNumberClient
{
    public async Task<WeChatPhoneNumberResult> GetPhoneNumberAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var accessToken = await accessTokenClient.GetAccessTokenAsync(cancellationToken);
        var uri = BuildPhoneNumberUri(accessToken);
        var payload = new WeChatPhoneNumberRequest(code);

        using var response = await httpClient.PostAsJsonAsync(uri, payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<WeChatPhoneNumberResponse>(cancellationToken);
        if (body is null)
        {
            throw new InvalidOperationException("微信手机号响应为空");
        }

        var errorCode = body.ErrorCode ?? WeChatErrorCodes.Ok;
        if (errorCode == WeChatErrorCodes.Ok && body.PhoneInfo?.PhoneNumber is { } phone)
        {
            return new WeChatPhoneNumberResult(true, phone, errorCode, body.ErrorMessage ?? "ok");
        }

        return new WeChatPhoneNumberResult(
            false,
            null,
            errorCode,
            body.ErrorMessage ?? "微信手机号响应缺少 phone_info");
    }

    private static string BuildPhoneNumberUri(string accessToken)
    {
        var token = Uri.EscapeDataString(accessToken);
        return $"https://api.weixin.qq.com/wxa/business/getuserphonenumber?access_token={token}";
    }

    private sealed record WeChatPhoneNumberRequest(
        [property: JsonPropertyName("code")] string Code);

    private sealed record WeChatPhoneNumberResponse(
        [property: JsonPropertyName("errcode")] int? ErrorCode,
        [property: JsonPropertyName("errmsg")] string? ErrorMessage,
        [property: JsonPropertyName("phone_info")] WeChatPhoneInfo? PhoneInfo);

    private sealed record WeChatPhoneInfo(
        [property: JsonPropertyName("phoneNumber")] string PhoneNumber);
}
