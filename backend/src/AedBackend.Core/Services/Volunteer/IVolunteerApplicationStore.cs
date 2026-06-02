using AedBackend.Core.Models.Volunteer;

namespace AedBackend.Core.Services.Volunteer;

public interface IVolunteerApplicationStore
{
    Task<VolunteerApplicationRecord> SubmitAsync(
        string openId,
        VolunteerApplicationPayloadRecord payload,
        int? derivedFromId,
        CancellationToken cancellationToken);

    Task<VolunteerApplicationRecord?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<VolunteerApplicationPage> ListByUserAsync(
        string openId,
        VolunteerApplicationQuery query,
        CancellationToken cancellationToken);

    Task<VolunteerApplicationPage> ListAllAsync(
        AdminVolunteerApplicationQuery query,
        CancellationToken cancellationToken);
}

public sealed record VolunteerApplicationQuery(int Page = 1, int PageSize = 20);

public sealed record AdminVolunteerApplicationQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Keyword = null);

public sealed record VolunteerApplicationPage(
    IReadOnlyList<VolunteerApplicationRecord> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);
