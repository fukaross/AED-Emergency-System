using System.Net;
using AedBackend.Infrastructure.Persistence;
using AedBackend.Infrastructure.Upload;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AedBackend.Infrastructure.Demo;

/**
 * CONTRACT-003 启动门禁：演示能力只在 Development + Demo:Enabled=true + 仅回环监听
 * 同时成立时可用；另外禁止 OSS 上传并要求本机回链，避免演示误触真实外部副作用。
 */
public static class DemoModeGuard
{
    /** Web 主机默认（未声明任何监听配置）只监听 localhost，按回环处理。 */
    private static readonly string[] DefaultListenAddresses = ["http://localhost:5000"];

    public static void ValidateStartup(
        IConfiguration configuration,
        IHostEnvironment environment,
        DemoOptions demo,
        DataOptions data,
        UploadOptions upload,
        WeChatOptions weChat,
        DemoRoleCatalog roles)
    {
        if (!demo.Enabled) return;

        if (!environment.IsDevelopment())
        {
            throw new DemoConfigurationException(
                $"Demo:Enabled=true 只允许在 Development 环境启用；当前环境为 {environment.EnvironmentName}。"
                + "请在真实模式关闭 Demo:Enabled，演示 code 在真实模式下不可用。");
        }

        if (!data.UseSqlite())
        {
            throw new DemoConfigurationException(
                $"Demo:Enabled=true 要求 Data:Provider=Sqlite（当前为 {data.Provider}）。"
                + "演示身份/学校绑定由 init-demo-data 写入 SQLite，JSON provider 不提供等价能力。");
        }

        if (upload.UseOss())
        {
            throw new DemoConfigurationException(
                "Demo:Enabled=true 禁止 OSS 上传：演示模式只允许本地上传（Upload:Provider=Local）。");
        }

        if (string.IsNullOrWhiteSpace(weChat.PublicBaseUrl))
        {
            throw new DemoConfigurationException(
                "Demo:Enabled=true 需要 WeChat:PublicBaseUrl 指回本机（例如 http://127.0.0.1:5150），"
                + "否则本地图片/视频回链无法被详情与报告校验接受。");
        }

        var addresses = ResolveListenAddresses(configuration);
        var invalid = addresses.Where(address => !IsLoopbackEndpoint(address)).ToArray();
        if (invalid.Length > 0)
        {
            throw new DemoConfigurationException(
                "Demo:Enabled=true 只允许监听本机回环地址；当前监听配置包含非回环地址："
                + string.Join(", ", invalid)
                + "。演示身份不得暴露到局域网或公网。");
        }

        roles.EnsureLoaded();
    }

    /**
     * 解析 Kestrel 的有效监听配置：显式端点默认优先于 urls；PreferHostingUrls=true 时
     * urls 优先；再回退到 HTTP_PORTS / HTTPS_PORTS，最后使用 localhost 默认地址。
     */
    public static IReadOnlyList<string> ResolveListenAddresses(IConfiguration configuration)
    {
        var endpoints = configuration.GetSection("Kestrel:Endpoints").GetChildren()
            .Select(endpoint => endpoint["Url"])
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .ToArray();
        var urls = configuration["urls"];
        var declaredUrls = string.IsNullOrWhiteSpace(urls)
            ? []
            : urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (configuration.GetValue<bool>("PreferHostingUrls") && declaredUrls.Length > 0)
        {
            return declaredUrls;
        }

        if (endpoints.Length > 0) return endpoints;
        if (declaredUrls.Length > 0) return declaredUrls;

        var addresses = new List<string>();
        AddWildcardPortAddresses(configuration, "http_ports", "http", addresses);
        AddWildcardPortAddresses(configuration, "https_ports", "https", addresses);

        return addresses.Count > 0 ? addresses : DefaultListenAddresses;
    }

    private static void AddWildcardPortAddresses(
        IConfiguration configuration,
        string configurationKey,
        string scheme,
        ICollection<string> addresses)
    {
        var ports = configuration[configurationKey];
        if (string.IsNullOrWhiteSpace(ports)) return;

        foreach (var port in ports.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            addresses.Add($"{scheme}://*:{port}");
        }
    }

    public static bool IsLoopbackEndpoint(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(uri.DnsSafeHost, out var parsed) && IPAddress.IsLoopback(parsed);
    }
}
