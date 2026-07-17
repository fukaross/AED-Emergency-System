using AedBackend.Core.Models.Rescue;

namespace AedBackend.Core.Services.Rescue;

public interface IRescueSessionStore
{
    Task<RescueSessionRecord?> FindByHelpIdAsync(string helpRequestId, CancellationToken ct);

    Task<RescueSessionRecord> GetOrCreateAsync(string helpRequestId, DateTimeOffset now, CancellationToken ct);

    Task<RescueSessionRecord> JoinAsync(RescueSessionJoinCommand command, CancellationToken ct);

    Task<RescueSessionRecord> AppendMessageAsync(RescueSessionAppendCommand command, CancellationToken ct);

    Task<RescueSessionStatusAppendResult> AppendStatusEventAsync(
        RescueSessionStatusCommand command,
        CancellationToken ct);

    Task<RescueSessionRecord> UpdateLocationAsync(RescueSessionLocationCommand command, CancellationToken ct);

    Task<RescueSessionRecord> MarkReadAsync(
        string helpRequestId,
        string openId,
        long lastReadSeq,
        CancellationToken ct);

    Task CloseSessionAsync(string helpRequestId, CancellationToken ct);

    Task<IReadOnlyList<RescueSessionRecord>> ListByParticipantAsync(string openId, CancellationToken ct);

    Task<IReadOnlyList<RescueSessionRecord>> ListAllAsync(CancellationToken ct);

    Task<IReadOnlyList<RescueSessionOverviewRecord>> ListOverviewsAsync(
        string? viewerOpenId,
        CancellationToken ct);
}

public sealed record RescueSessionJoinCommand(
    string HelpRequestId,
    string OpenId,
    string DisplayName,
    DateTimeOffset Now);

public sealed record RescueSessionAppendCommand(
    string HelpRequestId,
    string Kind,
    string SenderOpenId,
    string SenderName,
    string? Text,
    string? EventType,
    string? Action,
    DateTimeOffset Now);

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
    RescueSessionRecord Session,
    bool Appended,
    RescueSessionMessageRecord? Event);

public sealed record RescueSessionLocationCommand(
    string HelpRequestId,
    string OpenId,
    double Lat,
    double Lng,
    DateTimeOffset Now);

public sealed class RescueSessionClosedException : InvalidOperationException
{
    public RescueSessionClosedException(string helpRequestId)
        : base($"急救会话已关闭: {helpRequestId}")
    {
        HelpRequestId = helpRequestId;
    }

    public string HelpRequestId { get; }
}
