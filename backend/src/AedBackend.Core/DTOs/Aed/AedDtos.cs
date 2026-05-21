namespace AedBackend.Core.DTOs.Aed;

public sealed record AedItemResponse(
    string Id,
    string Name,
    double Lat,
    double Lng,
    string Status,
    int? Distance,
    string Image,
    string Address,
    string LastMaintenance,
    string NextMaintenance,
    string Maintainer);
