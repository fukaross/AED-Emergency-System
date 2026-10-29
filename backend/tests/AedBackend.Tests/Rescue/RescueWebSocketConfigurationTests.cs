using AedBackend.Api.WebSockets;

namespace AedBackend.Tests.Rescue;

public sealed class RescueWebSocketConfigurationTests
{
    [Fact]
    public void RuntimeOptions_UseExplicitThirtySecondKeepAlive()
    {
        var options = RescueWebSocketRuntimeOptions.Create();

        Assert.Equal(TimeSpan.FromSeconds(30), options.KeepAliveInterval);
    }
}
