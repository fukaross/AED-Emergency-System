using AedBackend.Core.Models.Help;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AedBackend.Infrastructure.Help;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal static class SqliteSchemaUpgrade
{
    private const string ActiveHelpConflictReadOnlySql = """
        SELECT UserId, COUNT(*) AS ActiveCount, GROUP_CONCAT(Id) AS HelpIds
        FROM help_requests
        WHERE UserId IS NOT NULL
          AND Status IN ('published', 'responding', 'pending_review')
        GROUP BY UserId
        HAVING COUNT(*) > 1;
        """;

    public static async Task ApplyAsync(AedDbContext db, CancellationToken ct)
    {
        await AddExperienceUserColumnsAsync(db, ct);
        await AddResponderColumnsAsync(db, ct);
        await CreateVolunteerApplicationsAsync(db, ct);
        await CreateSchoolIdentityTablesAsync(db, ct);
        await CreateHelpRequestIndexesAsync(db, ct);
        await SqliteRescueSchemaUpgrade.EnsureAsync(db, ct);
        await InsertMissingAedDeviceSeedsAsync(db, ct);
    }

    private static async Task AddExperienceUserColumnsAsync(AedDbContext db, CancellationToken ct)
    {
        if (!await ColumnExistsAsync(db, "experience_users", "Name", ct))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE experience_users ADD COLUMN Name TEXT NULL;", ct);
        if (!await ColumnExistsAsync(db, "experience_users", "AvatarUrl", ct))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE experience_users ADD COLUMN AvatarUrl TEXT NULL;", ct);
        if (!await ColumnExistsAsync(db, "experience_users", "Gender", ct))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE experience_users ADD COLUMN Gender TEXT NULL;", ct);
    }

    private static async Task AddResponderColumnsAsync(AedDbContext db, CancellationToken ct)
    {
        if (!await ColumnExistsAsync(db, "responder_profiles", "BoundOpenId", ct))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE responder_profiles ADD COLUMN BoundOpenId TEXT NULL;", ct);
        if (!await ColumnExistsAsync(db, "responder_profiles", "Source", ct))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE responder_profiles ADD COLUMN Source TEXT NOT NULL DEFAULT 'seed';", ct);
        if (!await ColumnExistsAsync(db, "responder_profiles", "SourceApplicationId", ct))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE responder_profiles ADD COLUMN SourceApplicationId INTEGER NULL;", ct);
    }

    internal static async Task<bool> ColumnExistsAsync(
        AedDbContext db,
        string table,
        string column,
        CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var closeAfterRead = connection.State == System.Data.ConnectionState.Closed;
        if (closeAfterRead) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({ValidateTableName(table)});";
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
        finally
        {
            if (closeAfterRead) await connection.CloseAsync();
        }
    }

    private static async Task CreateVolunteerApplicationsAsync(AedDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS volunteer_applications (
                Id INTEGER NOT NULL CONSTRAINT pk_volunteer_applications PRIMARY KEY AUTOINCREMENT,
                OpenId TEXT NOT NULL,
                Status TEXT NOT NULL,
                DerivedFromId INTEGER NULL,
                AttemptNumber INTEGER NOT NULL,
                PayloadJson TEXT NOT NULL,
                ReviewNote TEXT NULL,
                ReviewedBy TEXT NULL,
                ReviewedAt TEXT NULL,
                CreatedRescuerId TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CONSTRAINT fk_volunteer_applications_self
                    FOREIGN KEY (DerivedFromId) REFERENCES volunteer_applications(Id)
                    ON DELETE RESTRICT
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS ix_volunteer_applications_open_id_created_at ON volunteer_applications(OpenId, CreatedAt);",
            ct);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS ix_responder_profiles_bound_open_id ON responder_profiles(BoundOpenId);",
            ct);
    }

    private static async Task CreateSchoolIdentityTablesAsync(AedDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS school_person_profiles (
                Id INTEGER NOT NULL CONSTRAINT pk_school_person_profiles PRIMARY KEY AUTOINCREMENT,
                PersonType TEXT NOT NULL,
                Name TEXT NOT NULL,
                College TEXT NOT NULL,
                ClassName TEXT NULL,
                StudentId TEXT NULL,
                StaffId TEXT NULL,
                Phone TEXT NULL,
                Status TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS school_binding_applications (
                Id INTEGER NOT NULL CONSTRAINT pk_school_binding_applications PRIMARY KEY AUTOINCREMENT,
                OpenId TEXT NOT NULL,
                Status TEXT NOT NULL,
                SchoolProfileId INTEGER NULL,
                PayloadJson TEXT NOT NULL,
                ReviewNote TEXT NULL,
                ReviewedBy TEXT NULL,
                ReviewedAt TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CONSTRAINT fk_school_binding_profile
                    FOREIGN KEY (SchoolProfileId) REFERENCES school_person_profiles(Id)
                    ON DELETE RESTRICT
            );
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS responder_claim_applications (
                Id INTEGER NOT NULL CONSTRAINT pk_responder_claim_applications PRIMARY KEY AUTOINCREMENT,
                OpenId TEXT NOT NULL,
                Status TEXT NOT NULL,
                ResponderProfileId TEXT NULL,
                PayloadJson TEXT NOT NULL,
                ReviewNote TEXT NULL,
                ReviewedBy TEXT NULL,
                ReviewedAt TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CONSTRAINT fk_responder_claim_profile
                    FOREIGN KEY (ResponderProfileId) REFERENCES responder_profiles(Id)
                    ON DELETE RESTRICT
            );
            """, ct);
        await CreateIdentityIndexesAsync(db, ct);
    }

    /** 将历史冲突转换为可行动的启动错误；失败的建索引语句不会改写业务记录。 */
    private static async Task CreateHelpRequestIndexesAsync(AedDbContext db, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS uk_help_active_user
                ON help_requests(UserId)
                WHERE Status IN ('published', 'responding', 'pending_review');
                """, ct);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            var conflict = await ReadActiveHelpConflictAsync(db, ct);
            if (conflict.UserCount == 0) throw;
            throw CreateActiveHelpConflictException(db, conflict, exception);
        }
    }

    private static async Task<ActiveHelpConflictSummary> ReadActiveHelpConflictAsync(
        AedDbContext db,
        CancellationToken ct)
    {
        var counts = await db.HelpRequests.AsNoTracking()
            .Where(help => help.UserId != null
                && (help.Status == HelpStatuses.Published
                    || help.Status == HelpStatuses.Responding
                    || help.Status == HelpStatuses.PendingReview))
            .GroupBy(help => help.UserId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Count())
            .ToArrayAsync(ct);
        return new ActiveHelpConflictSummary(counts.Length, counts.Sum());
    }

    private static InvalidOperationException CreateActiveHelpConflictException(
        AedDbContext db,
        ActiveHelpConflictSummary conflict,
        SqliteException innerException)
    {
        var databasePath = Path.GetFullPath(db.Database.GetDbConnection().DataSource);
        var message = $"""
            SQLite 活动求助唯一索引升级已中止：数据库 "{databasePath}" 中检测到 {conflict.UserCount} 个身份共有 {conflict.RequestCount} 条冲突的活动求助。
            uk_help_active_user 尚未创建；升级不会自动删除或合并任何求助记录。
            请保持服务停止，先备份数据库文件以及同目录下可能存在的 -wal/-shm 文件，再以 SQLite 只读模式执行以下只读 SQL 排查：
            {ActiveHelpConflictReadOnlySql}
            由运维与业务负责人逐条确认真实状态并明确处理冲突；只读 SQL 返回 0 行后方可重启服务。
            """;
        return new InvalidOperationException(message, innerException);
    }

    private static async Task InsertMissingAedDeviceSeedsAsync(AedDbContext db, CancellationToken ct)
    {
        var existingIds = await db.AedDevices.Select(d => d.Id).ToArrayAsync(ct);
        var existingNames = await db.AedDevices.Select(d => d.Name).ToArrayAsync(ct);
        var idSet = existingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nameSet = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingSeeds = AedDeviceSeedData.Devices
            .Where(d => !idSet.Contains(d.Id) && !nameSet.Contains(d.Name))
            .Select(SqliteMapper.ToEntity)
            .ToArray();
        if (missingSeeds.Length == 0) return;

        db.AedDevices.AddRange(missingSeeds);
        await db.SaveChangesAsync(ct);
    }

    private static async Task CreateIdentityIndexesAsync(AedDbContext db, CancellationToken ct)
    {
        var statements = new[]
        {
            "CREATE INDEX IF NOT EXISTS ix_school_profiles_person_name ON school_person_profiles(PersonType, Name);",
            "CREATE INDEX IF NOT EXISTS ix_school_profiles_student_id ON school_person_profiles(StudentId);",
            "CREATE INDEX IF NOT EXISTS ix_school_profiles_staff_id ON school_person_profiles(StaffId);",
            "CREATE INDEX IF NOT EXISTS ix_school_binding_open_id_created_at ON school_binding_applications(OpenId, CreatedAt);",
            "CREATE INDEX IF NOT EXISTS ix_school_binding_status ON school_binding_applications(Status);",
            "CREATE INDEX IF NOT EXISTS ix_responder_claim_open_id_created_at ON responder_claim_applications(OpenId, CreatedAt);",
            "CREATE INDEX IF NOT EXISTS ix_responder_claim_status ON responder_claim_applications(Status);"
        };
        foreach (var statement in statements)
        {
            await db.Database.ExecuteSqlRawAsync(statement, ct);
        }
    }

    private static string ValidateTableName(string table)
    {
        return table is "experience_users" or "responder_profiles" or "rescue_sessions"
            or "school_person_profiles" or "school_binding_applications"
            or "responder_claim_applications"
            ? table
            : throw new ArgumentOutOfRangeException(nameof(table), table, "不支持的 SQLite 表名");
    }

    private sealed record ActiveHelpConflictSummary(int UserCount, int RequestCount);
}
