using AedBackend.Infrastructure.Demo;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Demo;

/**
 * 受限本地身份适配：Demo 开启时固定 loginCode 直接映射到 demo/roles.json 的 openId，
 * 不访问微信；Demo 关闭（真实模式）时完全走真实微信客户端，演示 code 不会命中本地映射。
 */
public sealed class DemoAwareWeChatLoginClientTests
{
    private const string WeChatInvalidCodeResponse = """{"errcode":40029,"errmsg":"invalid code"}""";
    private const string WeChatSuccessResponse = """{"openid":"wechat-openid-real","session_key":"session","unionid":null}""";

    [Fact]
    public async Task DemoEnabled_KnownCode_MapsToConfiguredOpenIdWithoutWeChatCall()
    {
        var (client, handler) = CreateClient(demoEnabled: true, WeChatSuccessResponse);

        var result = await client.CodeToSessionAsync("demo-rescuer", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Session);
        Assert.Equal("demo-openid-rescuer-east", result.Session!.OpenId);
        Assert.Empty(handler.RequestUris);
    }

    [Fact]
    public async Task DemoEnabled_AllFixedCodes_MapToDistinctDemoIdentities()
    {
        var (client, _) = CreateClient(demoEnabled: true, WeChatSuccessResponse);

        var requester = await client.CodeToSessionAsync("demo-requester", CancellationToken.None);
        var rescuer = await client.CodeToSessionAsync("demo-rescuer", CancellationToken.None);
        var unqualified = await client.CodeToSessionAsync("demo-unqualified", CancellationToken.None);

        Assert.Equal("demo-openid-requester", requester.Session!.OpenId);
        Assert.Equal("demo-openid-rescuer-east", rescuer.Session!.OpenId);
        Assert.Equal("demo-openid-unqualified", unqualified.Session!.OpenId);
    }

    [Fact]
    public async Task DemoEnabled_UnknownCode_ReturnsExistingParameterErrorSemantics()
    {
        var (client, handler) = CreateClient(demoEnabled: true, WeChatSuccessResponse);

        var result = await client.CodeToSessionAsync("demo-nobody", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);
        Assert.Equal(DemoAwareWeChatLoginClient.UnknownDemoCodeErrorCode, result.ErrorCode);
        Assert.Empty(handler.RequestUris);
    }

    [Fact]
    public async Task DemoEnabled_DoesNotAcceptArbitraryOpenIdAsCode()
    {
        var (client, handler) = CreateClient(demoEnabled: true, WeChatSuccessResponse);

        var result = await client.CodeToSessionAsync("demo-openid-requester", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(handler.RequestUris);
    }

    [Fact]
    public async Task DemoDisabled_DemoCode_IsSentToRealWeChatClient()
    {
        var (client, handler) = CreateClient(demoEnabled: false, WeChatInvalidCodeResponse);

        var result = await client.CodeToSessionAsync("demo-requester", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(40029, result.ErrorCode);
        var requested = Assert.Single(handler.RequestUris);
        Assert.Contains("api.weixin.qq.com/sns/jscode2session", requested, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DemoDisabled_RealCode_UsesRealWeChatSession()
    {
        var (client, handler) = CreateClient(demoEnabled: false, WeChatSuccessResponse);

        var result = await client.CodeToSessionAsync("real-wechat-code", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("wechat-openid-real", result.Session!.OpenId);
        Assert.Single(handler.RequestUris);
    }

    private static (DemoAwareWeChatLoginClient Client, StubHttpMessageHandler Handler) CreateClient(
        bool demoEnabled,
        string responseBody)
    {
        var handler = new StubHttpMessageHandler(responseBody);
        var options = Options.Create(new WeChatOptions
        {
            AppId = "test-app-id",
            AppSecret = "test-app-secret",
            PublicBaseUrl = "http://127.0.0.1:5150"
        });
        var inner = new WeChatLoginClient(new HttpClient(handler), options);
        var client = new DemoAwareWeChatLoginClient(
            Options.Create(new DemoOptions { Enabled = demoEnabled }),
            new DemoRoleCatalog(),
            inner,
            NullLogger<DemoAwareWeChatLoginClient>.Instance);
        return (client, handler);
    }

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        public List<string> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri?.ToString() ?? string.Empty);
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
