namespace AedBackend.Core.Services.User;

public sealed record UserIdentityQuery(int Page = 1, int PageSize = 20);

public sealed record UserIdentityAdminQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Keyword = null);

public sealed record SchoolPersonProfileQuery(
    int Page = 1,
    int PageSize = 20,
    string? PersonType = null,
    string? Keyword = null);

public sealed record UserIdentityPage<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);
