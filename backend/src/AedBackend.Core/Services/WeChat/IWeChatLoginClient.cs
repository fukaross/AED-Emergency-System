using AedBackend.Core.Models.WeChat;

namespace AedBackend.Core.Services.WeChat;

public interface IWeChatLoginClient
{
    Task<WeChatCodeSessionResult> CodeToSessionAsync(
        string code,
        CancellationToken cancellationToken);
}
