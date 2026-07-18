using AedBackend.Core.Models.Rescue;

namespace AedBackend.Core.Services.Rescue;

public interface IRescueSessionStore
{
    Task<RescueSessionRecord?> FindByHelpIdAsync(string helpRequestId, CancellationToken ct);

    Task<RescueSessionParticipantAccess?> FindParticipantAccessAsync(
        RescueSessionParticipantQuery query,
        CancellationToken ct);

    Task<RescueSessionRecord> GetOrCreateAsync(string helpRequestId, DateTimeOffset now, CancellationToken ct);

    Task<RescueSessionJoinResult> JoinAsync(RescueSessionJoinCommand command, CancellationToken ct);

    Task<RescueSessionMessageRecord> AppendMessageAsync(
        RescueTextMessageCommand command,
        CancellationToken ct);

    Task<RescueSessionStatusAppendResult> AppendStatusEventAsync(
        RescueSessionStatusCommand command,
        CancellationToken ct);

    Task<RescueSessionParticipantRecord> UpdateLocationAsync(
        RescueSessionLocationCommand command,
        CancellationToken ct);

    Task<long> MarkReadAsync(RescueSessionReadCommand command, CancellationToken ct);

    Task CloseSessionAsync(
        string helpRequestId,
        DateTimeOffset closedAt,
        CancellationToken ct);

    Task<IReadOnlyList<RescueSessionOverviewRecord>> ListOverviewsAsync(
        RescueSessionOverviewQuery query,
        CancellationToken ct);
}

public sealed record RescueSessionOverviewQuery
{
    public required IReadOnlyCollection<string> HelpRequestIds { get; init; }
    public string? ViewerOpenId { get; init; }
}

public sealed record RescueSessionParticipantQuery
{
    public required string HelpRequestId { get; init; }
    public required string OpenId { get; init; }
}

public sealed record RescueSessionParticipantAccess(
    DateTimeOffset? ClosedAt,
    RescueSessionParticipantRecord Participant);

public sealed record RescueSessionJoinCommand
{
    public required string HelpRequestId { get; init; }
    public required string OpenId { get; init; }
    public required string DisplayName { get; init; }
    public required DateTimeOffset Now { get; init; }
}

public sealed record RescueSessionJoinResult(
    RescueSessionRecord Session,
    bool IsNewParticipant,
    RescueSessionMessageRecord? JoinedEvent);

public sealed record RescueTextMessageCommand
{
    public required string HelpRequestId { get; init; }
    public required string SenderOpenId { get; init; }
    public required string SenderName { get; init; }
    public required string Text { get; init; }
    public required DateTimeOffset Now { get; init; }
}

public sealed record RescueSessionStatusCommand
{
    public required string HelpRequestId { get; init; }
    public required string SenderOpenId { get; init; }
    public required string SenderName { get; init; }
    public required string EventType { get; init; }
    public required string Action { get; init; }
    public required DateTimeOffset Now { get; init; }
}

public sealed record RescueSessionStatusAppendResult(
    bool Appended,
    RescueSessionMessageRecord? Event,
    RescueParticipantState State);

public sealed record RescueSessionLocationCommand
{
    public required string HelpRequestId { get; init; }
    public required string OpenId { get; init; }
    public required double Lat { get; init; }
    public required double Lng { get; init; }
    public required DateTimeOffset Now { get; init; }
}

public sealed record RescueSessionReadCommand
{
    public required string HelpRequestId { get; init; }
    public required string OpenId { get; init; }
    public required long LastReadSeq { get; init; }
}

public sealed class RescueSessionClosedException : InvalidOperationException
{
    public RescueSessionClosedException(string helpRequestId)
        : base($"急救会话已关闭: {helpRequestId}")
    {
        HelpRequestId = helpRequestId;
    }

    public string HelpRequestId { get; }
}
