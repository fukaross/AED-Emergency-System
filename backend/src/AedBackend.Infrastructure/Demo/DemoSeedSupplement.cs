using AedBackend.Core.Models.User;
using AedBackend.Infrastructure.Persistence.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Infrastructure.Demo;

/**
 * 迁移器只覆盖 11 类 JSON 集合，不包含学校档案/身份绑定表。
 * 本类在迁移后的 SQLite 上补齐 demo/roles.json 的 schoolBinding 与 openId 关系，
 * 并对账“演示角色真的满足现有真实授权规则”，避免把空表迁移成功当成角色就绪。
 */
internal static class DemoSeedSupplement
{
    private const string DemoReviewer = "demo-init";
    private const string DemoReviewNote = "演示初始化：合成学校绑定自动通过（demo-v1）";
    private const string ActiveQualification = "active";

    public static async Task<(IReadOnlyDictionary<string, int> Counts, IReadOnlyList<DemoRoleReadiness> Roles)> ApplyAsync(
        string databasePath,
        DemoRolesDocument roles,
        DemoSourceSnapshot source,
        CancellationToken ct)
    {
        using var scope = await CreateScopeAsync(databasePath, ct);
        var db = scope.Db;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await EnsureNoAdminUsersAsync(db, ct);
        await InsertSchoolIdentitiesAsync(db, roles, ct);
        var readiness = await VerifyRolesAsync(db, roles, ct);
        var counts = await ReadCountsAsync(db, ct);
        VerifyCounts(counts, roles, source);
        await transaction.CommitAsync(ct);

        await CheckpointAsync(db, ct);
        return (counts, readiness);
    }

