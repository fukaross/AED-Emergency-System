using AedBackend.Core.Models.Course;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

public sealed class JsonToSqliteMigrator
{
    public async Task<JsonToSqliteMigrationReport> RunAsync(
        string dataRoot,
        string databasePath,
        CancellationToken ct)
    {
        var fullDbPath = Path.GetFullPath(databasePath);
        if (File.Exists(fullDbPath)) throw new InvalidOperationException("目标 SQLite 数据库已存在");

        var snapshot = await new JsonRuntimeDataReader(dataRoot).ReadAsync(ct);
        JsonRuntimeDataValidator.Validate(snapshot);
        var tempPath = BuildTempPath(fullDbPath);

        try
        {
            await ImportAsync(snapshot, tempPath, ct);
            var counts = await ReconcileAsync(snapshot, tempPath, ct);
            await CheckpointAsync(tempPath, ct);
            Directory.CreateDirectory(Path.GetDirectoryName(fullDbPath) ?? ".");
            File.Move(tempPath, fullDbPath);
            DeleteSqliteFiles(tempPath);
            return new JsonToSqliteMigrationReport(Path.GetFullPath(dataRoot), fullDbPath, counts);
        }
        catch
        {
            DeleteSqliteFiles(tempPath);
            throw;
        }
    }

    private static async Task ImportAsync(JsonRuntimeSnapshot snapshot, string dbPath, CancellationToken ct)
    {
        using var scope = await CreateScopeAsync(dbPath, ct);
        await using var tx = await scope.Db.Database.BeginTransactionAsync(ct);
        AddSnapshot(scope.Db, snapshot);
        await scope.Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static void AddSnapshot(AedDbContext db, JsonRuntimeSnapshot snapshot)
    {
        db.ExperienceUsers.AddRange(snapshot.ExperienceUsers.Select(SqliteMapper.ToEntity));
        db.AdminUsers.AddRange(snapshot.AdminUsers.Select(SqliteMapper.ToEntity));
        db.AdminUserProfiles.AddRange(snapshot.AdminUserProfiles.Select(SqliteMapper.ToEntity));
        db.AdminAuditLogs.AddRange(snapshot.AdminAuditLogs.Select(SqliteMapper.ToEntity));
        db.AedDevices.AddRange(snapshot.AedDevices.Select(SqliteMapper.ToEntity));
        db.ResponderProfiles.AddRange(snapshot.ResponderProfiles.Select(SqliteMapper.ToEntity));
        if (snapshot.KnowledgeSummary is not null) db.KnowledgeSummary.Add(SqliteMapper.ToEntity(snapshot.KnowledgeSummary));
        db.Courses.AddRange(snapshot.Courses.Select(SqliteMapper.ToEntity));
        db.QuickActions.AddRange(snapshot.QuickActions.Select(SqliteMapper.ToEntity));
        db.HelpRequests.AddRange(snapshot.HelpRequests.Select(SqliteMapper.ToEntity));
        db.RescueSessions.AddRange(snapshot.RescueSessions.Select(SqliteMapper.ToEntity));
    }

    private static async Task<IReadOnlyDictionary<string, int>> ReconcileAsync(
        JsonRuntimeSnapshot snapshot,
        string dbPath,
        CancellationToken ct)
    {
        using var scope = await CreateScopeAsync(dbPath, ct);
        var counts = await ReadCountsAsync(scope.Db, ct);
        CheckCounts(snapshot, counts);
        await CheckCriticalFieldsAsync(snapshot, scope.Db, ct);
        return counts;
    }

    private static async Task CheckpointAsync(string dbPath, CancellationToken ct)
    {
        using var scope = await CreateScopeAsync(dbPath, ct);
        await scope.Db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", ct);
    }

    private static async Task<DbScope> CreateScopeAsync(string dbPath, CancellationToken ct)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AedDbContext>(options =>
            options.UseSqlite(SqliteStoreHelpers.BuildConnectionString(dbPath)));
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<AedDbContext>>();
        var db = await factory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        await SqliteStoreHelpers.ApplyPragmasAsync(db, ct);
        return new DbScope(provider, db);
    }

    private static async Task<IReadOnlyDictionary<string, int>> ReadCountsAsync(AedDbContext db, CancellationToken ct)
    {
        return new Dictionary<string, int>
        {
            ["experience_users"] = await db.ExperienceUsers.CountAsync(ct),
            ["admin_users"] = await db.AdminUsers.CountAsync(ct),
            ["admin_sessions"] = await db.AdminSessions.CountAsync(ct),
            ["admin_user_profiles"] = await db.AdminUserProfiles.CountAsync(ct),
            ["admin_audit_logs"] = await db.AdminAuditLogs.CountAsync(ct),
            ["aed_devices"] = await db.AedDevices.CountAsync(ct),
            ["responder_profiles"] = await db.ResponderProfiles.CountAsync(ct),
            ["courses"] = await db.Courses.CountAsync(ct),
            ["course_videos"] = await db.CourseVideos.CountAsync(ct),
            ["quick_actions"] = await db.QuickActions.CountAsync(ct),
            ["help_requests"] = await db.HelpRequests.CountAsync(ct),
            ["help_reports"] = await db.HelpReports.CountAsync(ct),
            ["rescue_sessions"] = await db.RescueSessions.CountAsync(ct),
            ["rescue_session_participants"] = await db.RescueSessionParticipants.CountAsync(ct),
            ["rescue_session_messages"] = await db.RescueSessionMessages.CountAsync(ct)
        };
    }

