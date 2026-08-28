namespace AedBackend.Core.DTOs.Rescue;

public sealed record RescueSosDto(double Lat, double Lng, string Address);

public sealed record RescueLocationDto(double Lat, double Lng, DateTimeOffset ReportedAt);

public sealed record RescueParticipantDto(
    string OpenId,
    string DisplayName,
    string? AvatarUrl,
    DateTimeOffset JoinedAt,
    bool TakenAed,
    bool Arrived,
    string Signal,
    RescueLocationDto? Location);

public sealed record RescueMeDto(
    string OpenId,
    bool TakenAed,
    bool Arrived,
    long LastReadSeq);

public sealed record RescueMessageDto(
    string Id,
    long Seq,
    string Kind,
    string SenderOpenId,
    string SenderName,
    string? Text,
    string? EventType,
    string? Action,
    DateTimeOffset CreatedAt,
    string? ImageUrl = null);

public sealed record RescueTimingDto(
    int SignalLostAfterSeconds,
    int LocationGoneAfterSeconds,
    int LocationReportIntervalSeconds);

public sealed record RescueSessionSnapshotDto(
    string HelpId,
    string RequestNo,
    string HelpStatus,
    RescueSosDto Sos,
    RescueMeDto Me,
    IReadOnlyList<RescueParticipantDto> Participants,
    IReadOnlyList<RescueMessageDto> Messages,
    long LatestSeq,
    RescueTimingDto Timing,
    DateTimeOffset ServerTime);

public sealed record SendRescueMessageRequest(string? Kind, string? Text, string? ImageUrl);

public sealed record SendRescueStatusEventRequest(string? EventType, string? Action);

public sealed record RescueMyStateDto(string OpenId, bool TakenAed, bool Arrived);

public sealed record RescueStatusEventResponse(
    bool Appended,
    RescueMessageDto? Event,
    RescueMyStateDto Me);

public sealed record ReportRescueLocationRequest(double? Lat, double? Lng);

public sealed record ReportRescueLocationResponse(DateTimeOffset ReportedAt);

public sealed record RescueViewParticipantDto(
    string ParticipantId,
    string DisplayName,
    bool TakenAed,
    bool Arrived,
    string Signal,
    RescueLocationDto? Location);

public sealed record RescueViewEventDto(
    long Seq,
    string EventType,
    string Action,
    string ActorName,
    DateTimeOffset CreatedAt);

public sealed record RescueViewDto(
    string HelpId,
    string HelpStatus,
    RescueSosDto Sos,
    int RespondersCount,
    IReadOnlyList<RescueViewParticipantDto> Participants,
    IReadOnlyList<RescueViewEventDto> Events,
    RescueTimingDto Timing,
    DateTimeOffset ServerTime);

public sealed record RescueMineItemDto(
    string HelpId,
    string RequestNo,
    string HelpStatus,
    string Type,
    string Address,
    bool Joined,
    DateTimeOffset? JoinedAt,
    int UnreadCount,
    DateTimeOffset? LastActivityAt);

public sealed record RescueMineResponse(IReadOnlyList<RescueMineItemDto> Items);

/** WS 扇出信封（ADR 0001）：非终态载荷仅作变化提示；关闭门后的最终 help_status 是客户端停止实时任务的控制帧。 */
public sealed record RescueSocketEnvelope(string Type, object Data);

public sealed record RescueHelpStatusDto(string HelpStatus);
