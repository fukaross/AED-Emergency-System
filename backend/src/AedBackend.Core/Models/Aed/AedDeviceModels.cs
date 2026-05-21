namespace AedBackend.Core.Models.Aed;

public sealed record AedDeviceRecord
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public double Lat { get; init; }
    public double Lng { get; init; }
    public string Status { get; init; } = "online";
    public string Image { get; init; } = "/static/images/aed-placeholder.png";
    public string Address { get; init; } = string.Empty;
    public string LastMaintenance { get; init; } = string.Empty;
    public string NextMaintenance { get; init; } = string.Empty;
    public string Maintainer { get; init; } = string.Empty;
}

public sealed record NearbyAedDevice(AedDeviceRecord Device, int Distance);
