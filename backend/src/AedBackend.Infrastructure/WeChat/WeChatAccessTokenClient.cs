using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.WeChat;

public sealed class WeChatAccessTokenClient
{
    private static readonly TimeSpan RefreshAhead = TimeSpan.FromMinutes(5);

    private readonly HttpClient httpClient;
    private readonly IOptions<WeChatOptions> options;
    private readonly SemaphoreSlim tokenLock = new(1, 1);

    private AccessTokenSnapshot? cachedToken;

    public WeChatAccessTokenClient(HttpClient httpClient, IOptions<WeChatOptions> options)
    {
        this.httpClient = httpClient;
        this.options = options;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (cachedToken is { } snapshot && IsUsable(snapshot))
        {
            return snapshot.Token;
        }

        await tokenLock.WaitAsync(cancellationToken);

        try
        {
            if (cachedToken is { } lockedSnapshot && IsUsable(lockedSnapshot))
            {
                return lockedSnapshot.Token;
            }

            cachedToken = await RequestAccessTokenAsync(cancellationToken);
            return cachedToken.Value.Token;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private static bool IsUsable(AccessTokenSnapshot snapshot)
    {
        return snapshot.ExpiresAt > DateTimeOffset.UtcNow.Add(RefreshAhead);
    }

    private async Task<AccessTokenSnapshot> RequestAccessTokenAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var uri = BuildTokenUri(settings);
        var response = await httpClient.GetFromJsonAsync<WeChatAccessTokenResponse>(uri, cancellationToken);

        if (response?.AccessToken is not null)
        {
            return new AccessTokenSnapshot(
                response.AccessToken,
                DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn));
        }

        var errorCode = response?.ErrorCode?.ToString() ?? "empty_response";
        var errorMessage = response?.ErrorMessage ?? "微信 access_token 响应为空";
        throw new InvalidOperationException($"获取微信 access_token 失败：{errorCode} {errorMessage}");
    }

    private static string BuildTokenUri(WeChatOptions options)
    {
        var appId = Uri.EscapeDataString(options.AppId);
        var appSecret = Uri.EscapeDataString(options.AppSecret);

        return "https://api.weixin.qq.com/cgi-bin/token"
            + $"?grant_type=client_credential&appid={appId}&secret={appSecret}";
    }

    private readonly record struct AccessTokenSnapshot(string Token, DateTimeOffset ExpiresAt);

    private sealed record WeChatAccessTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("errcode")] int? ErrorCode,
        [property: JsonPropertyName("errmsg")] string? ErrorMessage);
}
