using AedBackend.Core.DTOs.WeChat;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Tests.Rescue;

/** 捕获式订阅消息发送器替身：避免测试触达真实微信接口，并支持推送收窄断言。 */
internal sealed class FakeSubscribeMessageSender : IWeChatSubscribeMessageSender
{
    private readonly TaskCompletionSource sendStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource releaseSend = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource sendCompleted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private bool blockSend;

    public List<SubscribeMessageCommand> Sent { get; } = [];
    public bool LastTokenCanBeCanceled { get; private set; }
    public bool CancellationObserved { get; private set; }

    public void BlockSend()
    {
        blockSend = true;
    }

    public Task WaitForSendAsync(CancellationToken cancellationToken)
    {
        return sendStarted.Task.WaitAsync(cancellationToken);
    }

    public void ReleaseSend()
    {
        releaseSend.TrySetResult();
    }

    public Task WaitForCompletionAsync(CancellationToken cancellationToken)
    {
        return sendCompleted.Task.WaitAsync(cancellationToken);
    }

    public async Task<SendSubscribeMessageResult> SendAsync(
        SubscribeMessageCommand command,
        CancellationToken cancellationToken)
    {
        LastTokenCanBeCanceled = cancellationToken.CanBeCanceled;
        Sent.Add(command);
        try
        {
            if (blockSend)
            {
                sendStarted.TrySetResult();
                await releaseSend.Task.WaitAsync(cancellationToken);
            }

            return new SendSubscribeMessageResult(true, 0, "ok", "msg-1");
        }
        catch (OperationCanceledException)
        {
            CancellationObserved = true;
            throw;
        }
        finally
        {
            sendCompleted.TrySetResult();
        }
    }
}
