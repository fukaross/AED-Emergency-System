namespace AedBackend.Core.Models.Rescue;

public sealed record RescueSessionOverviewRecord
{
    public string HelpRequestId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public int ParticipantCount { get; init; }
    public int MessageCount { get; init; }
    public DateTimeOffset? LastActivityAt { get; init; }
    public RescueSessionViewerOverviewRecord? Viewer { get; init; }
}

public sealed record RescueSessionViewerOverviewRecord
{
    public DateTimeOffset JoinedAt { get; init; }
    public long LastReadSeq { get; init; }
    public int UnreadCount { get; init; }
}
