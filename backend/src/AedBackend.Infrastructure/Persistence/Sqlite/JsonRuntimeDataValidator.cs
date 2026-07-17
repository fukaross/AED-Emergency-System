namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal static class JsonRuntimeDataValidator
{
    public static void Validate(JsonRuntimeSnapshot snapshot)
    {
        Unique(snapshot.ExperienceUsers.Select(u => u.OpenId), "experience_users.openId");
        Unique(snapshot.AdminUsers.Select(u => u.Id), "admin_users.id");
        Unique(snapshot.AdminUsers.Select(u => u.NormalizedUsername), "admin_users.normalizedUsername");
        Unique(snapshot.AdminUserProfiles.Select(p => p.OpenId), "admin_user_profiles.openId");
        Unique(snapshot.AdminAuditLogs.Select(l => l.Id), "admin_audit_logs.id");
        Unique(snapshot.AedDevices.Select(d => d.Id), "aed_devices.id");
        Unique(snapshot.AedDevices.Select(d => d.Name), "aed_devices.name");
        Unique(snapshot.ResponderProfiles.Select(r => r.Id), "responder_profiles.id");
        Unique(snapshot.Courses.Select(c => c.Id), "courses.id");
        Unique(snapshot.QuickActions.Select(a => a.Id), "quick_actions.id");
        Unique(snapshot.HelpRequests.Select(h => h.Id), "help_requests.id");
        Unique(snapshot.HelpRequests.Select(h => h.RequestNo), "help_requests.requestNo");
        Unique(snapshot.RescueSessions.Select(s => s.HelpRequestId), "rescue_sessions.helpRequestId");
        ValidateCourses(snapshot.Courses);
        ValidateHelp(snapshot.HelpRequests);
        ValidateAdmin(snapshot.AdminUsers);
        ValidateRescue(snapshot.RescueSessions, snapshot.HelpRequests.Select(item => item.Id));
    }

    private static void ValidateCourses(IEnumerable<Core.Models.Course.CourseRecord> courses)
    {
        foreach (var course in courses)
        {
            Unique(course.Steps.Select(s => s.Order.ToString()), $"course_steps.order:{course.Id}");
            Unique(course.Videos.Select(v => v.Order.ToString()), $"course_videos.order:{course.Id}");
            if (course.Videos.Any(v => string.IsNullOrWhiteSpace(v.Url)))
                throw new InvalidOperationException($"课程视频 URL 为空: {course.Id}");
        }
    }

    private static void ValidateHelp(IEnumerable<Core.Models.Help.HelpRequestRecord> requests)
    {
        foreach (var request in requests)
        {
            Unique(request.Reports.Select(r => r.Id), $"help_reports.id:{request.Id}");
            if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.RequestNo))
                throw new InvalidOperationException("求助记录缺少 ID 或 RequestNo");
        }
    }

    private static void ValidateAdmin(IEnumerable<Core.Models.Admin.AdminUserRecord> users)
    {
        foreach (var user in users)
        {
            Unique(user.Sessions.Select(s => s.Id), $"admin_sessions.id:{user.Id}");
            if (user.Sessions.Any(s => string.IsNullOrWhiteSpace(s.SessionHash)))
                throw new InvalidOperationException($"Admin session hash 为空: {user.Id}");
        }
    }

    private static void ValidateRescue(
        IEnumerable<Core.Models.Rescue.RescueSessionRecord> sessions,
        IEnumerable<string> helpRequestIds)
    {
        var helpIds = helpRequestIds.ToHashSet(StringComparer.Ordinal);
        foreach (var session in sessions)
        {
            if (!helpIds.Contains(session.HelpRequestId))
                throw new InvalidOperationException($"急救会话缺少关联求助: {session.HelpRequestId}");
            Unique(session.Participants.Select(p => p.OpenId), $"rescue_session_participants.openId:{session.HelpRequestId}");
            Unique(session.Messages.Select(m => m.Id), $"rescue_session_messages.id:{session.HelpRequestId}");
            Unique(session.Messages.Select(m => m.Seq.ToString()), $"rescue_session_messages.seq:{session.HelpRequestId}");
        }
    }

    private static void Unique(IEnumerable<string?> values, string label)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            if (!seen.Add(value!)) throw new InvalidOperationException($"重复键: {label}={value}");
        }
    }
}
