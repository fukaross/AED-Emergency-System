namespace AedBackend.Core.Models.Aed;

public sealed record AedDeviceRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string Status { get; set; } = "online";
    public string? StatusNote { get; set; }
    public string Image { get; set; } = "/static/images/aed-placeholder.png";
    public string Address { get; set; } = string.Empty;
    public string LastMaintenance { get; set; } = string.Empty;
    public string NextMaintenance { get; set; } = string.Empty;
    public string Maintainer { get; set; } = string.Empty;
}

public sealed record NearbyAedDevice(AedDeviceRecord Device, int Distance);
