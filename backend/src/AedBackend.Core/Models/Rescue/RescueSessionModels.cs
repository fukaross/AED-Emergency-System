namespace AedBackend.Core.Models.Rescue;

public static class RescueMessageKinds
{
    public const string Text = "text";
    public const string StatusEvent = "status_event";
    public const string Image = "image";
}

public static class RescueEventTypes
{
    public const string Joined = "joined";
    public const string TakenAed = "taken_aed";
    public const string Arrived = "arrived";
    public const string Offline = "offline";
    public const string Online = "online";
}

public static class RescueEventActions
{
    public const string Mark = "mark";
    public const string Revoke = "revoke";
}

public static class RescueSignalStates
{
    public const string Ok = "ok";
    public const string Lost = "lost";
    public const string Gone = "gone";
}

public sealed record RescueSessionRecord
{
    public string HelpRequestId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public IReadOnlyList<RescueSessionParticipantRecord> Participants { get; init; } = [];
    public IReadOnlyList<RescueSessionMessageRecord> Messages { get; init; } = [];
}

public sealed record RescueSessionParticipantRecord
{
    public string OpenId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
    public DateTimeOffset JoinedAt { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
    public double? LastLat { get; init; }
    public double? LastLng { get; init; }
    public DateTimeOffset? LocationReportedAt { get; init; }
    public long LastReadSeq { get; init; }
}

public sealed record RescueSessionMessageRecord
{
    public string Id { get; init; } = string.Empty;
    public long Seq { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string SenderOpenId { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string? Text { get; init; }
    public string? ImageUrl { get; init; }
    public string? EventType { get; init; }
    public string? Action { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
