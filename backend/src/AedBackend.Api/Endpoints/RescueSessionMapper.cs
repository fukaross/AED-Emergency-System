using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Rescue;
using System.Security.Cryptography;
using System.Text;

namespace AedBackend.Api.Endpoints;

internal sealed record RescueMappingContext(RescueOptions Options, DateTimeOffset Now);

internal sealed record RescueSnapshotMappingInput
{
    public required RescueSessionRecord Session { get; init; }
    public required HelpRequestRecord Help { get; init; }
    public required string CurrentOpenId { get; init; }
    public long? AfterSeq { get; init; }
    public required RescueMappingContext Context { get; init; }
}

internal sealed record RescueViewMappingInput
{
    public required HelpRequestRecord Help { get; init; }
    public required RescueSessionRecord? Session { get; init; }
    public required RescueMappingContext Context { get; init; }
}

internal static class RescueSessionMapper
{
    public static RescueSessionSnapshotDto ToSnapshot(RescueSnapshotMappingInput input)
    {
        var states = RescueSessionProjection.FoldParticipantStates(input.Session);
        var myState = states[input.CurrentOpenId];
        var myParticipant = input.Session.Participants.First(p => p.OpenId == input.CurrentOpenId);
        var messages = input.AfterSeq is null
            ? input.Session.Messages
            : input.Session.Messages.Where(message => message.Seq > input.AfterSeq.Value).ToArray();

        return new RescueSessionSnapshotDto(
            input.Help.Id,
            input.Help.RequestNo,
            input.Help.Status,
            new RescueSosDto(input.Help.Lat, input.Help.Lng, input.Help.Address),
            new RescueMeDto(input.CurrentOpenId, myState.TakenAed, myState.Arrived, myParticipant.LastReadSeq),
            input.Session.Participants
                .Select(participant => ToParticipant(new ParticipantMappingInput
                {
                    Participant = participant,
                    State = states[participant.OpenId],
                    Context = input.Context,
                }))
                .ToArray(),
            messages.Select(ToMessage).ToArray(),
            LatestSeqOf(input.Session),
            ToTiming(input.Context.Options),
            input.Context.Now);
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
        var context = new RescueMappingContext(options, now);
        var states = RescueSessionProjection.FoldParticipantStates(session);
        return session.Participants
            .Select(participant => ToParticipant(new ParticipantMappingInput
            {
                Participant = participant,
                State = states[participant.OpenId],
                Context = context,
            }))
            .ToArray();
    }

    /** 求助人只读实时视图：不含 openId 与文字消息（BackEnd.md §4.5A rescue-view 契约）。 */
    public static RescueViewDto ToRescueView(RescueViewMappingInput input)
    {
        var states = input.Session is null
            ? new Dictionary<string, RescueParticipantState>()
            : RescueSessionProjection.FoldParticipantStates(input.Session);
        var participantIds = CreateStableParticipantIds(input.Help.Id, input.Session);
        var participants = input.Session?.Participants
            .Select(participant => ToViewParticipant(new ViewParticipantMappingInput
            {
                ParticipantId = participantIds[participant.OpenId],
                Participant = participant,
                State = states[participant.OpenId],
                Context = input.Context,
            }))
            .ToArray() ?? [];
        return new RescueViewDto(
            input.Help.Id,
            input.Help.Status,
            new RescueSosDto(input.Help.Lat, input.Help.Lng, input.Help.Address),
            input.Session?.Participants.Count ?? 0,
            participants,
            input.Session?.Messages
                .Where(m => m.Kind == RescueMessageKinds.StatusEvent)
                .Select(m => new RescueViewEventDto(m.Seq, m.EventType!, m.Action!, m.SenderName, m.CreatedAt))
                .ToArray() ?? [],
            ToTiming(input.Context.Options),
            input.Context.Now);
    }

    /** joined 事件的单调顺序决定匿名编号；仅损坏的历史数据使用会话内稳定哈希。 */
    private static IReadOnlyDictionary<string, string> CreateStableParticipantIds(
        string helpId,
        RescueSessionRecord? session)
    {
        if (session is null) return new Dictionary<string, string>();
        var joinedIds = session.Messages
            .Where(message => message.Kind == RescueMessageKinds.StatusEvent
                && message.EventType == RescueEventTypes.Joined
                && message.Action == RescueEventActions.Mark)
            .OrderBy(message => message.Seq)
            .Select((message, index) => new { message.SenderOpenId, Id = $"p-{index + 1}" })
            .GroupBy(item => item.SenderOpenId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);
        foreach (var participant in session.Participants)
        {
            joinedIds.TryAdd(participant.OpenId, CreateFallbackParticipantId(helpId, participant.OpenId));
        }
        return joinedIds;
    }

    private static string CreateFallbackParticipantId(string helpId, string openId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{helpId}\n{openId}"));
        return $"p-{Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant()}";
    }

    private static RescueTimingDto ToTiming(RescueOptions options)
    {
        return new RescueTimingDto(
            options.SignalLostAfterSeconds,
            options.LocationGoneAfterSeconds,
            options.LocationReportIntervalSeconds);
    }

    public static RescueParticipantDto ToParticipant(ParticipantMappingInput input)
    {
        var signal = RescueSessionProjection.SignalOf(
            input.Participant.LocationReportedAt,
            input.Context.Now,
            input.Context.Options.SignalLostAfterSeconds,
            input.Context.Options.LocationGoneAfterSeconds);
        return new RescueParticipantDto(
            input.Participant.OpenId,
            input.Participant.DisplayName,
            input.Participant.JoinedAt,
            input.State.TakenAed,
            input.State.Arrived,
            signal,
            ToLocation(input.Participant, signal));
    }

    private static RescueViewParticipantDto ToViewParticipant(ViewParticipantMappingInput input)
    {
        var signal = RescueSessionProjection.SignalOf(
            input.Participant.LocationReportedAt,
            input.Context.Now,
            input.Context.Options.SignalLostAfterSeconds,
            input.Context.Options.LocationGoneAfterSeconds);
        return new RescueViewParticipantDto(
            input.ParticipantId,
            input.Participant.DisplayName,
            input.State.TakenAed,
            input.State.Arrived,
            signal,
            ToLocation(input.Participant, signal));
    }

    private sealed record ViewParticipantMappingInput
    {
        public required string ParticipantId { get; init; }
        public required RescueSessionParticipantRecord Participant { get; init; }
        public required RescueParticipantState State { get; init; }
        public required RescueMappingContext Context { get; init; }
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

internal sealed record ParticipantMappingInput
{
    public required RescueSessionParticipantRecord Participant { get; init; }
    public required RescueParticipantState State { get; init; }
    public required RescueMappingContext Context { get; init; }
}
