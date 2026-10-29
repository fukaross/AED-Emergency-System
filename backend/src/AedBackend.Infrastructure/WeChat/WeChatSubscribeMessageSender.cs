using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AedBackend.Core.DTOs.WeChat;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Infrastructure.WeChat;

public static class WeChatErrorCodes
{
    public const int Ok = 0;
    public const int UserRefused = 43101;
    public const int EmptyResponse = -2;
}

public sealed class WeChatSubscribeMessageSender(
    HttpClient httpClient,
    WeChatAccessTokenClient accessTokenClient)
    : IWeChatSubscribeMessageSender
{
    public async Task<SendSubscribeMessageResult> SendAsync(
        SubscribeMessageCommand command,
        CancellationToken cancellationToken)
    {
        var accessToken = await accessTokenClient.GetAccessTokenAsync(cancellationToken);
        var uri = BuildSendUri(accessToken);
        var payload = CreatePayload(command);

        using var request = CreateHttpRequest(uri, payload);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var bodyText = await response.Content.ReadAsStringAsync(cancellationToken);
        var body = DeserializeResponse(bodyText);

        if (!response.IsSuccessStatusCode)
        {
            return CreateHttpFailureResult(response, body, bodyText);
        }

        return CreateBusinessResult(body);
    }

    private static WeChatSendResponse? DeserializeResponse(string bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WeChatSendResponse>(bodyText);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SendSubscribeMessageResult CreateBusinessResult(WeChatSendResponse? body)
    {
        if (body is null)
        {
            return new SendSubscribeMessageResult(
                false,
                WeChatErrorCodes.EmptyResponse,
                "微信订阅消息响应为空",
                null);
        }

        var errorCode = body.ErrorCode ?? WeChatErrorCodes.Ok;
        return new SendSubscribeMessageResult(
            errorCode == WeChatErrorCodes.Ok,
            errorCode,
            body.ErrorMessage ?? "ok",
            body.MessageId);
    }

    private static SendSubscribeMessageResult CreateHttpFailureResult(
        HttpResponseMessage response,
        WeChatSendResponse? body,
        string bodyText)
    {
        var errorCode = body?.ErrorCode ?? -(int)response.StatusCode;
        var errorMessage = body?.ErrorMessage ?? CreateHttpFailureMessage(response, bodyText);

        return new SendSubscribeMessageResult(
            false,
            errorCode,
            errorMessage,
            body?.MessageId);
    }

    private static HttpRequestMessage CreateHttpRequest(string uri, WeChatSendRequest payload)
    {
        var body = JsonSerializer.Serialize(payload);
        var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        return new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = content,
            Headers =
            {
                UserAgent = { ProductInfoHeaderValue.Parse("AedBackend/1.0") }
            }
        };
    }

    private static string CreateHttpFailureMessage(HttpResponseMessage response, string bodyText)
    {
        var message = $"微信订阅消息 HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        return string.IsNullOrWhiteSpace(bodyText)
            ? message
            : $"{message}: {bodyText}";
    }

    private static string BuildSendUri(string accessToken)
    {
        var token = Uri.EscapeDataString(accessToken);
        return $"https://api.weixin.qq.com/cgi-bin/message/subscribe/send?access_token={token}";
    }

    private static WeChatSendRequest CreatePayload(SubscribeMessageCommand command)
    {
        return new WeChatSendRequest(
            command.OpenId,
            command.TemplateId,
            command.Page,
            command.Data,
            command.MiniProgramState,
            "zh_CN");
    }

    private sealed record WeChatSendRequest(
        [property: JsonPropertyName("touser")] string ToUser,
        [property: JsonPropertyName("template_id")] string TemplateId,
        [property: JsonPropertyName("page")] string? Page,
        [property: JsonPropertyName("data")] IReadOnlyDictionary<string, SubscribeMessageDataValue> Data,
        [property: JsonPropertyName("miniprogram_state")] string MiniProgramState,
        [property: JsonPropertyName("lang")] string Lang);

    private sealed record WeChatSendResponse(
        [property: JsonPropertyName("errcode")] int? ErrorCode,
        [property: JsonPropertyName("errmsg")] string? ErrorMessage,
        [property: JsonPropertyName("msgid")] string? MessageId);
}
