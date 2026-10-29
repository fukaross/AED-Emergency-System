using AedBackend.Infrastructure.Demo;

namespace AedBackend.Api;

/**
 * CONTRACT-003 的演示初始化入口：init-demo-data --data-root <绝对目录>。
 * 只初始化数据（SQLite + 上传目录），不启动服务、不发 HTTP、不创建管理员；
 * 失败以非零退出码结束且不留下可被误认为完整就绪的数据库。
 */
internal static class InitDemoDataCli
{
    public const string CommandName = "init-demo-data";

    public static async Task<int> TryRunAsync(string[] args, CancellationToken ct)
    {
        if (args.Length == 0 || args[0] != CommandName) return -1;

        try
        {
            var dataRoot = ParseDataRoot(args);
            var report = await new DemoDataInitializer().RunAsync(
                new DemoInitializationRequest { DataRoot = dataRoot },
                ct);
            PrintReport(report);
            return 0;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"演示数据初始化失败：{exception.Message}");
            return 2;
        }
    }

    private static string ParseDataRoot(string[] args)
    {
        string? dataRoot = null;
        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument == "--data-root")
            {
                if (index + 1 >= args.Length)
                {
                    throw new DemoInitializationException("--data-root 缺少取值");
                }

                if (dataRoot is not null)
                {
                    throw new DemoInitializationException("--data-root 只能出现一次");
                }

                dataRoot = args[++index];
                continue;
            }

            throw new DemoInitializationException(
                $"不支持参数：{argument}。init-demo-data 只接受 --data-root <绝对目录>，"
                + "不提供强制重置、覆盖或删除开关。");
        }

        return string.IsNullOrWhiteSpace(dataRoot)
            ? throw new DemoInitializationException("缺少参数 --data-root <绝对目录>")
            : dataRoot;
    }

    private static void PrintReport(DemoInitializationReport report)
    {
        Console.WriteLine($"演示数据初始化完成：dataVersion={report.DataVersion}");
        Console.WriteLine($"数据目录: {report.DataRoot}");
        Console.WriteLine($"SQLite:   {report.DatabasePath}");
        Console.WriteLine($"上传目录: {report.UploadsPath}");
        Console.WriteLine("计数:");
        foreach (var item in report.Counts)
        {
            Console.WriteLine($"  {item.Key}={item.Value}");
        }

        Console.WriteLine("演示角色对账:");
        foreach (var role in report.Roles)
        {
            Console.WriteLine(
                $"  {role.Role} openId={role.OpenId} campus={role.Campus} accountStatus={role.AccountStatus} "
                + $"responderProfileId={role.ResponderProfileId ?? "-"} certified={role.Certified} "
                + $"schoolBinding={role.SchoolBindingApproved}");
        }

        Console.WriteLine(
            "未自动执行：服务启动、管理员 bootstrap、演示登录。"
            + "后续请自行设置 AED_PUSH_ADMIN_KEY / AED_ADMIN_SESSION_HASH_SECRET（不要写入仓库），"
            + "再按 README 启动后端并调用 POST /api/admin/auth/bootstrap。");
    }
}
