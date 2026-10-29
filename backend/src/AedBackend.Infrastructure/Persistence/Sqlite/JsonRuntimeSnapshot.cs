using AedBackend.Core.Models.Admin;
using AedBackend.Core.Models.Aed;
using AedBackend.Core.Models.Course;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Knowledge;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Models.WeChat;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

public sealed record JsonRuntimeSnapshot(
    IReadOnlyList<ExperienceUserRecord> ExperienceUsers,
    IReadOnlyList<AdminUserRecord> AdminUsers,
    IReadOnlyList<AdminUserProfileRecord> AdminUserProfiles,
    IReadOnlyList<AdminAuditLogRecord> AdminAuditLogs,
    IReadOnlyList<AedDeviceRecord> AedDevices,
    IReadOnlyList<ResponderProfileRecord> ResponderProfiles,
    KnowledgeSummary? KnowledgeSummary,
    IReadOnlyList<CourseRecord> Courses,
    IReadOnlyList<QuickActionRecord> QuickActions,
    IReadOnlyList<HelpRequestRecord> HelpRequests,
    IReadOnlyList<RescueSessionRecord> RescueSessions);

public sealed record JsonToSqliteMigrationReport(
    string DataRoot,
    string DatabasePath,
    IReadOnlyDictionary<string, int> Counts);
