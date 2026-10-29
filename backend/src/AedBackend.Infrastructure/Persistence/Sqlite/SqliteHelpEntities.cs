namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class HelpRequestEntity
{
    public string Id { get; set; } = string.Empty;
    public string RequestNo { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string RequestChannel { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? CloseNote { get; set; }
    public string RequesterPhone { get; set; } = string.Empty;
    public HelpGuestSessionEntity? GuestSession { get; set; }
    public List<HelpImageEntity> Images { get; set; } = new();
    public List<HelpResponderEntity> Responders { get; set; } = new();
    public List<HelpTimelineEntity> Timeline { get; set; } = new();
    public List<HelpReportEntity> Reports { get; set; } = new();
}

internal sealed class HelpImageEntity
{
    public int Id { get; set; }
    public string HelpRequestId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Url { get; set; } = string.Empty;
}

internal sealed class HelpGuestSessionEntity
{
    public string HelpRequestId { get; set; } = string.Empty;
    public string? GuestName { get; set; }
    public string FollowupCode { get; set; } = string.Empty;
    public string CloseToken { get; set; } = string.Empty;
    public string AnonymousId { get; set; } = string.Empty;
    public bool CallbackAllowed { get; set; }
    public DateTimeOffset? PrivacyConsentAt { get; set; }
}

internal sealed class HelpResponderEntity
{
    public int Id { get; set; }
    public string HelpRequestId { get; set; } = string.Empty;
    public string OpenId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public int? EtaSeconds { get; set; }
    public DateTimeOffset RespondedAt { get; set; }
}

internal sealed class HelpTimelineEntity
{
    public int Id { get; set; }
    public string HelpRequestId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string? Note { get; set; }
}

internal sealed class HelpReportEntity
{
    public string Id { get; set; } = string.Empty;
    public string HelpRequestId { get; set; } = string.Empty;
    public string? ReporterOpenId { get; set; }
    public string RecordText { get; set; } = string.Empty;
    public bool IsFalseAlarm { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public List<HelpReportImageEntity> Images { get; set; } = new();
}

internal sealed class HelpReportImageEntity
{
    public int Id { get; set; }
    public string HelpReportId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Url { get; set; } = string.Empty;
}
