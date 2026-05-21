using AedBackend.Core.DTOs.WeChat;

namespace AedBackend.Core.Services.WeChat;

public interface IWeChatSubscribeMessageSender
{
    Task<SendSubscribeMessageResult> SendAsync(
        SubscribeMessageCommand command,
        CancellationToken cancellationToken);
}
