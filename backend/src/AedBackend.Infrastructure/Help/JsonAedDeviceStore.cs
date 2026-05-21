using System.Text.Json;
using AedBackend.Core.Models.Aed;
using AedBackend.Core.Services.Help;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Help;

// [Transitional: JSON -> MySQL] Static AED seed mirrors Design/README.md §5.
public sealed class JsonAedDeviceStore : IAedDeviceStore
{
    private const double EarthRadiusMeters = 6371000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonAedDeviceStore(IOptions<WeChatOptions> options)
    {
        filePath = Path.GetFullPath(options.Value.AedDevicesFilePath);
    }

    public async Task<IReadOnlyList<NearbyAedDevice>> GetNearbyAsync(
        double lat,
        double lng,
        int? radiusMeters,
        CancellationToken cancellationToken)
    {
        var devices = await LoadDevicesAsync(cancellationToken);
        var nearby = devices.Select(item => ToNearby(item, lat, lng));
        if (radiusMeters is > 0)
        {
            nearby = nearby.Where(item => item.Distance <= radiusMeters.Value);
        }

        return nearby
            .OrderByDescending(item => item.Device.Status == "online")
            .ThenBy(item => item.Distance)
            .ToArray();
    }

    public async Task<AedDeviceRecord?> FindByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var devices = await LoadDevicesAsync(cancellationToken);
        return devices.FirstOrDefault(item =>
            item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<AedDeviceRecord>> LoadDevicesAsync(
        CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
            {
                await WriteDevicesAsync(DefaultDevices, cancellationToken);
                return DefaultDevices;
            }

            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<IReadOnlyList<AedDeviceRecord>>(
                stream,
                JsonOptions,
                cancellationToken) ?? [];
        }
        finally
        {
            fileLock.Release();
        }
    }

    private static NearbyAedDevice ToNearby(AedDeviceRecord device, double lat, double lng)
    {
        var distance = (int)Math.Round(CalculateDistance(lat, lng, device.Lat, device.Lng));
        return new NearbyAedDevice(device, distance);
    }

    private static double CalculateDistance(
        double fromLat,
        double fromLng,
        double toLat,
        double toLng)
    {
        var deltaLat = ToRadians(toLat - fromLat);
        var deltaLng = ToRadians(toLng - fromLng);
        var a = Math.Pow(Math.Sin(deltaLat / 2), 2)
            + Math.Cos(ToRadians(fromLat))
            * Math.Cos(ToRadians(toLat))
            * Math.Pow(Math.Sin(deltaLng / 2), 2);

        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    private async Task WriteDevicesAsync(
        IReadOnlyList<AedDeviceRecord> devices,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, devices, JsonOptions, cancellationToken);
    }

    private static readonly IReadOnlyList<AedDeviceRecord> DefaultDevices =
    [
        Create("AED-01", "REDACTED", 19.974447, 110.517041, "online"),
        Create("AED-02", "REDACTED", 19.975735, 110.517149, "online"),
        Create("AED-03", "REDACTED", 19.975388, 110.517049, "online"),
        Create("AED-04", "REDACTED", 19.974972, 110.517038, "online"),
        Create("AED-05", "REDACTED", 19.974480, 110.517032, "online"),
        Create("AED-06", "图书馆", 19.973585, 110.516800, "offline"),
        Create("AED-07", "REDACTED", 19.973603, 110.517880, "online"),
        Create("AED-08", "REDACTED", 19.973257, 110.517849, "online"),
        Create("AED-09", "REDACTED", 19.972782, 110.517886, "online"),
        Create("AED-10", "美术楼", 19.974215, 110.518559, "online"),
        Create("AED-11", "REDACTED", 19.974997, 110.518831, "offline"),
        Create("AED-12", "REDACTED", 19.975272, 110.518919, "online")
    ];

    private static AedDeviceRecord Create(
        string id,
        string name,
        double lat,
        double lng,
        string status)
    {
        return new AedDeviceRecord
        {
            Id = id,
            Name = name,
            Lat = lat,
            Lng = lng,
            Status = status,
            Address = $"REDACTED{name}（具体位置待标注）",
            LastMaintenance = status == "offline" ? "2026-01-20" : "2026-03-15",
            NextMaintenance = status == "offline" ? "2026-07-20" : "2026-09-15",
            Maintainer = "待分配"
        };
    }
}
