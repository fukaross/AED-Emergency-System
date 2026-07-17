using AedBackend.Core.Models.Rescue;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal static class SqliteRescueSessionOverviewReader
{
    /**
     * 三条线性元数据查询避免列表端加载消息正文，也避免两个 collection Include 形成笛卡尔积。
     */
    public static async Task<IReadOnlyList<RescueSessionOverviewRecord>> ReadAllAsync(
        AedDbContext db,
        string? viewerOpenId,
        CancellationToken ct)
    {
        var rows = await ReadRowsAsync(db, ct);
        return BuildOverviews(rows, viewerOpenId);
    }

    private static async Task<OverviewRows> ReadRowsAsync(AedDbContext db, CancellationToken ct)
    {
        var sessions = await db.RescueSessions.AsNoTracking()
            .Select(item => new SessionRow
            {
                HelpRequestId = item.HelpRequestId,
                CreatedAt = item.CreatedAt,
                ClosedAt = item.ClosedAt
            })
            .ToArrayAsync(ct);
        var participants = await db.RescueSessionParticipants.AsNoTracking()
            .Select(item => new ParticipantRow
            {
                HelpRequestId = item.HelpRequestId,
                OpenId = item.OpenId,
                JoinedAt = item.JoinedAt,
                LastReadSeq = item.LastReadSeq
            })
            .ToArrayAsync(ct);
        var messages = await db.RescueSessionMessages.AsNoTracking()
            .Select(item => new MessageRow
            {
                HelpRequestId = item.HelpRequestId,
                Seq = item.Seq,
                SenderOpenId = item.SenderOpenId,
                CreatedAt = item.CreatedAt
            })
            .ToArrayAsync(ct);
        return new OverviewRows(sessions, participants, messages);
    }

    private static IReadOnlyList<RescueSessionOverviewRecord> BuildOverviews(
        OverviewRows rows,
        string? viewerOpenId)
    {
        var groups = new OverviewGroups(
            rows.Participants.ToLookup(item => item.HelpRequestId),
            rows.Messages.ToLookup(item => item.HelpRequestId));
        return rows.Sessions.Select(session => BuildOverview(session, groups, viewerOpenId))
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();
    }

    private static RescueSessionOverviewRecord BuildOverview(
        SessionRow session,
        OverviewGroups groups,
        string? viewerOpenId)
    {
        var participants = groups.Participants[session.HelpRequestId].ToArray();
        var messages = groups.Messages[session.HelpRequestId].ToArray();
        var viewer = participants.FirstOrDefault(item => item.OpenId == viewerOpenId);
        return new RescueSessionOverviewRecord
        {
            HelpRequestId = session.HelpRequestId,
            CreatedAt = session.CreatedAt,
            ClosedAt = session.ClosedAt,
            ParticipantCount = participants.Length,
            MessageCount = messages.Length,
            LastActivityAt = messages.Length == 0 ? null : messages.Max(item => item.CreatedAt),
            Viewer = viewer is null ? null : BuildViewer(viewer, messages, viewerOpenId!)
        };
    }

    private static RescueSessionViewerOverviewRecord BuildViewer(
        ParticipantRow viewer,
        IReadOnlyCollection<MessageRow> messages,
        string viewerOpenId) => new()
        {
            JoinedAt = viewer.JoinedAt,
            LastReadSeq = viewer.LastReadSeq,
            UnreadCount = messages.Count(item =>
                item.Seq > viewer.LastReadSeq && item.SenderOpenId != viewerOpenId)
        };

    private sealed record OverviewRows(
        SessionRow[] Sessions,
        ParticipantRow[] Participants,
        MessageRow[] Messages);

    private sealed record OverviewGroups(
        ILookup<string, ParticipantRow> Participants,
        ILookup<string, MessageRow> Messages);

    private sealed record SessionRow
    {
        public string HelpRequestId { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? ClosedAt { get; init; }
    }

    private sealed record ParticipantRow
    {
        public string HelpRequestId { get; init; } = string.Empty;
        public string OpenId { get; init; } = string.Empty;
        public DateTimeOffset JoinedAt { get; init; }
        public long LastReadSeq { get; init; }
    }

    private sealed record MessageRow
    {
        public string HelpRequestId { get; init; } = string.Empty;
        public long Seq { get; init; }
        public string SenderOpenId { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
    }
}
