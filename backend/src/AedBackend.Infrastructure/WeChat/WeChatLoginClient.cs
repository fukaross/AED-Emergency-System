using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.WeChat;

public sealed class WeChatLoginClient : IWeChatLoginClient
{
    private readonly HttpClient httpClient;
    private readonly IOptions<WeChatOptions> options;

    public WeChatLoginClient(HttpClient httpClient, IOptions<WeChatOptions> options)
    {
        this.httpClient = httpClient;
        this.options = options;
    }

    public async Task<WeChatCodeSessionResult> CodeToSessionAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var uri = BuildCodeSessionUri(options.Value, code);
        var response = await httpClient.GetFromJsonAsync<WeChatCodeSessionResponse>(
            uri,
            cancellationToken);

        if (response?.OpenId is not null)
        {
            var session = new WeChatLoginSession(response.OpenId, response.UnionId);
            return new WeChatCodeSessionResult(true, session, 0, "ok");
        }

        return new WeChatCodeSessionResult(
            false,
            null,
            response?.ErrorCode ?? -1,
            response?.ErrorMessage ?? "微信登录响应为空");
    }

    private static string BuildCodeSessionUri(WeChatOptions options, string code)
    {
        var appId = Uri.EscapeDataString(options.AppId);
        var appSecret = Uri.EscapeDataString(options.AppSecret);
        var jsCode = Uri.EscapeDataString(code);

        return "https://api.weixin.qq.com/sns/jscode2session"
            + $"?appid={appId}&secret={appSecret}&js_code={jsCode}"
            + "&grant_type=authorization_code";
    }

    private sealed record WeChatCodeSessionResponse(
        [property: JsonPropertyName("openid")] string? OpenId,
        [property: JsonPropertyName("session_key")] string? SessionKey,
        [property: JsonPropertyName("unionid")] string? UnionId,
        [property: JsonPropertyName("errcode")] int? ErrorCode,
        [property: JsonPropertyName("errmsg")] string? ErrorMessage);
}
