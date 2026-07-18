using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.WeChat;
using AedBackend.Tests.Admin;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Tests.Rescue;

public sealed class HelpPublishLifecycleApiTests : AdminApiTestBase
{
    private const string SubscribeTemplateId = "test-template";
    private const string RequesterToken = "token-requester";
    private const string ResponderToken = "token-responder";
    private const string SessionPagePrefix = "pages-sub/rescue/session?id=";
    private static readonly TimeSpan ConcurrentRequestTimeout = TimeSpan.FromSeconds(5);

    private FakeSubscribeMessageSender Sender { get; } = new();

    protected override Action<IServiceCollection>? ConfigureServices => services =>
    {
        services.Configure<WeChatOptions>(options => options.SubscribeTemplateId = SubscribeTemplateId);
        services.AddSingleton<IWeChatSubscribeMessageSender>(Sender);
    };

    [Fact]
    public async Task Publish_BlockedPush_DoesNotHoldSessionLifecycleLease()
    {
        SeedLoggedInExperienceUsers(
            SubscribeTemplateId,
            ("openid-1", RequesterToken),
            ("openid-2", ResponderToken));
        SeedBoundResponders(("rescuer-t1", "openid-2", "active", "张急救"));
        Sender.BlockSend();

        using var publishRequest = CreatePublishRequest();
        var publishTask = Client.SendAsync(publishRequest);
        using var timeout = new CancellationTokenSource(ConcurrentRequestTimeout);
        HttpResponseMessage joinResponse;
        var publishWasBlocked = false;
        try
        {
            await Sender.WaitForSendAsync(timeout.Token);
            var page = Assert.IsType<string>(Assert.Single(Sender.Sent).Page);
            Assert.StartsWith(SessionPagePrefix, page, StringComparison.Ordinal);
            using var joinRequest = CreateJoinRequest(page[SessionPagePrefix.Length..]);
            joinResponse = await Client.SendAsync(joinRequest, timeout.Token);
            publishWasBlocked = !publishTask.IsCompleted;
        }
        finally
        {
            Sender.ReleaseSend();
        }

        var publishResponse = await publishTask.WaitAsync(timeout.Token);
        Assert.True(publishWasBlocked);
        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
    }

    [Fact]
    public async Task Publish_ClientDisconnectAfterCommit_DoesNotCancelNotification()
    {
        SeedLoggedInExperienceUsers(
            SubscribeTemplateId,
            ("openid-1", RequesterToken),
            ("openid-2", ResponderToken));
        SeedBoundResponders(("rescuer-t1", "openid-2", "active", "张急救"));
        Sender.BlockSend();

        using var publishRequest = CreatePublishRequest();
        using var requestCancellation = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource(ConcurrentRequestTimeout);
        var publishTask = Client.SendAsync(publishRequest, requestCancellation.Token);
        await Sender.WaitForSendAsync(timeout.Token);

        Assert.False(Sender.LastTokenCanBeCanceled);
        requestCancellation.Cancel();
        Sender.ReleaseSend();
        await Sender.WaitForCompletionAsync(timeout.Token);
        Assert.False(Sender.CancellationObserved);
        await ObserveCancellationAsync(publishTask);
    }

    private static async Task ObserveCancellationAsync(Task<HttpResponseMessage> request)
    {
        try
        {
            using var response = await request;
        }
        catch (OperationCanceledException)
        {
            // 客户端观察到取消是预期行为；服务端提交后的通知仍必须完成。
        }
    }

    private static HttpRequestMessage CreatePublishRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/help/request")
        {
            Content = JsonContent.Create(new
            {
                lat = 19.974,
                lng = 110.517,
                address = "教学楼A一层大厅",
                content = "有人晕倒需要协助",
                type = "help_hand"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", RequesterToken);
        return request;
    }

    private static HttpRequestMessage CreateJoinRequest(string helpId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/help/{helpId}/session/join");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ResponderToken);
        return request;
    }

}
