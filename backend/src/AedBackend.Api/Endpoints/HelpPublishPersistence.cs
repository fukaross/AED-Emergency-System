namespace AedBackend.Api.Endpoints;

internal sealed record HelpPublishPersistenceInput
{
    public required string HelpId { get; init; }
    public required RescueSessionLifecycleCoordinator Lifecycle { get; init; }
    public required Func<CancellationToken, Task<bool>> TrySaveHelp { get; init; }
    public required Func<CancellationToken, Task> EnsureSession { get; init; }
    public required CancellationToken RequestCancellationToken { get; init; }
}

/**
 * 将求助与其会话初始化放在同一生命周期租约内；首笔提交后不再受客户端断连中断。
 */
internal static class HelpPublishPersistence
{
    public static async Task<bool> ExecuteAsync(HelpPublishPersistenceInput input)
    {
        using var lease = await input.Lifecycle.EnterAsync(
            input.HelpId,
            input.RequestCancellationToken);
        if (!await input.TrySaveHelp(input.RequestCancellationToken)) return false;
        await input.EnsureSession(CancellationToken.None);
        return true;
    }
}
