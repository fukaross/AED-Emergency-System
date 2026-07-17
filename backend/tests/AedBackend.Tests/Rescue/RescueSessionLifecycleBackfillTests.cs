using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Admin;
using AedBackend.Tests.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Tests.Rescue;

public sealed class RescueSessionLifecycleBackfillTests
{
    private static readonly DateTimeOffset ClosedAt =
        new(2026, 7, 17, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task JsonStartup_BackfillsClosedFenceAndClearsLegacyLocation()
    {
        using var temp = new ProviderTempData();
        await temp.WriteMinimalJsonAsync();
        await temp.WriteAsync("help-requests.json", new { requests = new[] { ClosedHelp() } });
        await temp.WriteAsync("rescue-sessions.json", new { sessions = new[] { LegacySession() } });

        using var app = new AdminTestApplication(temp.Root);
        using var client = app.CreateClient();
        Assert.True((await client.GetAsync("/health")).IsSuccessStatusCode);
        var store = app.Services.GetRequiredService<IRescueSessionStore>();

        var session = await store.FindByHelpIdAsync("help-closed", CancellationToken.None);
        Assert.NotNull(session);
        Assert.NotNull(session.ClosedAt);
        Assert.Null(session.Participants[0].LocationReportedAt);
        await Assert.ThrowsAsync<RescueSessionClosedException>(() => store.JoinAsync(
            new("help-closed", "openid-late", "迟到急救员", ClosedAt), CancellationToken.None));
    }

    [Fact]
    public async Task SqliteUpgrade_RejectsLegacyOrphanSession()
    {
        using var temp = new ProviderTempData();
        await temp.WriteMinimalJsonAsync();
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
        await InsertOrphanSessionAsync(temp.DbPath);

        using var app = new AdminTestApplication(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });

        var error = Assert.Throws<InvalidOperationException>(() => app.CreateClient());
        Assert.Contains("缺少关联求助", error.ToString());
    }

    [Fact]
    public async Task SqliteUpgrade_BackfillsClosedFenceAndClearsLegacyLocation()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await temp.WriteRescueSessionJsonAsync();
        await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
        await SimulateLegacyClosedSessionAsync(temp.DbPath);

        using var app = new AdminTestApplication(temp.Root, new Dictionary<string, string?>
        {
            ["Data:Provider"] = "Sqlite",
            ["Data:SqlitePath"] = temp.DbPath
        });
        using var client = app.CreateClient();
        Assert.True((await client.GetAsync("/health")).IsSuccessStatusCode);
        var store = app.Services.GetRequiredService<IRescueSessionStore>();

        var session = await store.FindByHelpIdAsync("help-01", CancellationToken.None);
        Assert.NotNull(session);
        Assert.NotNull(session.ClosedAt);
        Assert.Null(session.Participants[0].LocationReportedAt);
        await Assert.ThrowsAsync<DbUpdateException>(() => store.JoinAsync(
            new("missing-help", "openid-late", "迟到急救员", ClosedAt), CancellationToken.None));
    }

    private static HelpRequestRecord ClosedHelp() => new()
    {
        Id = "help-closed",
        RequestNo = "HELP-CLOSED",
        UserId = "openid-requester",
        Address = "校门",
        Content = "求助",
        Status = HelpStatuses.Closed,
        CreatedAt = ClosedAt.AddMinutes(-5),
        ClosedAt = ClosedAt,
        RequesterPhone = "REDACTED"
    };

    private static RescueSessionRecord LegacySession() => new()
    {
        HelpRequestId = "help-closed",
        CreatedAt = ClosedAt.AddMinutes(-5),
        Participants =
        [
            new RescueSessionParticipantRecord
            {
                OpenId = "openid-responder",
                DisplayName = "急救员甲",
                JoinedAt = ClosedAt.AddMinutes(-4),
                LastSeenAt = ClosedAt.AddMinutes(-1),
                LastLat = 20,
                LastLng = 110,
                LocationReportedAt = ClosedAt.AddMinutes(-1)
            }
        ]
    };

    private static async Task SimulateLegacyClosedSessionAsync(string dbPath)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={Path.GetFullPath(dbPath)};Foreign Keys=True;Pooling=False");
        await connection.OpenAsync();
        await using var closeHelp = connection.CreateCommand();
        closeHelp.CommandText = "UPDATE help_requests SET Status='closed', ClosedAt=$closedAt WHERE Id='help-01';";
        closeHelp.Parameters.AddWithValue("$closedAt", ClosedAt.ToString("O"));
        await closeHelp.ExecuteNonQueryAsync();

        await using var dropColumn = connection.CreateCommand();
        dropColumn.CommandText = "ALTER TABLE rescue_sessions DROP COLUMN ClosedAt;";
        await dropColumn.ExecuteNonQueryAsync();
    }

    private static async Task InsertOrphanSessionAsync(string dbPath)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={Path.GetFullPath(dbPath)};Foreign Keys=True;Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO rescue_sessions (HelpRequestId, CreatedAt, ClosedAt)
            VALUES ('missing-help', '2026-07-17T12:00:00+00:00', NULL);
            """;
        await command.ExecuteNonQueryAsync();
    }
}
