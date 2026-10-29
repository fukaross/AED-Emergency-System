namespace AedBackend.Api;

/**
 * 本地秘密：appsettings.Development.local.json 不被 .NET 默认加载，必须显式加入。
 * 该文件只用于本机随机引导密钥/SessionHashSecret，不进入仓库；
 * 加入后重新追加环境变量与命令行来源，保证环境变量/CLI 优先级仍然最高。
 */
internal static class DevelopmentLocalSecrets
{
    public const string FileName = "appsettings.Development.local.json";

    public static void Apply(WebApplicationBuilder builder, string[] args)
    {
        if (!builder.Environment.IsDevelopment()) return;

        var path = Path.Combine(builder.Environment.ContentRootPath, FileName);
        if (!File.Exists(path)) return;

        builder.Configuration
            .AddJsonFile(path, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddCommandLine(args);
    }
}
