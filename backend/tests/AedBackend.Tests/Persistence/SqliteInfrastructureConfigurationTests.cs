using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Admin;
using Microsoft.Data.Sqlite;

namespace AedBackend.Tests.Persistence;

public sealed class SqliteInfrastructureConfigurationTests
{
    [Fact]
    public async Task BuildConnectionString_AppliesDefaultTimeoutToEveryOpenedConnection()
    {
        using var temp = new ProviderTempData();
        var connectionString = SqliteStoreHelpers.BuildConnectionString(temp.DbPath);
        var builder = new SqliteConnectionStringBuilder(connectionString);

        Assert.Equal(SqliteStoreHelpers.BusyTimeoutSeconds, builder.DefaultTimeout);
        await using var first = new SqliteConnection(connectionString);
        await using var second = new SqliteConnection(connectionString);
        await first.OpenAsync();
        await second.OpenAsync();

        await using var firstCommand = first.CreateCommand();
        await using var secondCommand = second.CreateCommand();
        Assert.Equal(SqliteStoreHelpers.BusyTimeoutSeconds, firstCommand.CommandTimeout);
        Assert.Equal(SqliteStoreHelpers.BusyTimeoutSeconds, secondCommand.CommandTimeout);
    }

    [Fact]
    public async Task Migrator_CreatesCanonicalHelpAndRescueUniqueIndexes()
    {
        using var temp = new ProviderTempData();
        await temp.WriteMinimalJsonAsync();
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);

        var names = await ReadCanonicalIndexNamesAsync(temp.DbPath);

        Assert.Contains("uk_help_active_user", names);
        Assert.Contains("uk_rescue_participant", names);
        Assert.Contains("uk_rescue_message_seq", names);
        Assert.DoesNotContain("IX_rescue_session_participants_HelpRequestId_OpenId", names);
        Assert.DoesNotContain("IX_rescue_session_messages_HelpRequestId_Seq", names);
    }

    [Fact]
    public async Task SqliteStartup_RestoresActiveHelpUniqueIndexForExistingDatabase()
    {
        using var temp = new ProviderTempData();
        await temp.WriteMinimalJsonAsync();
        await new JsonToSqliteMigrator().RunAsync(
            temp.Root,
            temp.DbPath,
            CancellationToken.None);
        await DropActiveHelpIndexAsync(temp.DbPath);

        using var app = new AdminTestApplication(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });
        using var client = app.CreateClient();
        Assert.True((await client.GetAsync("/health")).IsSuccessStatusCode);

        var names = await ReadCanonicalIndexNamesAsync(temp.DbPath);
        Assert.Contains("uk_help_active_user", names);
    }

    [Fact]
    public async Task SqliteStartup_DuplicateActiveHelps_FailsWithActionableOperationsGuidance()
    {
        using var temp = new ProviderTempData();
        await temp.WriteMinimalJsonAsync();
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
        await DropActiveHelpIndexAsync(temp.DbPath);
        await InsertDuplicateActiveHelpsAsync(temp.DbPath);

        using var app = new AdminTestApplication(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });

        var exception = Assert.ThrowsAny<Exception>(() => app.CreateClient());
        var startupFailure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains(Path.GetFullPath(temp.DbPath), startupFailure.Message);
        Assert.Contains("备份", startupFailure.Message);
        Assert.Contains("只读 SQL", startupFailure.Message);
        Assert.Contains("GROUP BY UserId", startupFailure.Message);
        Assert.Contains("不会自动删除或合并", startupFailure.Message);
        Assert.IsType<SqliteException>(startupFailure.InnerException);
        Assert.Equal(2, await ReadActiveHelpCountAsync(temp.DbPath, "duplicate-user"));
        Assert.DoesNotContain("uk_help_active_user", await ReadCanonicalIndexNamesAsync(temp.DbPath));
    }

    private static async Task<IReadOnlyCollection<string>> ReadCanonicalIndexNamesAsync(string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type='index'
              AND tbl_name IN (
                'help_requests',
                'rescue_session_participants',
                'rescue_session_messages');
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        return names;
    }

    private static async Task DropActiveHelpIndexAsync(string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP INDEX uk_help_active_user;";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertDuplicateActiveHelpsAsync(string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO help_requests (
                Id, RequestNo, UserId, RequestChannel, Lat, Lng, Address,
                Content, Type, Status, CreatedAt, RequesterPhone)
            VALUES
                ('help-01', 'HELP-01', 'duplicate-user', 'miniapp', 1, 2,
                 'addr', 'content', 'medical', 'published', '2026-07-18T00:00:00Z', 'REDACTED'),
                ('help-02', 'HELP-02', 'duplicate-user', 'miniapp', 1, 2,
                 'addr', 'content', 'medical', 'responding', '2026-07-18T00:00:00Z', 'REDACTED');
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ReadActiveHelpCountAsync(string dbPath, string userId)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM help_requests
            WHERE UserId = $userId
              AND Status IN ('published', 'responding', 'pending_review');
            """;
        command.Parameters.AddWithValue("$userId", userId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
