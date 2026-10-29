using AedBackend.Core.Models.Rescue;

namespace AedBackend.Core.Services.Rescue;

public sealed record RescuePresenceCommand(
    string OpenId,
    string DisplayName,
    string EventType);

/** 离线/上线事件规划：仅在状态跃迁时产生命令，避免重复追加。 */
public static class RescuePresencePlanner
{
    public static IReadOnlyList<RescuePresenceCommand> Plan(
        RescueSessionRecord session,
        DateTimeOffset now,
        int offlineAfterSeconds)
    {
        var commands = new List<RescuePresenceCommand>();
        foreach (var participant in session.Participants)
        {
            var lastActive = participant.LocationReportedAt ?? participant.JoinedAt;
            var isStale = (now - lastActive).TotalSeconds >= offlineAfterSeconds;
            var latest = LatestPresenceEvent(session, participant.OpenId);
            if (isStale && latest?.EventType != RescueEventTypes.Offline)
            {
                commands.Add(new RescuePresenceCommand(
                    participant.OpenId,
                    participant.DisplayName,
                    RescueEventTypes.Offline));
            }
            else if (!isStale && latest?.EventType == RescueEventTypes.Offline)
            {
                commands.Add(new RescuePresenceCommand(
                    participant.OpenId,
                    participant.DisplayName,
                    RescueEventTypes.Online));
            }
        }

        return commands;
    }

    private static RescueSessionMessageRecord? LatestPresenceEvent(
        RescueSessionRecord session,
        string openId)
    {
        return session.Messages
            .Where(message => message.Kind == RescueMessageKinds.StatusEvent
                && message.SenderOpenId == openId
                && (message.EventType == RescueEventTypes.Offline
                    || message.EventType == RescueEventTypes.Online))
            .MaxBy(message => message.Seq);
    }
}
