using AedBackend.Core.Models.Rescue;

namespace AedBackend.Core.Services.Rescue;

public sealed record RescueParticipantState(bool TakenAed, bool Arrived);

/**
 * 会话派生状态的纯函数投影：折叠规则在服务端 Core 层统一实现，客户端不重复判定（BackEnd.md §4.5A）。
 */
public static class RescueSessionProjection
{
    /**
     * 每名参与者对每种 eventType 取最新一条状态事件的 action，mark 为生效、revoke 为已撤销。
     */
    public static IReadOnlyDictionary<string, RescueParticipantState> FoldParticipantStates(
        RescueSessionRecord session)
    {
        var states = new Dictionary<string, RescueParticipantState>(StringComparer.Ordinal);
        foreach (var participant in session.Participants)
        {
            states[participant.OpenId] = new RescueParticipantState(
                IsMarked(session.Messages, participant.OpenId, RescueEventTypes.TakenAed),
                IsMarked(session.Messages, participant.OpenId, RescueEventTypes.Arrived));
        }
        return states;
    }

    public static string SignalOf(
        DateTimeOffset? locationReportedAt,
        DateTimeOffset now,
        int lostAfterSeconds,
        int goneAfterSeconds)
    {
        if (locationReportedAt is null) return RescueSignalStates.Gone;

        var elapsed = now - locationReportedAt.Value;
        if (elapsed <= TimeSpan.FromSeconds(lostAfterSeconds)) return RescueSignalStates.Ok;
        if (elapsed <= TimeSpan.FromSeconds(goneAfterSeconds)) return RescueSignalStates.Lost;
        return RescueSignalStates.Gone;
    }

    public static int UnreadCount(RescueSessionRecord session, string openId, long lastReadSeq)
    {
        return session.Messages.Count(message =>
            message.Seq > lastReadSeq && message.SenderOpenId != openId);
    }

    private static bool IsMarked(
        IReadOnlyList<RescueSessionMessageRecord> messages,
        string openId,
        string eventType)
    {
        var latest = messages
            .Where(message => message.Kind == RescueMessageKinds.StatusEvent
                && message.SenderOpenId == openId
                && message.EventType == eventType)
            .OrderByDescending(message => message.Seq)
            .FirstOrDefault();
        return latest is not null && latest.Action == RescueEventActions.Mark;
    }
}
