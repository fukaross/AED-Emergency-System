using System.Data;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal static class SqliteRescueSchemaUpgrade
{
    public static async Task EnsureAsync(AedDbContext db, CancellationToken ct)
    {
        await CreateTablesAsync(db, ct);
        if (!await SqliteSchemaUpgrade.ColumnExistsAsync(db, "rescue_sessions", "ClosedAt", ct))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE rescue_sessions ADD COLUMN ClosedAt TEXT NULL;",
                ct);
        await CreateIndexesAndGuardAsync(db, ct);
        await BackfillClosedSessionsAsync(db, ct);
        await EnsureIntegrityAsync(db, ct);
    }

    private static async Task CreateTablesAsync(AedDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS rescue_sessions (
                HelpRequestId TEXT NOT NULL CONSTRAINT pk_rescue_sessions PRIMARY KEY,
                CreatedAt TEXT NOT NULL,
                ClosedAt TEXT NULL
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS rescue_session_participants (
                Id INTEGER NOT NULL CONSTRAINT pk_rescue_session_participants PRIMARY KEY AUTOINCREMENT,
                HelpRequestId TEXT NOT NULL,
                OpenId TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                JoinedAt TEXT NOT NULL,
                LastSeenAt TEXT NOT NULL,
                LastLat REAL NULL,
                LastLng REAL NULL,
                LocationReportedAt TEXT NULL,
                LastReadSeq INTEGER NOT NULL DEFAULT 0,
                CONSTRAINT fk_rescue_participant_session
                    FOREIGN KEY (HelpRequestId) REFERENCES rescue_sessions(HelpRequestId) ON DELETE CASCADE
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS rescue_session_messages (
                Id TEXT NOT NULL CONSTRAINT pk_rescue_session_messages PRIMARY KEY,
                HelpRequestId TEXT NOT NULL,
                Seq INTEGER NOT NULL,
                Kind TEXT NOT NULL,
                SenderOpenId TEXT NOT NULL,
                SenderName TEXT NOT NULL,
                Text TEXT NULL,
                EventType TEXT NULL,
                Action TEXT NULL,
                CreatedAt TEXT NOT NULL,
                CONSTRAINT fk_rescue_message_session
                    FOREIGN KEY (HelpRequestId) REFERENCES rescue_sessions(HelpRequestId) ON DELETE CASCADE
            );
            """, ct);
    }

    private static async Task CreateIndexesAndGuardAsync(AedDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS uk_rescue_participant ON rescue_session_participants(HelpRequestId, OpenId);",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS uk_rescue_message_seq ON rescue_session_messages(HelpRequestId, Seq);",
            ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER IF NOT EXISTS trg_rescue_session_help_insert
            BEFORE INSERT ON rescue_sessions
            FOR EACH ROW
            WHEN NOT EXISTS (SELECT 1 FROM help_requests WHERE Id = NEW.HelpRequestId)
            BEGIN
                SELECT RAISE(ABORT, 'rescue session help request not found');
            END;
            """, ct);
    }

    private static async Task BackfillClosedSessionsAsync(AedDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("""
            INSERT OR IGNORE INTO rescue_sessions (HelpRequestId, CreatedAt, ClosedAt)
            SELECT Id, CreatedAt, COALESCE(ClosedAt, CreatedAt) FROM help_requests
            WHERE Status IN ('closed', 'false_alarm');
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE rescue_sessions
            SET ClosedAt = COALESCE(
                (SELECT ClosedAt FROM help_requests WHERE Id = rescue_sessions.HelpRequestId),
                (SELECT CreatedAt FROM help_requests WHERE Id = rescue_sessions.HelpRequestId))
            WHERE ClosedAt IS NULL AND HelpRequestId IN (
                SELECT Id FROM help_requests WHERE Status IN ('closed', 'false_alarm'));
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE rescue_session_participants
            SET LastLat = NULL, LastLng = NULL, LocationReportedAt = NULL
            WHERE HelpRequestId IN (
                SELECT Id FROM help_requests WHERE Status IN ('closed', 'false_alarm'));
            """, ct);
    }

    private static async Task EnsureIntegrityAsync(AedDbContext db, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var closeAfterRead = connection.State == ConnectionState.Closed;
        if (closeAfterRead) await connection.OpenAsync(ct);
        try
        {
            await using var orphanCommand = connection.CreateCommand();
            orphanCommand.CommandText = """
                SELECT rs.HelpRequestId FROM rescue_sessions rs
                LEFT JOIN help_requests h ON h.Id = rs.HelpRequestId
                WHERE h.Id IS NULL LIMIT 1;
                """;
            var orphan = await orphanCommand.ExecuteScalarAsync(ct);
            if (orphan is not null)
                throw new InvalidOperationException($"急救会话缺少关联求助: {orphan}");

            await using var foreignKeyCommand = connection.CreateCommand();
            foreignKeyCommand.CommandText = "PRAGMA foreign_key_check;";
            await using var reader = await foreignKeyCommand.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
                throw new InvalidOperationException($"SQLite 外键校验失败: {reader.GetString(0)}");
        }
        finally
        {
            if (closeAfterRead) await connection.CloseAsync();
        }
    }
}
