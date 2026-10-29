using AedBackend.Core.DTOs.WeChat;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Demo;

/**
 * 演示模式不发送真实微信订阅消息：返回明确的“演示未发送”结果并记录日志，
 * 绝不报告发送成功，也不调用微信接口。非演示模式委托真实发送器。
 */
public sealed class DemoAwareSubscribeMessageSender(
    IOptions<DemoOptions> demoOptions,
    WeChatSubscribeMessageSender inner,
    ILogger<DemoAwareSubscribeMessageSender> logger) : IWeChatSubscribeMessageSender
{
    public const int DemoNotSentErrorCode = -100;
    public const string DemoNotSentMessage = "演示模式未发送（本地演示，未调用微信订阅消息接口）";

    public Task<SendSubscribeMessageResult> SendAsync(
        SubscribeMessageCommand command,
        CancellationToken cancellationToken)
    {
        if (!demoOptions.Value.Enabled)
        {
            return inner.SendAsync(command, cancellationToken);
        }

        logger.LogInformation(
            "演示模式未发送微信订阅消息：templateId={TemplateId} openId={OpenId}（未外发）",
            command.TemplateId,
            command.OpenId);
        return Task.FromResult(new SendSubscribeMessageResult(
            false,
            DemoNotSentErrorCode,
            DemoNotSentMessage,
            null));
    }
}
