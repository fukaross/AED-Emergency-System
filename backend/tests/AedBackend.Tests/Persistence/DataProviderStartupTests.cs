using AedBackend.Tests.Admin;
using AedBackend.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using System.Net;

namespace AedBackend.Tests.Persistence;

public sealed class DataProviderStartupTests
{
    [Fact]
    public async Task SqliteProvider_StartsWhenMigratedDatabaseExists()
    {
        using var temp = new ProviderTempData();
        await temp.WriteMinimalJsonAsync();
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);

        using var app = new AdminTestApplication(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task SqliteProvider_UpgradesPhase7SchemaWithoutDroppingExistingData()
    {
        using var temp = new ProviderTempData();
        await temp.WriteMinimalJsonAsync();
        await temp.WriteUserJsonAsync("openid-old", "REDACTED", "old-token");
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
        await DropPhase7ColumnsAndTableAsync(temp.DbPath);

        using var app = new AdminTestApplication(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });
        using var client = app.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertColumnExistsAsync(temp.DbPath, "experience_users", "Name");
        await AssertColumnExistsAsync(temp.DbPath, "experience_users", "AvatarUrl");
        await AssertColumnExistsAsync(temp.DbPath, "experience_users", "Gender");
        await AssertColumnExistsAsync(temp.DbPath, "responder_profiles", "BoundOpenId");
        await AssertColumnExistsAsync(temp.DbPath, "responder_profiles", "Source");
        await AssertColumnExistsAsync(temp.DbPath, "responder_profiles", "SourceApplicationId");
        await AssertTableExistsAsync(temp.DbPath, "volunteer_applications");
        Assert.Equal("REDACTED", await ReadPhoneAsync(temp.DbPath, "openid-old"));
    }

    [Fact]
    public void SqliteProvider_FailsWhenDatabaseIsMissing()
    {
        using var temp = new ProviderTempData();
        using var app = new AdminTestApplication(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });

        Assert.Throws<InvalidOperationException>(() => app.CreateClient());
    }

    [Fact]
    public async Task SqliteProvider_AdminDashboardRejectsInvalidSessionWithoutServerError()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);

        using var app = new AdminTestApplication(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Aed-Admin-Key", "invalid-admin-session");

        var response = await client.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SqliteProvider_AdminDashboardLoadsWithValidSession()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);

        using var app = new AdminTestApplication(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Aed-Admin-Key", ProviderTempData.AdminSessionKey);

        var response = await client.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task DropPhase7ColumnsAndTableAsync(string dbPath)
    {
        await using var connection = CreateConnection(dbPath);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "DROP TABLE volunteer_applications;");
        await ExecuteAsync(connection, "DROP INDEX IF EXISTS IX_responder_profiles_BoundOpenId;");
        await ExecuteAsync(connection, "DROP INDEX IF EXISTS ix_responder_profiles_bound_open_id;");
        await ExecuteAsync(connection, "ALTER TABLE experience_users DROP COLUMN Name;");
        await ExecuteAsync(connection, "ALTER TABLE experience_users DROP COLUMN AvatarUrl;");
        await ExecuteAsync(connection, "ALTER TABLE experience_users DROP COLUMN Gender;");
        await ExecuteAsync(connection, "ALTER TABLE responder_profiles DROP COLUMN BoundOpenId;");
        await ExecuteAsync(connection, "ALTER TABLE responder_profiles DROP COLUMN Source;");
        await ExecuteAsync(connection, "ALTER TABLE responder_profiles DROP COLUMN SourceApplicationId;");
    }

    private static async Task AssertColumnExistsAsync(string dbPath, string table, string column)
    {
        await using var connection = CreateConnection(dbPath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase)) return;
        }
        throw new InvalidOperationException($"Column {table}.{column} was not found.");
    }

    private static async Task AssertTableExistsAsync(string dbPath, string table)
    {
        await using var connection = CreateConnection(dbPath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$table;";
        command.Parameters.AddWithValue("$table", table);
        var actual = await command.ExecuteScalarAsync();
        Assert.Equal(table, actual);
    }

    private static async Task<string?> ReadPhoneAsync(string dbPath, string openId)
    {
        await using var connection = CreateConnection(dbPath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Phone FROM experience_users WHERE OpenId=$openId;";
        command.Parameters.AddWithValue("$openId", openId);
        return (string?)await command.ExecuteScalarAsync();
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static SqliteConnection CreateConnection(string dbPath)
    {
        return new SqliteConnection($"Data Source={Path.GetFullPath(dbPath)};Foreign Keys=True;Pooling=False");
    }
}
