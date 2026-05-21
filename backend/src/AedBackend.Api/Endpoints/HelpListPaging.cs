using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Help;
using AedBackend.Core.Models.Help;

namespace AedBackend.Api.Endpoints;

internal static class HelpListPaging
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;

    public static PaginatedData<HelpListItem> ToPage(
        IReadOnlyList<HelpRequestRecord> records,
        int? page,
        int? pageSize)
    {
        var safePage = Math.Max(DefaultPage, page ?? DefaultPage);
        var safePageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, 100);
        var items = records.Select(HelpResponseMapper.ToListItem).ToArray();
        var paged = items.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToArray();
        return new PaginatedData<HelpListItem>(
            paged,
            items.Length,
            safePage,
            safePageSize,
            Math.Max(1, (int)Math.Ceiling(items.Length / (double)safePageSize)));
    }

    public static IReadOnlyList<HelpRequestRecord> FilterByStatus(
        IReadOnlyList<HelpRequestRecord> records,
        string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? records
            : records.Where(item => item.Status == status).ToArray();
    }
}
