using System.Net;
using System.Net.Http.Json;
using AedBackend.Api.Endpoints;
using AedBackend.Core.Constants;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Tests.Admin;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Tests.Rescue;

public sealed class RescueSessionLifecycleApiTests : AdminApiTestBase
{
    private const string ResponderToken = "token-responder";

    [Fact]
    public async Task MessageQueuedBeforeClose_RechecksHelpInsideLifecycleFence()
    {
        SeedCast();
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderToken);
        await Client.PostAsync("/api/help/help-1/session/join", null);
        var coordinator = Services.GetRequiredService<RescueSessionLifecycleCoordinator>();
        var helpStore = Services.GetRequiredService<IHelpRequestStore>();
        var sessionStore = Services.GetRequiredService<IRescueSessionStore>();

        var lease = await coordinator.EnterAsync("help-1", CancellationToken.None);
        var pending = Client.PostAsJsonAsync(
            "/api/help/help-1/session/messages",
            new { text = "不应写入" });
        await WaitForUsageAsync(coordinator, 2);
        await helpStore.CloseAsync(
            "help-1", HelpStatuses.Closed, null, DateTimeOffset.UtcNow, CancellationToken.None);
        lease.Dispose();

        var response = await pending;
        await AssertErrorAsync(response, HttpStatusCode.Conflict, ErrorCodes.RescueSessionClosed);
        var session = await sessionStore.FindByHelpIdAsync("help-1", CancellationToken.None);
        Assert.DoesNotContain(session!.Messages, message => message.Text == "不应写入");
    }

    private void SeedCast()
    {
        SeedLoggedInExperienceUsers(null, ("openid-1", "token-requester"), ("openid-2", ResponderToken));
        SeedBoundResponders(("rescuer-1", "openid-2", "active", "张急救"));
    }

    private static async Task WaitForUsageAsync(
        RescueSessionLifecycleCoordinator coordinator,
        int expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (coordinator.GetUsageCount("help-1") != expected)
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
    }
}