    /** 删除 WAL 伴生文件，保证原子移动时目标库是单文件。 */
    public static void DeleteSidecarFiles(string databasePath)
    {
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var path = $"{databasePath}{suffix}";
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static async Task EnsureNoAdminUsersAsync(AedDbContext db, CancellationToken ct)
    {
        var adminCount = await db.AdminUsers.CountAsync(ct);
        if (adminCount == 0) return;
        throw new DemoInitializationException(
            $"演示库中出现了 {adminCount} 个管理员账号；初始化不得创建管理员，请改用 POST /api/admin/auth/bootstrap。");
    }

    private static async Task InsertSchoolIdentitiesAsync(
        AedDbContext db,
        DemoRolesDocument roles,
        CancellationToken ct)
    {
        var pendingProfiles = new List<(DemoRoleDefinition Role, DemoSchoolBinding Binding, SchoolPersonProfileEntity Entity)>();
        var now = DateTimeOffset.UtcNow;
        foreach (var role in roles.DemoRoles)
        {
            if (role.SchoolBinding is not { } binding) continue;
            var entity = new SchoolPersonProfileEntity
            {
                PersonType = binding.PersonType,
                Name = binding.Name,
                College = binding.College,
                ClassName = binding.ClassName,
                StudentId = binding.StudentId,
                StaffId = binding.StaffId,
                Phone = binding.Phone,
                Status = "active",
                CreatedAt = now,
                UpdatedAt = now
            };
            db.SchoolPersonProfiles.Add(entity);
            pendingProfiles.Add((role, binding, entity));
        }

        if (pendingProfiles.Count == 0) return;
        await db.SaveChangesAsync(ct);

        foreach (var (role, binding, entity) in pendingProfiles)
        {
            var payload = new SchoolBindingPayloadRecord
            {
                PersonType = binding.PersonType,
                Name = binding.Name,
                College = binding.College,
                ClassName = binding.ClassName,
                StudentId = binding.StudentId,
                StaffId = binding.StaffId,
                Phone = binding.Phone,
                Note = binding.Note
            };
            db.SchoolBindingApplications.Add(new SchoolBindingApplicationEntity
            {
                OpenId = role.OpenId,
                Status = SchoolBindingStatuses.Approved,
                SchoolProfileId = entity.Id,
                PayloadJson = SqliteSchoolBindingApplicationStore.Serialize(payload),
                ReviewNote = DemoReviewNote,
                ReviewedBy = DemoReviewer,
                ReviewedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<IReadOnlyList<DemoRoleReadiness>> VerifyRolesAsync(
        AedDbContext db,
        DemoRolesDocument roles,
        CancellationToken ct)
    {
        var userOpenIds = (await db.ExperienceUsers.AsNoTracking().Select(user => user.OpenId).ToArrayAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var accountStatuses = await db.AdminUserProfiles.AsNoTracking()
            .ToDictionaryAsync(profile => profile.OpenId, profile => profile.AccountStatus, StringComparer.Ordinal, ct);
        var responders = await db.ResponderProfiles.AsNoTracking()
            .ToDictionaryAsync(responder => responder.Id, StringComparer.Ordinal, ct);
        var bindings = (await db.SchoolBindingApplications.AsNoTracking()
                .Where(binding => binding.Status == SchoolBindingStatuses.Approved)
                .ToArrayAsync(ct))
            .GroupBy(binding => binding.OpenId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        var readiness = new List<DemoRoleReadiness>(roles.DemoRoles.Count);
        foreach (var role in roles.DemoRoles)
        {
            if (!userOpenIds.Contains(role.OpenId))
            {
                throw new DemoInitializationException(
                    $"演示角色 {role.Role} 的用户 {role.OpenId} 未写入 SQLite experience_users");
            }

            if (!accountStatuses.TryGetValue(role.OpenId, out var accountStatus))
            {
                throw new DemoInitializationException(
                    $"演示角色 {role.Role} 缺少 admin_user_profiles 投影（accountStatus={role.AccountStatus}）");
            }

            if (!string.Equals(accountStatus, role.AccountStatus, StringComparison.OrdinalIgnoreCase))
            {
                throw new DemoInitializationException(
                    $"演示角色 {role.Role} 的账号状态对账失败：期望 {role.AccountStatus}，SQLite 为 {accountStatus}");
            }

            var certified = false;
            if (role.ResponderProfileId is { } responderId)
            {
                if (!responders.TryGetValue(responderId, out var responder))
                {
                    throw new DemoInitializationException(
                        $"演示角色 {role.Role} 的急救员档案 {responderId} 未写入 SQLite responder_profiles");
                }

                if (!string.Equals(responder.BoundOpenId, role.OpenId, StringComparison.Ordinal))
                {
                    throw new DemoInitializationException(
                        $"急救员档案 {responderId} 的 boundOpenId={responder.BoundOpenId} 未绑定到演示角色 {role.OpenId}");
                }

                certified = string.Equals(responder.QualificationStatus, ActiveQualification, StringComparison.OrdinalIgnoreCase);
                if (certified != role.ExpectedCertifiedResponder)
                {
                    throw new DemoInitializationException(
                        $"演示角色 {role.Role} 的授权对账失败：期望 certified={role.ExpectedCertifiedResponder}，实际 {certified}");
                }
            }

            var hasSchoolBinding = role.SchoolBinding is not null;
            if (hasSchoolBinding && !bindings.ContainsKey(role.OpenId))
            {
                throw new DemoInitializationException(
                    $"演示角色 {role.Role} 缺少审批通过的学校绑定（school_binding_applications）");
            }

            readiness.Add(new DemoRoleReadiness(
                role.Role,
                role.OpenId,
                role.Campus,
                role.AccountStatus,
                role.ResponderProfileId,
                certified,
                hasSchoolBinding));
        }

        return readiness;
    }

    private static async Task<IReadOnlyDictionary<string, int>> ReadCountsAsync(AedDbContext db, CancellationToken ct)
    {
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["experience_users"] = await db.ExperienceUsers.CountAsync(ct),
            ["admin_users"] = await db.AdminUsers.CountAsync(ct),
            ["admin_sessions"] = await db.AdminSessions.CountAsync(ct),
            ["admin_user_profiles"] = await db.AdminUserProfiles.CountAsync(ct),
            ["admin_audit_logs"] = await db.AdminAuditLogs.CountAsync(ct),
            ["aed_devices"] = await db.AedDevices.CountAsync(ct),
            ["responder_profiles"] = await db.ResponderProfiles.CountAsync(ct),
            ["courses"] = await db.Courses.CountAsync(ct),
            ["quick_actions"] = await db.QuickActions.CountAsync(ct),
            ["knowledge_summary"] = await db.KnowledgeSummary.CountAsync(ct),
            ["help_requests"] = await db.HelpRequests.CountAsync(ct),
            ["rescue_sessions"] = await db.RescueSessions.CountAsync(ct),
            ["school_person_profiles"] = await db.SchoolPersonProfiles.CountAsync(ct),
            ["school_binding_applications"] = await db.SchoolBindingApplications.CountAsync(ct)
        };
    }

    private static void VerifyCounts(
        IReadOnlyDictionary<string, int> counts,
        DemoRolesDocument roles,
        DemoSourceSnapshot source)
    {
        RequireCount(counts, "experience_users", source.ExperienceUserCount);
        RequireCount(counts, "admin_users", 0);
        RequireCount(counts, "admin_sessions", 0);
        RequireCount(counts, "aed_devices", source.AedDeviceCount);
        RequireCount(counts, "responder_profiles", source.RespondersById.Count);
        RequireCount(counts, "courses", source.CourseCount);
        RequireCount(counts, "quick_actions", source.QuickActionCount);
        RequireCount(counts, "knowledge_summary", 1);
        RequireCount(counts, "help_requests", 0);
        RequireCount(counts, "rescue_sessions", 0);

        var bindingCount = roles.DemoRoles.Count(role => role.SchoolBinding is not null);
        RequireCount(counts, "school_person_profiles", bindingCount);
        RequireCount(counts, "school_binding_applications", bindingCount);
    }

    private static void RequireCount(IReadOnlyDictionary<string, int> counts, string key, int expected)
    {
        var actual = counts[key];
        if (actual == expected) return;
        throw new DemoInitializationException(
            $"演示库对账失败：{key} 期望 {expected} 条，实际 {actual} 条");
    }

    private static async Task CheckpointAsync(AedDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", ct);
    }

    private static async Task<DbScope> CreateScopeAsync(string databasePath, CancellationToken ct)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AedDbContext>(options =>
            options.UseSqlite(SqliteStoreHelpers.BuildConnectionString(databasePath)));
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<AedDbContext>>();
        var db = await factory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        await SqliteSchemaUpgrade.ApplyAsync(db, ct);
        await SqliteStoreHelpers.ApplyPragmasAsync(db, ct);
        return new DbScope(provider, db);
    }

    private sealed class DbScope : IDisposable
    {
        private readonly ServiceProvider provider;

        public DbScope(ServiceProvider provider, AedDbContext db)
        {
            this.provider = provider;
            Db = db;
        }

        public AedDbContext Db { get; }

        public void Dispose()
        {
            Db.Dispose();
            provider.Dispose();
        }
    }
}
