using AedBackend.Core.DTOs.WeChat;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Tests.Rescue;

/** 捕获式订阅消息发送器替身：避免测试触达真实微信接口，并支持推送收窄断言。 */
internal sealed class FakeSubscribeMessageSender : IWeChatSubscribeMessageSender
{
    public List<SubscribeMessageCommand> Sent { get; } = [];

    public Task<SendSubscribeMessageResult> SendAsync(
        SubscribeMessageCommand command,
        CancellationToken cancellationToken)
    {
        Sent.Add(command);
        return Task.FromResult(new SendSubscribeMessageResult(true, 0, "ok", "msg-1"));
    }
}
