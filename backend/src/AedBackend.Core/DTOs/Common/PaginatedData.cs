namespace AedBackend.Core.DTOs.Common;

public sealed record PaginatedData<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);
