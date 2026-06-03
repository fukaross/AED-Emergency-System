using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal static class SqliteSchemaUpgrade
{
    public static async Task ApplyAsync(AedDbContext db, CancellationToken ct)
    {
        await AddExperienceUserColumnsAsync(db, ct);
        await AddResponderColumnsAsync(db, ct);
        await CreateVolunteerApplicationsAsync(db, ct);
        await CreateSchoolIdentityTablesAsync(db, ct);
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

    private static async Task<bool> ColumnExistsAsync(
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
        return table is "experience_users" or "responder_profiles"
            or "school_person_profiles" or "school_binding_applications"
            or "responder_claim_applications"
            ? table
            : throw new ArgumentOutOfRangeException(nameof(table), table, "不支持的 SQLite 表名");
    }
}
