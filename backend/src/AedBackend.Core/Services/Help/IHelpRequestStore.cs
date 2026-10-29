using AedBackend.Core.Models.Help;

namespace AedBackend.Core.Services.Help;

public interface IHelpRequestStore
{
    Task<bool> TrySaveIfNoActiveAsync(
        HelpRequestRecord record,
        CancellationToken cancellationToken);

    Task<HelpRequestRecord?> FindByIdOrRequestNoAsync(
        string idOrRequestNo,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HelpRequestRecord>> ListByUserAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HelpRequestRecord>> ListAllAsync(CancellationToken cancellationToken);

    Task<HelpRequestOverviewPage> ListOverviewsAsync(
        HelpRequestOverviewQuery query,
        CancellationToken cancellationToken);

    Task<HelpRequestRecord> AddReportAsync(
        HelpReportUpdate update,
        CancellationToken cancellationToken);

    Task<HelpRequestRecord> CloseAsync(
        HelpClosureUpdate update,
        CancellationToken cancellationToken);

    Task<HelpRequestRecord> TransitionToRespondingAsync(
        string id,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public enum HelpRequestOverviewScope
{
    Active,
    History
}

public sealed record HelpRequestOverviewQuery
{
    public required HelpRequestOverviewScope Scope { get; init; }
    public int Offset { get; init; }
    public int? Limit { get; init; }

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(Offset);
        if (Limit is <= 0) throw new ArgumentOutOfRangeException(nameof(Limit));
    }
}

public sealed record HelpRequestOverviewRecord
{
    public string Id { get; init; } = string.Empty;
    public string RequestNo { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record HelpClosureUpdate
{
    public required string HelpRequestId { get; init; }
    public required string Status { get; init; }
    public string? Note { get; init; }
    public required DateTimeOffset ClosedAt { get; init; }
}

public sealed record HelpRequestOverviewPage(
    IReadOnlyList<HelpRequestOverviewRecord> Items,
    int Total);
