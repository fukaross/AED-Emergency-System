using AedBackend.Infrastructure.Demo;
using AedBackend.Infrastructure.Persistence;
using AedBackend.Infrastructure.Upload;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AedBackend.Tests.Demo;

/**
 * CONTRACT-003 启动门禁：演示能力仅在 Development + Demo:Enabled=true + 仅回环监听
 * （且 SQLite、本地上传、本机回链）同时成立时可用；其余组合必须明确失败。
 */
public sealed class DemoModeGuardTests
{
    [Fact]
    public void ValidateStartup_DemoDisabled_IgnoresOtherRiskySettings()
    {
        var environment = new TestEnvironment { EnvironmentName = "Production" };
        var configuration = Config(("urls", "http://0.0.0.0:5150"));

        DemoModeGuard.ValidateStartup(
            configuration,
            environment,
            new DemoOptions { Enabled = false },
            new DataOptions { Provider = DataOptions.JsonProvider },
            new UploadOptions { Provider = UploadOptions.OssProvider },
            new WeChatOptions { PublicBaseUrl = "" },
            new DemoRoleCatalog());
    }

    [Fact]
    public void ValidateStartup_DemoEnabledOutsideDevelopment_Throws()
    {
        var exception = Assert.Throws<DemoConfigurationException>(() => DemoModeGuard.ValidateStartup(
            Config(("urls", "http://127.0.0.1:5150")),
            new TestEnvironment { EnvironmentName = "Production" },
            new DemoOptions { Enabled = true },
            new DataOptions { Provider = DataOptions.SqliteProvider },
            new UploadOptions { Provider = UploadOptions.LocalProvider },
            new WeChatOptions { PublicBaseUrl = "http://127.0.0.1:5150" },
            new DemoRoleCatalog()));

        Assert.Contains("Development", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://0.0.0.0:5150")]
    [InlineData("http://192.168.1.10:5150")]
    [InlineData("http://*:5150")]
    [InlineData("http://localhost:5150;http://0.0.0.0:5150")]
    public void ValidateStartup_DemoEnabledWithNonLoopbackAddress_Throws(string urls)
    {
        var exception = Assert.Throws<DemoConfigurationException>(() => DemoModeGuard.ValidateStartup(
            Config(("urls", urls)),
            Development(),
            new DemoOptions { Enabled = true },
            new DataOptions { Provider = DataOptions.SqliteProvider },
            new UploadOptions { Provider = UploadOptions.LocalProvider },
            new WeChatOptions { PublicBaseUrl = "http://127.0.0.1:5150" },
            new DemoRoleCatalog()));

        Assert.Contains("回环", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://127.0.0.1:5150")]
    [InlineData("http://localhost:5150")]
    [InlineData("http://localhost:5150;https://localhost:7118")]
    [InlineData("http://[::1]:5150")]
    public void ValidateStartup_DemoEnabledWithLoopbackOnly_Succeeds(string urls)
    {
        DemoModeGuard.ValidateStartup(
            Config(("urls", urls)),
            Development(),
            new DemoOptions { Enabled = true },
            new DataOptions { Provider = DataOptions.SqliteProvider },
            new UploadOptions { Provider = UploadOptions.LocalProvider },
            new WeChatOptions { PublicBaseUrl = "http://127.0.0.1:5150" },
            new DemoRoleCatalog());
    }

    [Fact]
    public void ValidateStartup_DemoEnabledWithKestrelNonLoopbackEndpoint_Throws()
    {
        var configuration = Config(("Kestrel:Endpoints:Http:Url", "http://0.0.0.0:5000"));

        var exception = Assert.Throws<DemoConfigurationException>(() => DemoModeGuard.ValidateStartup(
            configuration,
            Development(),
            new DemoOptions { Enabled = true },
            new DataOptions { Provider = DataOptions.SqliteProvider },
            new UploadOptions { Provider = UploadOptions.LocalProvider },
            new WeChatOptions { PublicBaseUrl = "http://127.0.0.1:5150" },
            new DemoRoleCatalog()));

        Assert.Contains("0.0.0.0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateStartup_DemoEnabledWithLoopbackUrlsAndNonLoopbackKestrelEndpoint_Throws()
    {
        var configuration = Config(
            ("urls", "http://127.0.0.1:5150"),
            ("Kestrel:Endpoints:Http:Url", "http://0.0.0.0:5000"));

        var exception = Assert.Throws<DemoConfigurationException>(() => DemoModeGuard.ValidateStartup(
            configuration,
            Development(),
            new DemoOptions { Enabled = true },
            new DataOptions { Provider = DataOptions.SqliteProvider },
            new UploadOptions { Provider = UploadOptions.LocalProvider },
            new WeChatOptions { PublicBaseUrl = "http://127.0.0.1:5150" },
            new DemoRoleCatalog()));

        Assert.Contains("0.0.0.0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateStartup_DemoEnabledWithJsonProvider_Throws()
    {
        var exception = Assert.Throws<DemoConfigurationException>(() => DemoModeGuard.ValidateStartup(
            Config(("urls", "http://127.0.0.1:5150")),
            Development(),
            new DemoOptions { Enabled = true },
            new DataOptions { Provider = DataOptions.JsonProvider },
            new UploadOptions { Provider = UploadOptions.LocalProvider },
            new WeChatOptions { PublicBaseUrl = "http://127.0.0.1:5150" },
            new DemoRoleCatalog()));

        Assert.Contains("Sqlite", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateStartup_DemoEnabledWithOssUpload_Throws()
    {
        var exception = Assert.Throws<DemoConfigurationException>(() => DemoModeGuard.ValidateStartup(
            Config(("urls", "http://127.0.0.1:5150")),
            Development(),
            new DemoOptions { Enabled = true },
            new DataOptions { Provider = DataOptions.SqliteProvider },
            new UploadOptions { Provider = UploadOptions.OssProvider },
            new WeChatOptions { PublicBaseUrl = "http://127.0.0.1:5150" },
            new DemoRoleCatalog()));

        Assert.Contains("OSS", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateStartup_DemoEnabledWithoutPublicBaseUrl_Throws()
    {
        var exception = Assert.Throws<DemoConfigurationException>(() => DemoModeGuard.ValidateStartup(
            Config(("urls", "http://127.0.0.1:5150")),
            Development(),
            new DemoOptions { Enabled = true },
            new DataOptions { Provider = DataOptions.SqliteProvider },
            new UploadOptions { Provider = UploadOptions.LocalProvider },
            new WeChatOptions { PublicBaseUrl = "" },
            new DemoRoleCatalog()));

        Assert.Contains("PublicBaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateStartup_DemoEnabledWithoutRolesArtifact_Throws()
    {
        var missingRoles = new DemoRoleCatalog(
            Path.Combine(Path.GetTempPath(), $"missing-roles-{Guid.NewGuid():N}", "demo-roles.json"));

        var exception = Assert.Throws<DemoConfigurationException>(() => DemoModeGuard.ValidateStartup(
            Config(("urls", "http://127.0.0.1:5150")),
            Development(),
            new DemoOptions { Enabled = true },
            new DataOptions { Provider = DataOptions.SqliteProvider },
            new UploadOptions { Provider = UploadOptions.LocalProvider },
            new WeChatOptions { PublicBaseUrl = "http://127.0.0.1:5150" },
            missingRoles));

        Assert.Contains("演示角色注册表", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveListenAddresses_DefaultsToLoopbackWhenNoAddressDeclared()
    {
        var addresses = DemoModeGuard.ResolveListenAddresses(Config());

        Assert.NotEmpty(addresses);
        Assert.All(addresses, address => Assert.True(DemoModeGuard.IsLoopbackEndpoint(address)));
    }

    [Fact]
    public void ResolveListenAddresses_PrefersKestrelEndpointsOverUrls()
    {
        var configuration = Config(
            ("urls", "http://127.0.0.1:5150"),
            ("Kestrel:Endpoints:Http:Url", "http://0.0.0.0:5000"));

        var addresses = DemoModeGuard.ResolveListenAddresses(configuration);

        Assert.Equal(["http://0.0.0.0:5000"], addresses);
    }

    [Fact]
    public void ResolveListenAddresses_PrefersUrlsOverHttpPorts()
    {
        var addresses = DemoModeGuard.ResolveListenAddresses(Config(
            ("urls", "http://127.0.0.1:5150"),
            ("http_ports", "5000")));

        Assert.Equal(["http://127.0.0.1:5150"], addresses);
    }

    [Fact]
    public void ResolveListenAddresses_PrefersUrlsWhenKestrelRequestsIt()
    {
        var addresses = DemoModeGuard.ResolveListenAddresses(Config(
            ("urls", "http://127.0.0.1:5150"),
            ("Kestrel:Endpoints:Http:Url", "http://0.0.0.0:5000"),
            ("PreferHostingUrls", "true")));

        Assert.Equal(["http://127.0.0.1:5150"], addresses);
    }

    [Theory]
    [InlineData("http_ports", "5150", "http://*:5150")]
    [InlineData("https_ports", "7118", "https://*:7118")]
    public void ResolveListenAddresses_CollectsWildcardPortBindings(
        string configurationKey,
        string port,
        string expectedAddress)
    {
        var addresses = DemoModeGuard.ResolveListenAddresses(Config((configurationKey, port)));

        Assert.Contains(expectedAddress, addresses);
        Assert.False(DemoModeGuard.IsLoopbackEndpoint(expectedAddress));
    }

    [Theory]
    [InlineData("http://localhost:5150", true)]
    [InlineData("http://127.0.0.1:5150", true)]
    [InlineData("https://[::1]:7118", true)]
    [InlineData("http://0.0.0.0:5150", false)]
    [InlineData("http://*:5150", false)]
    [InlineData("http://example.test:5150", false)]
    [InlineData("not-a-url", false)]
    public void IsLoopbackEndpoint_ClassifiesAddresses(string address, bool expected)
    {
        Assert.Equal(expected, DemoModeGuard.IsLoopbackEndpoint(address));
    }

    private static TestEnvironment Development()
    {
        return new TestEnvironment { EnvironmentName = "Development" };
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(item =>
                new KeyValuePair<string, string?>(item.Key, item.Value)))
            .Build();
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "AedBackend.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
