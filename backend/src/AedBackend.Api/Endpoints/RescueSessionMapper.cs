using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Rescue;

namespace AedBackend.Api.Endpoints;

internal static class RescueSessionMapper
{
    public static RescueSessionSnapshotDto ToSnapshot(
        RescueSessionRecord session,
        HelpRequestRecord help,
        string currentOpenId,
        long? afterSeq,
        RescueOptions options,
        DateTimeOffset now)
    {
        var states = RescueSessionProjection.FoldParticipantStates(session);
        var myState = states[currentOpenId];
        var myParticipant = session.Participants.First(p => p.OpenId == currentOpenId);
        var messages = afterSeq is null
            ? session.Messages
            : session.Messages.Where(message => message.Seq > afterSeq.Value).ToArray();

        return new RescueSessionSnapshotDto(
            help.Id,
            help.RequestNo,
            help.Status,
            new RescueSosDto(help.Lat, help.Lng, help.Address),
            new RescueMeDto(currentOpenId, myState.TakenAed, myState.Arrived, myParticipant.LastReadSeq),
            session.Participants
                .Select(p => ToParticipant(p, states[p.OpenId], options, now))
                .ToArray(),
            messages.Select(ToMessage).ToArray(),
            LatestSeqOf(session),
            now);
    }

    public static RescueMessageDto ToMessage(RescueSessionMessageRecord message)
    {
        return new RescueMessageDto(
            message.Id,
            message.Seq,
            message.Kind,
            message.SenderOpenId,
            message.SenderName,
            message.Text,
            message.EventType,
            message.Action,
            message.CreatedAt);
    }

    public static long LatestSeqOf(RescueSessionRecord session)
    {
        return session.Messages.Count == 0 ? 0 : session.Messages.Max(message => message.Seq);
    }

    /** 管理端监控视角的参与者列表（含 openId，供管理端只读监控与存档）。 */
    public static IReadOnlyList<RescueParticipantDto> ToParticipants(
        RescueSessionRecord session,
        RescueOptions options,
        DateTimeOffset now)
    {
        var states = RescueSessionProjection.FoldParticipantStates(session);
        return session.Participants
            .Select(p => ToParticipant(p, states[p.OpenId], options, now))
            .ToArray();
    }

    /** 求助人只读实时视图：不含 openId 与文字消息（BackEnd.md §4.5A rescue-view 契约）。 */
    public static RescueViewDto ToRescueView(
        HelpRequestRecord help,
        RescueSessionRecord? session,
        RescueOptions options,
        DateTimeOffset now)
    {
        var states = session is null
            ? new Dictionary<string, RescueParticipantState>()
            : RescueSessionProjection.FoldParticipantStates(session);
        return new RescueViewDto(
            help.Id,
            help.Status,
            new RescueSosDto(help.Lat, help.Lng, help.Address),
            session?.Participants.Count ?? 0,
            session?.Participants
                .Select(p => ToViewParticipant(p, states[p.OpenId], options, now))
                .ToArray() ?? [],
            session?.Messages
                .Where(m => m.Kind == RescueMessageKinds.StatusEvent)
                .Select(m => new RescueViewEventDto(m.Seq, m.EventType!, m.Action!, m.SenderName, m.CreatedAt))
                .ToArray() ?? [],
            now);
    }

    private static RescueParticipantDto ToParticipant(
        RescueSessionParticipantRecord participant,
        RescueParticipantState state,
        RescueOptions options,
        DateTimeOffset now)
    {
        var signal = RescueSessionProjection.SignalOf(
            participant.LastSeenAt,
            now,
            options.SignalLostAfterSeconds,
            options.LocationGoneAfterSeconds);
        return new RescueParticipantDto(
            participant.OpenId,
            participant.DisplayName,
            participant.JoinedAt,
            state.TakenAed,
            state.Arrived,
            signal,
            ToLocation(participant, signal));
    }

    private static RescueViewParticipantDto ToViewParticipant(
        RescueSessionParticipantRecord participant,
        RescueParticipantState state,
        RescueOptions options,
        DateTimeOffset now)
    {
        var signal = RescueSessionProjection.SignalOf(
            participant.LastSeenAt,
            now,
            options.SignalLostAfterSeconds,
            options.LocationGoneAfterSeconds);
        return new RescueViewParticipantDto(
            participant.DisplayName,
            state.TakenAed,
            state.Arrived,
            signal,
            ToLocation(participant, signal));
    }

    private static RescueLocationDto? ToLocation(RescueSessionParticipantRecord participant, string signal)
    {
        if (signal == RescueSignalStates.Gone
            || participant.LastLat is null
            || participant.LastLng is null
            || participant.LocationReportedAt is null)
        {
            return null;
        }

        return new RescueLocationDto(
            participant.LastLat.Value,
            participant.LastLng.Value,
            participant.LocationReportedAt.Value);
    }
}
