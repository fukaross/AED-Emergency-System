using AedBackend.Core.Models.Rescue;

namespace AedBackend.Core.Services.Rescue;

public interface IRescueSessionStore
{
    Task<RescueSessionRecord?> FindByHelpIdAsync(string helpRequestId, CancellationToken ct);

    Task<RescueSessionRecord> GetOrCreateAsync(string helpRequestId, DateTimeOffset now, CancellationToken ct);

    Task<RescueSessionRecord> JoinAsync(RescueSessionJoinCommand command, CancellationToken ct);

    Task<RescueSessionRecord> AppendMessageAsync(RescueSessionAppendCommand command, CancellationToken ct);

    Task<RescueSessionRecord> UpdateLocationAsync(RescueSessionLocationCommand command, CancellationToken ct);

    Task<RescueSessionRecord> MarkReadAsync(
        string helpRequestId,
        string openId,
        long lastReadSeq,
        CancellationToken ct);

    Task CloseSessionAsync(string helpRequestId, CancellationToken ct);

    Task<IReadOnlyList<RescueSessionRecord>> ListByParticipantAsync(string openId, CancellationToken ct);

    Task<IReadOnlyList<RescueSessionRecord>> ListAllAsync(CancellationToken ct);
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

public sealed record RescueSessionLocationCommand(
    string HelpRequestId,
    string OpenId,
    double Lat,
    double Lng,
    DateTimeOffset Now);
