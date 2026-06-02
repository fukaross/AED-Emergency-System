using AedBackend.Infrastructure.Persistence.Sqlite;

namespace AedBackend.Api;

internal static class JsonToSqliteCli
{
    public const string CommandName = "migrate-json-to-sqlite";

    public static async Task<int> TryRunAsync(string[] args, CancellationToken ct)
    {
        if (args.Length == 0 || args[0] != CommandName) return -1;

        try
        {
            var options = Parse(args);
            var report = await new JsonToSqliteMigrator().RunAsync(options.DataRoot, options.DatabasePath, ct);
            Console.WriteLine($"SQLite migration completed: {report.DatabasePath}");
            foreach (var item in report.Counts.OrderBy(item => item.Key))
                Console.WriteLine($"{item.Key}={item.Value}");
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"SQLite migration failed: {ex.Message}");
            return 2;
        }
    }

    private static CliOptions Parse(string[] args)
    {
        var dataRoot = ReadOption(args, "--data-root");
        var db = ReadOption(args, "--db");
        if (string.IsNullOrWhiteSpace(dataRoot) || string.IsNullOrWhiteSpace(db))
            throw new InvalidOperationException("缺少参数: --data-root 和 --db 必填");
        return new CliOptions(dataRoot, db);
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (var i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }

        return null;
    }

    private sealed record CliOptions(string DataRoot, string DatabasePath);
}
