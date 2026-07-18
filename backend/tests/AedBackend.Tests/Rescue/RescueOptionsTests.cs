using AedBackend.Infrastructure.Extensions;
using AedBackend.Infrastructure.Rescue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Rescue;

public sealed class RescueOptionsTests
{
    [Fact]
    public void InvalidTiming_FailsOptionsResolution()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Rescue:SignalLostAfterSeconds"] = "90",
            ["Rescue:LocationGoneAfterSeconds"] = "45",
            ["Rescue:LocationReportIntervalSeconds"] = "0"
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<RescueOptions>>().Value);
    }

    [Fact]
    public void ValidCustomTiming_IsBound()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Rescue:SignalLostAfterSeconds"] = "30",
            ["Rescue:LocationGoneAfterSeconds"] = "60",
            ["Rescue:LocationReportIntervalSeconds"] = "4"
        });

        var options = provider.GetRequiredService<IOptions<RescueOptions>>().Value;

        Assert.Equal(30, options.SignalLostAfterSeconds);
        Assert.Equal(60, options.LocationGoneAfterSeconds);
        Assert.Equal(4, options.LocationReportIntervalSeconds);
    }

    private static ServiceProvider CreateProvider(
        IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWeChatInfrastructure(configuration);
        return services.BuildServiceProvider();
    }
}
