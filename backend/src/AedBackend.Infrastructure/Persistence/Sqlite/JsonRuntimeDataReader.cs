using System.Text.Json;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Models.Aed;
using AedBackend.Core.Models.Course;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Knowledge;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Models.WeChat;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class JsonRuntimeDataReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string dataRoot;

    public JsonRuntimeDataReader(string dataRoot) => this.dataRoot = Path.GetFullPath(dataRoot);

    public async Task<JsonRuntimeSnapshot> ReadAsync(CancellationToken ct)
    {
        return new JsonRuntimeSnapshot(
            await ReadWrappedListAsync<ExperienceUserDatabase, ExperienceUserRecord>("wechat-experience-users.json", db => db.Users, ct),
            await ReadWrappedListAsync<AdminUserDatabase, AdminUserRecord>("admin-users.json", db => db.Users, ct),
            await ReadAdminProfilesAsync(ct),
            await ReadWrappedListAsync<AdminAuditLogDatabase, AdminAuditLogRecord>("admin-audit-logs.json", db => db.Logs, ct),
            await ReadListAsync<AedDeviceRecord>("aed-devices.json", ct),
            await ReadListAsync<ResponderProfileRecord>("rescuer-profiles.json", ct),
            await ReadOptionalAsync<KnowledgeSummary>("knowledge-summary.json", ct),
            await ReadListAsync<CourseRecord>("courses.json", ct),
            await ReadListAsync<QuickActionRecord>("quick-actions.json", ct),
            await ReadWrappedListAsync<HelpRequestDatabase, HelpRequestRecord>("help-requests.json", db => db.Requests, ct));
    }

    private async Task<T?> ReadOptionalAsync<T>(string fileName, CancellationToken ct)
    {
        var path = Resolve(fileName);
        if (!File.Exists(path)) return default;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }

    private async Task<IReadOnlyList<T>> ReadListAsync<T>(string fileName, CancellationToken ct)
    {
        return await ReadOptionalAsync<List<T>>(fileName, ct) ?? [];
    }

    private async Task<IReadOnlyList<TItem>> ReadWrappedListAsync<TDb, TItem>(
        string fileName,
        Func<TDb, IReadOnlyList<TItem>> selector,
        CancellationToken ct)
        where TDb : new()
    {
        var db = await ReadOptionalAsync<TDb>(fileName, ct) ?? new TDb();
        return selector(db);
    }

    private async Task<IReadOnlyList<AdminUserProfileRecord>> ReadAdminProfilesAsync(CancellationToken ct)
    {
        var db = await ReadOptionalAsync<AdminUserProfileDatabase>("admin-user-profiles.json", ct)
            ?? new AdminUserProfileDatabase();
        return db.Profiles.Values.ToArray();
    }

    private string Resolve(string fileName) => Path.Combine(dataRoot, fileName);

    private sealed class ExperienceUserDatabase
    {
        public List<ExperienceUserRecord> Users { get; init; } = [];
    }

    private sealed class AdminUserDatabase
    {
        public List<AdminUserRecord> Users { get; init; } = [];
    }

    private sealed class AdminUserProfileDatabase
    {
        public Dictionary<string, AdminUserProfileRecord> Profiles { get; init; } = [];
    }

    private sealed class AdminAuditLogDatabase
    {
        public List<AdminAuditLogRecord> Logs { get; init; } = [];
    }

    private sealed class HelpRequestDatabase
    {
        public List<HelpRequestRecord> Requests { get; init; } = [];
    }
}
