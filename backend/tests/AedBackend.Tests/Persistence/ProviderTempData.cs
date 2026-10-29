using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using AedBackend.Infrastructure.Admin;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Models.Aed;
using AedBackend.Core.Models.Course;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Knowledge;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Models.WeChat;

namespace AedBackend.Tests.Persistence;

internal sealed partial class ProviderTempData : IDisposable
{
    public const string AdminSessionKey = "valid-admin-session";
    public const string AdminSessionHashSecret = "test-session-secret-value";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string Root { get; } = Path.Combine(Path.GetTempPath(), $"aed-sqlite-test-{Guid.NewGuid():N}");
    public string DbPath => Path.Combine(Root, "aed.db");

    public ProviderTempData() => Directory.CreateDirectory(Root);

    public async Task WriteMinimalJsonAsync()
    {
        await WriteAsync("wechat-experience-users.json", new ExperienceUsersDb());
        await WriteAsync("admin-users.json", new AdminUsersDb());
        await WriteAsync("admin-user-profiles.json", new AdminProfilesDb());
        await WriteAsync("admin-audit-logs.json", new AdminAuditDb());
        await WriteAsync("aed-devices.json", Array.Empty<AedDeviceRecord>());
        await WriteAsync("rescuer-profiles.json", Array.Empty<ResponderProfileRecord>());
        await WriteAsync("knowledge-summary.json", new KnowledgeSummary());
        await WriteAsync("courses.json", Array.Empty<CourseRecord>());
        await WriteAsync("quick-actions.json", Array.Empty<QuickActionRecord>());
        await WriteAsync("help-requests.json", new HelpRequestsDb());
    }

    public async Task WriteValidDataAsync()
    {
        await WriteMinimalJsonAsync();
        await WriteAsync("admin-users.json", new AdminUsersDb { Users = [SampleAdmin()] });
        await WriteAsync("aed-devices.json", new[] { SampleAed() });
        await WriteAsync("rescuer-profiles.json", new[] { SampleResponder() });
        await WriteAsync("knowledge-summary.json", SampleSummary());
        await WriteAsync("courses.json", new[] { SampleCourse() });
        await WriteAsync("quick-actions.json", new[] { SampleQuickAction() });
        await WriteAsync("help-requests.json", new HelpRequestsDb { Requests = [SampleHelpRequest()] });
    }

