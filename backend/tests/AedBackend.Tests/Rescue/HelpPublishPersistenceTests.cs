using AedBackend.Api.Endpoints;

namespace AedBackend.Tests.Rescue;

public sealed class HelpPublishPersistenceTests
{
    private const string HelpId = "help-publish";

    [Fact]
    public async Task Execute_RequestCancelledAfterHelpCommit_StillInitializesSession()
    {
        using var requestCancellation = new CancellationTokenSource();
        var lifecycle = new RescueSessionLifecycleCoordinator();
        var helpSaved = false;
        var sessionInitialized = false;
        var sessionToken = new CancellationToken(canceled: true);

        var saved = await HelpPublishPersistence.ExecuteAsync(new HelpPublishPersistenceInput
        {
            HelpId = HelpId,
            Lifecycle = lifecycle,
            TrySaveHelp = token =>
            {
                Assert.Equal(requestCancellation.Token, token);
                helpSaved = true;
                requestCancellation.Cancel();
                return Task.FromResult(true);
            },
            EnsureSession = token =>
            {
                sessionToken = token;
                sessionInitialized = true;
                return Task.CompletedTask;
            },
            RequestCancellationToken = requestCancellation.Token,
        });

        Assert.True(saved);
        Assert.True(helpSaved);
        Assert.True(sessionInitialized);
        Assert.False(sessionToken.CanBeCanceled);
        Assert.Equal(0, lifecycle.GetUsageCount(HelpId));
    }

    [Fact]
    public async Task Execute_HelpCommitFails_DoesNotInitializeSessionAndReleasesLease()
    {
        var lifecycle = new RescueSessionLifecycleCoordinator();
        var sessionInitialized = false;
        var expected = new InvalidOperationException("injected help persistence failure");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HelpPublishPersistence.ExecuteAsync(new HelpPublishPersistenceInput
            {
                HelpId = HelpId,
                Lifecycle = lifecycle,
                TrySaveHelp = _ => Task.FromException<bool>(expected),
                EnsureSession = _ =>
                {
                    sessionInitialized = true;
                    return Task.CompletedTask;
                },
                RequestCancellationToken = CancellationToken.None,
            }));

        Assert.Same(expected, actual);
        Assert.False(sessionInitialized);
        Assert.Equal(0, lifecycle.GetUsageCount(HelpId));
    }
}
