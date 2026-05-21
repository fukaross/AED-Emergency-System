using AedBackend.Core.Models.WeChat;

namespace AedBackend.Core.Services.WeChat;

public interface IWeChatPhoneNumberClient
{
    Task<WeChatPhoneNumberResult> GetPhoneNumberAsync(
        string code,
        CancellationToken cancellationToken);
}
