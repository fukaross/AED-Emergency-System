using AedBackend.Core.Models.Rescue;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal static class RescueMigrationReconciler
{
    public static async Task CheckAsync(
        IReadOnlyList<RescueSessionRecord> sessions,
        AedDbContext db,
        CancellationToken ct)
    {
        var entities = await SqliteStoreHelpers.WithRescueGraph(db.RescueSessions.AsNoTracking())
            .ToArrayAsync(ct);
        var actual = entities.Select(SqliteMapper.ToRecord).ToDictionary(item => item.HelpRequestId);
        foreach (var session in sessions)
        {
            if (!actual.TryGetValue(session.HelpRequestId, out var item)
                || item.CreatedAt != session.CreatedAt
                || item.ClosedAt != session.ClosedAt)
            {
                throw Mismatch(session.HelpRequestId, "字段");
            }
            CheckParticipants(session, item);
            CheckMessages(session, item);
        }
    }

    private static void CheckParticipants(RescueSessionRecord expected, RescueSessionRecord actual)
    {
        var left = expected.Participants.OrderBy(item => item.OpenId).ToArray();
        var right = actual.Participants.OrderBy(item => item.OpenId).ToArray();
        if (left.Length != right.Length) throw Mismatch(expected.HelpRequestId, "参与者");
        for (var index = 0; index < left.Length; index++)
        {
            if (!ParticipantMatches(left[index], right[index]))
                throw Mismatch(expected.HelpRequestId, "参与者");
        }
    }

    private static void CheckMessages(RescueSessionRecord expected, RescueSessionRecord actual)
    {
        var left = expected.Messages.OrderBy(item => item.Seq).ToArray();
        var right = actual.Messages.OrderBy(item => item.Seq).ToArray();
        if (left.Length != right.Length) throw Mismatch(expected.HelpRequestId, "消息");
        for (var index = 0; index < left.Length; index++)
        {
            if (!MessageMatches(left[index], right[index]))
                throw Mismatch(expected.HelpRequestId, "消息");
        }
    }

    private static bool ParticipantMatches(
        RescueSessionParticipantRecord expected,
        RescueSessionParticipantRecord actual) =>
        expected.OpenId == actual.OpenId
        && expected.DisplayName == actual.DisplayName
        && expected.JoinedAt == actual.JoinedAt
        && expected.LastSeenAt == actual.LastSeenAt
        && expected.LastLat == actual.LastLat
        && expected.LastLng == actual.LastLng
        && expected.LocationReportedAt == actual.LocationReportedAt
        && expected.LastReadSeq == actual.LastReadSeq;

    private static bool MessageMatches(
        RescueSessionMessageRecord expected,
        RescueSessionMessageRecord actual) =>
        expected.Id == actual.Id
        && expected.Seq == actual.Seq
        && expected.Kind == actual.Kind
        && expected.SenderOpenId == actual.SenderOpenId
        && expected.SenderName == actual.SenderName
        && expected.Text == actual.Text
        && expected.EventType == actual.EventType
        && expected.Action == actual.Action
        && expected.CreatedAt == actual.CreatedAt;

    private static InvalidOperationException Mismatch(string helpRequestId, string field) =>
        new($"急救会话{field}对账失败: {helpRequestId}");
}
