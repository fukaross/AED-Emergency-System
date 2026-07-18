namespace AedBackend.Infrastructure.Rescue;

internal sealed record JsonRescueMessageDraft
{
    public string Kind { get; init; } = string.Empty;
    public string SenderOpenId { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string? Text { get; init; }
    public string? EventType { get; init; }
    public string? Action { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