    private static void CheckCounts(JsonRuntimeSnapshot snapshot, IReadOnlyDictionary<string, int> counts)
    {
        RequireCount(counts, "experience_users", snapshot.ExperienceUsers.Count);
        RequireCount(counts, "admin_users", snapshot.AdminUsers.Count);
        RequireCount(counts, "admin_sessions", snapshot.AdminUsers.Sum(u => u.Sessions.Count));
        RequireCount(counts, "admin_user_profiles", snapshot.AdminUserProfiles.Count);
        RequireCount(counts, "admin_audit_logs", snapshot.AdminAuditLogs.Count);
        RequireCount(counts, "aed_devices", snapshot.AedDevices.Count);
        RequireCount(counts, "responder_profiles", snapshot.ResponderProfiles.Count);
        RequireCount(counts, "courses", snapshot.Courses.Count);
        RequireCount(counts, "course_videos", snapshot.Courses.Sum(c => c.Videos.Count));
        RequireCount(counts, "quick_actions", snapshot.QuickActions.Count);
        RequireCount(counts, "help_requests", snapshot.HelpRequests.Count);
        RequireCount(counts, "help_reports", snapshot.HelpRequests.Sum(h => h.Reports.Count));
        RequireCount(counts, "rescue_sessions", snapshot.RescueSessions.Count);
        RequireCount(counts, "rescue_session_participants", snapshot.RescueSessions.Sum(s => s.Participants.Count));
        RequireCount(counts, "rescue_session_messages", snapshot.RescueSessions.Sum(s => s.Messages.Count));
    }

    private static async Task CheckCriticalFieldsAsync(JsonRuntimeSnapshot snapshot, AedDbContext db, CancellationToken ct)
    {
        await CheckCoursesAsync(snapshot.Courses, db, ct);
        await CheckRespondersAsync(snapshot.ResponderProfiles, db, ct);
        await CheckHelpAsync(snapshot.HelpRequests, db, ct);
        await CheckAdminSessionsAsync(snapshot.AdminUsers, db, ct);
        await RescueMigrationReconciler.CheckAsync(snapshot.RescueSessions, db, ct);
    }

    private static async Task CheckCoursesAsync(IReadOnlyList<CourseRecord> courses, AedDbContext db, CancellationToken ct)
    {
        var entities = await SqliteStoreHelpers.WithCourseGraph(db.Courses.AsNoTracking()).ToArrayAsync(ct);
        var records = entities.Select(SqliteMapper.ToRecord).ToDictionary(c => c.Id);
        foreach (var course in courses)
        {
            var actual = records[course.Id];
            if (actual.VideoUrl != course.VideoUrl || actual.Content != course.Content)
                throw new InvalidOperationException($"课程字段对账失败: {course.Id}");
            CheckVideoUrls(course, actual);
        }
    }

    private static async Task CheckRespondersAsync(IReadOnlyList<Core.Models.Responder.ResponderProfileRecord> responders, AedDbContext db, CancellationToken ct)
    {
        var actual = await db.ResponderProfiles.AsNoTracking().ToDictionaryAsync(r => r.Id, ct);
        foreach (var responder in responders)
        {
            var item = actual[responder.Id];
            if (item.Phone != responder.Phone || item.IdNumber != responder.IdNumber || item.CertificateNo != responder.CertificateNo)
                throw new InvalidOperationException($"急救员字段对账失败: {responder.Id}");
        }
    }

    private static async Task CheckHelpAsync(IReadOnlyList<Core.Models.Help.HelpRequestRecord> requests, AedDbContext db, CancellationToken ct)
    {
        var entities = await SqliteStoreHelpers.WithHelpGraph(db.HelpRequests.AsNoTracking()).ToArrayAsync(ct);
        var actual = entities.Select(SqliteMapper.ToRecord).ToDictionary(h => h.Id);
        foreach (var request in requests)
        {
            var item = actual[request.Id];
            if (item.Status != request.Status || item.Images.SequenceEqual(request.Images) == false)
                throw new InvalidOperationException($"求助记录对账失败: {request.Id}");
        }
    }

    private static async Task CheckAdminSessionsAsync(IReadOnlyList<Core.Models.Admin.AdminUserRecord> users, AedDbContext db, CancellationToken ct)
    {
        var actual = await db.AdminSessions.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        foreach (var session in users.SelectMany(u => u.Sessions))
        {
            var item = actual[session.Id];
            if (item.SessionHash != session.SessionHash || item.DeviceHash != session.DeviceHash)
                throw new InvalidOperationException($"Admin session 对账失败: {session.Id}");
        }
    }

    private static void CheckVideoUrls(CourseRecord expected, CourseRecord actual)
    {
        var left = expected.Videos.OrderBy(v => v.Order).Select(v => v.Url);
        var right = actual.Videos.OrderBy(v => v.Order).Select(v => v.Url);
        if (!left.SequenceEqual(right)) throw new InvalidOperationException($"课程视频对账失败: {expected.Id}");
    }

    private static string BuildTempPath(string dbPath)
    {
        return $"{dbPath}.tmp-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }

    private static void DeleteSqliteFiles(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists($"{path}-wal")) File.Delete($"{path}-wal");
        if (File.Exists($"{path}-shm")) File.Delete($"{path}-shm");
    }

    private static void RequireCount(IReadOnlyDictionary<string, int> counts, string key, int expected)
    {
        if (counts[key] != expected) throw new InvalidOperationException($"{key} 数量对账失败");
    }

    private sealed class DbScope : IDisposable
    {
        private readonly ServiceProvider provider;
        public AedDbContext Db { get; }

        public DbScope(ServiceProvider provider, AedDbContext db)
        {
            this.provider = provider;
            Db = db;
        }

        public void Dispose()
        {
            Db.Dispose();
            provider.Dispose();
        }
    }
}
