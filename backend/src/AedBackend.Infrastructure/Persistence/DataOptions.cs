using Microsoft.Extensions.Configuration;

namespace AedBackend.Infrastructure.Persistence;

public sealed class DataOptions
{
    public const string SectionName = "Data";
    public const string JsonProvider = "Json";
    public const string SqliteProvider = "Sqlite";

    public string Provider { get; set; } = JsonProvider;
    public string SqlitePath { get; set; } = "data/aed.db";

    public void ApplyEnvironmentAliases(IConfiguration configuration)
    {
        Provider = FirstNonEmpty(configuration["AED_DATA_PROVIDER"], Provider);
        SqlitePath = FirstNonEmpty(configuration["AED_SQLITE_PATH"], SqlitePath);
    }

    public bool UseSqlite()
    {
        return Provider.Equals(SqliteProvider, StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(string? first, string second)
    {
        return string.IsNullOrWhiteSpace(first) ? second : first;
    }
}
