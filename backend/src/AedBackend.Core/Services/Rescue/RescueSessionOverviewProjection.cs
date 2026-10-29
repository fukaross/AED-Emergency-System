using AedBackend.Core.Models.Rescue;

namespace AedBackend.Core.Services.Rescue;

public static class RescueSessionOverviewProjection
{
    public static RescueSessionOverviewRecord FromSession(
        RescueSessionRecord session,
        string? viewerOpenId)
    {
        var viewer = session.Participants.FirstOrDefault(item => item.OpenId == viewerOpenId);
        return new RescueSessionOverviewRecord
        {
            HelpRequestId = session.HelpRequestId,
            CreatedAt = session.CreatedAt,
            ClosedAt = session.ClosedAt,
            ParticipantCount = session.Participants.Count,
            MessageCount = session.Messages.Count,
            LastActivityAt = session.Messages.Count == 0
                ? null
                : session.Messages.Max(item => item.CreatedAt),
            Viewer = viewer is null ? null : new RescueSessionViewerOverviewRecord
            {
                JoinedAt = viewer.JoinedAt,
                LastReadSeq = viewer.LastReadSeq,
                UnreadCount = RescueSessionProjection.UnreadCount(
                    session,
                    viewer.OpenId,
                    viewer.LastReadSeq)
            }
        };
    }
}
