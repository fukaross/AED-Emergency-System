namespace AedBackend.Core.DTOs.Admin;

public sealed record AdminAedDeviceListItem(
    string Id,
    string Name,
    double Lat,
    double Lng,
    string Status,
    string? StatusNote,
    string Image,
    string Address,
    string LastMaintenance,
    string NextMaintenance,
    string Maintainer);

public sealed record AdminAedDeviceListResponse(
    IReadOnlyList<AdminAedDeviceListItem> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record CreateAedDeviceRequest(
    string Name,
    double Lat,
    double Lng,
    string? Image,
    string? Address,
    string? Status,
    string? StatusNote,
    string? LastMaintenance,
    string? NextMaintenance,
    string? Maintainer);

public sealed record UpdateAedDeviceRequest(
    string? Name,
    double? Lat,
    double? Lng,
    string? Status,
    string? StatusNote,
    string? Image,
    string? Address,
    string? LastMaintenance,
    string? NextMaintenance,
    string? Maintainer);
