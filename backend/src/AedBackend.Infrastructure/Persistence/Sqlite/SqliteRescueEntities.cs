namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class RescueSessionEntity
{
    public string HelpRequestId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public List<RescueSessionParticipantEntity> Participants { get; set; } = new();
    public List<RescueSessionMessageEntity> Messages { get; set; } = new();
}

internal sealed class RescueSessionParticipantEntity
{
    public int Id { get; set; }
    public string HelpRequestId { get; set; } = string.Empty;
    public string OpenId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public double? LastLat { get; set; }
    public double? LastLng { get; set; }
    public DateTimeOffset? LocationReportedAt { get; set; }
    public long LastReadSeq { get; set; }
}

internal sealed class RescueSessionMessageEntity
{
    public string Id { get; set; } = string.Empty;
    public string HelpRequestId { get; set; } = string.Empty;
    public long Seq { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string SenderOpenId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string? Text { get; set; }
    public string? EventType { get; set; }
    public string? Action { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
