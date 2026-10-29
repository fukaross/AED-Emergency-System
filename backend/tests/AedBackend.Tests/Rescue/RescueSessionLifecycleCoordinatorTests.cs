using AedBackend.Api.Endpoints;

namespace AedBackend.Tests.Rescue;

public sealed class RescueSessionLifecycleCoordinatorTests
{
    [Fact]
    public async Task EnterAsync_SerializesSameHelpAndReclaimsEntry()
    {
        var coordinator = new RescueSessionLifecycleCoordinator();
        var first = await coordinator.EnterAsync("help-1", CancellationToken.None);
        var secondTask = coordinator.EnterAsync("help-1", CancellationToken.None);
        await WaitForUsageAsync(coordinator, "help-1", 2);

        Assert.False(secondTask.IsCompleted);
        first.Dispose();
        var second = await secondTask;
        Assert.Equal(1, coordinator.GetUsageCount("help-1"));

        second.Dispose();
        Assert.Equal(0, coordinator.GetUsageCount("help-1"));
    }

    [Fact]
    public async Task EnterAsync_AllowsDifferentHelpIdsConcurrently()
    {
        var coordinator = new RescueSessionLifecycleCoordinator();
        using var first = await coordinator.EnterAsync("help-1", CancellationToken.None);
        using var second = await coordinator.EnterAsync("help-2", CancellationToken.None);

        Assert.Equal(1, coordinator.GetUsageCount("help-1"));
        Assert.Equal(1, coordinator.GetUsageCount("help-2"));
    }

    private static async Task WaitForUsageAsync(
        RescueSessionLifecycleCoordinator coordinator,
        string helpId,
        int expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (coordinator.GetUsageCount(helpId) != expected)
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
    }
}