    public async Task WriteAsync<T>(string fileName, T value)
    {
        await using var stream = File.Create(Path.Combine(Root, fileName));
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions);
    }

    public async Task WriteUserJsonAsync(
        string openId,
        string phone,
        string token)
    {
        var user = new ExperienceUserRecord(
            openId,
            null,
            phone,
            null,
            null,
            null,
            HashToken(token),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            new Dictionary<string, SubscriptionRecord>());
        await WriteAsync("wechat-experience-users.json", new ExperienceUsersDb { Users = [user] });
    }

    public async Task WriteUsersJsonAsync(params (string OpenId, string Token)[] users)
    {
        var records = users.Select(u => new ExperienceUserRecord(
            u.OpenId,
            null,
            "13800138000",
            null,
            null,
            null,
            HashToken(u.Token),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            new Dictionary<string, SubscriptionRecord>()));
        await WriteAsync("wechat-experience-users.json", new ExperienceUsersDb { Users = [.. records] });
    }

    public async Task WriteBoundResponderJsonAsync(string openId)
    {
        var responder = SampleResponder();
        responder.BoundOpenId = openId;
        await WriteAsync("rescuer-profiles.json", new[] { responder });
    }

    public async Task WriteResponderJsonAsync(params ResponderProfileRecord[] responders)
    {
        await WriteAsync("rescuer-profiles.json", responders);
    }

    public static ResponderProfileRecord Responder(
        string id,
        string name,
        string? level = "trainee",
        string status = "active",
        string? boundOpenId = null)
    {
        return SampleResponder() with
        {
            Id = id,
            RecordId = $"ARC-{id}",
            Name = name,
            Level = level,
            QualificationStatus = status,
            BoundOpenId = boundOpenId
        };
    }

    public void Dispose() => Directory.Delete(Root, true);

    private static AdminUserRecord SampleAdmin()
    {
        return new AdminUserRecord
        {
            Id = "admin-01",
            Username = "root",
            NormalizedUsername = "ROOT",
            PasswordHash = "hash",
            Role = AdminRoles.SuperAdmin,
            CreatedAt = DateTimeOffset.UtcNow,
            Sessions = [SampleSession()]
        };
    }

    private static AdminSessionEntry SampleSession()
    {
        return new AdminSessionEntry
        {
            Id = "session-01",
            SessionHash = AdminSessionCrypto.HashSessionKey(AdminSessionKey, AdminSessionHashSecret),
            DeviceHash = "device-hash",
            IpAddress = "127.0.0.1",
            MaskedIpAddress = "127.0.*.*",
            UserAgentSummary = "test",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            LastSeenAt = DateTimeOffset.UtcNow
        };
    }

    private static AedDeviceRecord SampleAed()
    {
        return new AedDeviceRecord { Id = "AED-01", Name = "AED", Lat = 1, Lng = 2, Address = "addr" };
    }

    private static ResponderProfileRecord SampleResponder()
    {
        return new ResponderProfileRecord
        {
            Id = "rescuer-001",
            RecordId = "FIXTURE-001",
            Name = "演示档案甲",
            Gender = "female",
            BirthDate = "2000-01-01",
            Phone = "13800138000",
            IdNumber = "990000199001010017",
            AvatarUrl = "/uploads/rescuer/a.jpg",
            Campus = "east",
            QualificationStatus = "active",
            CertificateNo = "TESTCERT0001",
            CertificateNoMasked = "TEST****0001"
        };
    }

    private static CourseRecord SampleCourse()
    {
        return new CourseRecord
        {
            Id = "course-01",
            Title = "课程",
            Thumbnail = "/uploads/course.jpg",
            Type = "video",
            Category = "first-aid",
            Content = "<p><img src=\"/uploads/classroom/a.jpg\"></p>",
            VideoUrl = "/uploads/classroom-videos/a.mp4",
            Videos =
            [
                new() { Order = 1, Title = "视频 1", Url = "/uploads/classroom-videos/a.mp4" },
                new() { Order = 2, Title = "视频 2", Url = "/uploads/classroom-videos/b.mp4" }
            ],
            CreatedAt = "2026-06-02T00:00:00Z"
        };
    }

    private static KnowledgeSummary SampleSummary()
    {
        return new KnowledgeSummary
        {
            UpdatedAt = "2026-06-02T00:00:00Z",
            Items = [new KnowledgeStatItem { Key = "aed_count", Title = "AED数量", Value = 10, SortOrder = 1 }]
        };
    }

    private static QuickActionRecord SampleQuickAction()
    {
        return new QuickActionRecord { Id = "qa-01", Title = "CPR", Icon = "/i.png", Summary = "summary", SortOrder = 1 };
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }

    private static HelpRequestRecord SampleHelpRequest()
    {
        return new HelpRequestRecord
        {
            Id = "help-01",
            RequestNo = "HELP001",
            UserId = "openid-1",
            Lat = 1,
            Lng = 2,
            Address = "addr",
            Content = "content",
            CreatedAt = DateTimeOffset.UtcNow,
            RequesterPhone = "13800138000",
            Images = ["/uploads/help/a.jpg"],
            Reports = [new HelpReportRecord("report-01", "openid-2", ["/uploads/report/a.jpg"], "ok", false, DateTimeOffset.UtcNow)],
            Timeline = [new HelpTimelineRecord("published", DateTimeOffset.UtcNow, null)]
        };
    }

    private sealed class ExperienceUsersDb
    {
        public List<ExperienceUserRecord> Users { get; init; } = [];
    }

    private sealed class AdminUsersDb
    {
        public List<AdminUserRecord> Users { get; init; } = [];
    }

    private sealed class AdminProfilesDb
    {
        public Dictionary<string, AdminUserProfileRecord> Profiles { get; init; } = [];
    }

    private sealed class AdminAuditDb
    {
        public List<AdminAuditLogRecord> Logs { get; init; } = [];
    }

    private sealed class HelpRequestsDb
    {
        public List<HelpRequestRecord> Requests { get; init; } = [];
    }

}
