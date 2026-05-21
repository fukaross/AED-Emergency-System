namespace AedBackend.Core.Models.Help;

public static class HelpStatuses
{
    public const string Published = "published";
    public const string Responding = "responding";
    public const string PendingReview = "pending_review";
    public const string Closed = "closed";
    public const string FalseAlarm = "false_alarm";

    public static readonly IReadOnlySet<string> Active = new HashSet<string>
    {
        Published,
        Responding,
        PendingReview
    };

    public static bool IsClosed(string status)
    {
        return status is Closed or FalseAlarm;
    }
}

public sealed record HelpRequestRecord
{
    public string Id { get; init; } = string.Empty;
    public string RequestNo { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string RequestChannel { get; init; } = "user";
    public double Lat { get; init; }
    public double Lng { get; init; }
    public string Address { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Type { get; init; } = "general";
    public string Status { get; init; } = HelpStatuses.Published;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public string? CloseNote { get; init; }
    public string RequesterPhone { get; init; } = string.Empty;
    public IReadOnlyList<string> Images { get; init; } = [];
    public HelpGuestSessionRecord? GuestSession { get; init; }
    public IReadOnlyList<HelpResponderRecord> Responders { get; init; } = [];
    public IReadOnlyList<HelpTimelineRecord> Timeline { get; init; } = [];
    public IReadOnlyList<HelpReportRecord> Reports { get; init; } = [];
}

public sealed record HelpGuestSessionRecord(
    string? GuestName,
    string FollowupCode,
    string CloseToken,
    string AnonymousId,
    bool CallbackAllowed,
    DateTimeOffset? PrivacyConsentAt);

public sealed record HelpResponderRecord(
    string OpenId,
    string Name,
    string Role,
    double Lat,
    double Lng,
    int? EtaSeconds,
    DateTimeOffset RespondedAt);

public sealed record HelpTimelineRecord(
    string Action,
    DateTimeOffset Timestamp,
    string? Note);

public sealed record HelpReportRecord(
    string Id,
    IReadOnlyList<string> Images,
    string RecordText,
    bool IsFalseAlarm,
    DateTimeOffset SubmittedAt);

public sealed record HelpReportUpdate(
    string HelpRequestId,
    HelpReportRecord Report,
    string NextStatus,
    DateTimeOffset UpdatedAt);
