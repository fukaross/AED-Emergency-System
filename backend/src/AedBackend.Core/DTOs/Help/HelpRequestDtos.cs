using AedBackend.Core.DTOs.WeChat;

namespace AedBackend.Core.DTOs.Help;

public sealed record PublishHelpRequest(
    double Lat,
    double Lng,
    string Address,
    string Content,
    string Type,
    IReadOnlyList<string>? Images,
    string? RequestChannel,
    string? GuestName,
    bool? CallbackAllowed,
    DateTimeOffset? PrivacyConsentAt);

public sealed record PublishHelpResponse(
    string Id,
    string RequestNo,
    string Status,
    DateTimeOffset CreatedAt,
    string? FollowupCode,
    string? CloseToken,
    HelpNotificationResult Notification);

public sealed record HelpNotificationResult(
    int TargetCount,
    IReadOnlyList<BroadcastRecipientResult> Recipients);

public sealed record HelpListItem(
    string Id,
    string RequestNo,
    string Content,
    string Type,
    string Status,
    string Address,
    DateTimeOffset CreatedAt,
    int RespondersCount,
    string LatestEta);

public sealed record HelpResponderResponse(
    string Name,
    string Role,
    double Distance,
    string EstimatedArrivalTime);

public sealed record HelpTimelineEntryResponse(
    string Action,
    DateTimeOffset Timestamp,
    string? Note);

public sealed record HelpReportRequest(
    IReadOnlyList<string>? Images,
    string RecordText,
    bool IsFalseAlarm);

public sealed record HelpReportResponse(
    string Id,
    string Status,
    DateTimeOffset SubmittedAt);

public sealed record HelpReportRecordResponse(
    string Id,
    IReadOnlyList<string> Images,
    string RecordText,
    bool IsFalseAlarm,
    DateTimeOffset SubmittedAt);

public sealed record HelpDetailResponse(
    string Id,
    string RequestNo,
    string Content,
    string Type,
    string Status,
    string Address,
    double Lat,
    double Lng,
    string RequesterPhone,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Images,
    IReadOnlyList<HelpResponderResponse> Responders,
    string EstimatedArrivalTime,
    IReadOnlyList<HelpTimelineEntryResponse> Timeline,
    IReadOnlyList<HelpReportRecordResponse> Reports);

public sealed record GuestHelpLookupRequest(string RequestNo, string FollowupCode);

public sealed record GuestHelpLookupResponse(
    string RequestNo,
    string Status,
    string Content,
    string Address,
    DateTimeOffset CreatedAt,
    int RespondersCount,
    string? LatestEta,
    bool CanClose,
    DateTimeOffset? ClosedAt);

public sealed record GuestCloseHelpRequest(string RequestNo, string CloseToken, string? Note);

public sealed record GuestCloseHelpResponse(
    string RequestNo,
    string Status,
    DateTimeOffset ClosedAt);

public sealed record CloseHelpRequest(string Status, string? Note);
