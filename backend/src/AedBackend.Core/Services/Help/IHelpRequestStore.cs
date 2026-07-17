using AedBackend.Core.Models.Help;

namespace AedBackend.Core.Services.Help;

public interface IHelpRequestStore
{
    Task SaveAsync(HelpRequestRecord record, CancellationToken cancellationToken);

    Task<HelpRequestRecord?> FindByIdOrRequestNoAsync(
        string idOrRequestNo,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HelpRequestRecord>> ListByUserAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HelpRequestRecord>> ListAllAsync(CancellationToken cancellationToken);

    Task<bool> HasActiveRequestAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<HelpRequestRecord> AddReportAsync(
        HelpReportUpdate update,
        CancellationToken cancellationToken);

    Task<HelpRequestRecord> CloseAsync(
        string id,
        string status,
        string? note,
        DateTimeOffset closedAt,
        CancellationToken cancellationToken);

    Task<HelpRequestRecord> TransitionToRespondingAsync(
        string id,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
