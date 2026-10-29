using System.Text.Json;
using AedBackend.Core.Models.Aed;
using AedBackend.Core.Services.Help;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Help;

/**
 * JSON provider 仅作为只读/回滚数据源：设备来自既有 JSON 文件，不再内置真实设备种子。
 * 文件缺失时返回空集合且不写盘；完整演示数据由 SQLite + init-demo-data 提供。
 */
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
            return await LoadUnsafeAsync(cancellationToken);
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

    public async Task<IReadOnlyList<AedDeviceRecord>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await LoadDevicesAsync(cancellationToken);
    }

    public async Task<AedDeviceRecord> AddAsync(
        AedDeviceRecord device,
        CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            var devices = await LoadUnsafeAsync(cancellationToken);
            var list = devices.ToList();

            if (list.Any(d => d.Name.Equals(device.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("设备名称已存在");
            }

            var newId = GenerateNextId(list);
            device.Id = newId;
            if (string.IsNullOrWhiteSpace(device.Status))
            {
                device.Status = "online";
            }

            list.Add(device);
            await WriteDevicesAsync(list, cancellationToken);
            return device;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<AedDeviceRecord?> UpdateAsync(
        string id,
        AedDeviceRecord updated,
        CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            var devices = await LoadUnsafeAsync(cancellationToken);
            var list = devices.ToList();
            var existing = list.FirstOrDefault(d =>
                d.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (existing is null) return null;

            if (updated.Status == "offline" && string.IsNullOrWhiteSpace(updated.StatusNote))
            {
                throw new InvalidOperationException("设备离线时必须填写状态备注");
            }

            if (!string.IsNullOrWhiteSpace(updated.Name)
                && !updated.Name.Equals(existing.Name, StringComparison.OrdinalIgnoreCase)
                && list.Any(d => d.Id != existing.Id
                    && d.Name.Equals(updated.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("设备名称已存在");
            }

            existing.Name = updated.Name;
            existing.Lat = updated.Lat;
            existing.Lng = updated.Lng;
            existing.Status = updated.Status;
            existing.StatusNote = updated.StatusNote;
            existing.Image = updated.Image;
            existing.Address = updated.Address;
            existing.LastMaintenance = updated.LastMaintenance;
            existing.NextMaintenance = updated.NextMaintenance;
            existing.Maintainer = updated.Maintainer;

            await WriteDevicesAsync(list, cancellationToken);
            return existing;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            var devices = await LoadUnsafeAsync(cancellationToken);
            var list = devices.ToList();
            var removed = list.RemoveAll(d =>
                d.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (removed == 0) return false;

            await WriteDevicesAsync(list, cancellationToken);
            return true;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<bool> ExistsByNameAsync(
        string name,
        string? excludeId,
        CancellationToken cancellationToken)
    {
        var devices = await LoadDevicesAsync(cancellationToken);
        return devices.Any(d =>
            d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && (excludeId is null || !d.Id.Equals(excludeId, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<IReadOnlyList<AedDeviceRecord>> LoadUnsafeAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath)) return [];

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<IReadOnlyList<AedDeviceRecord>>(
            stream,
            JsonOptions,
            cancellationToken) ?? [];
    }

    private static string GenerateNextId(IReadOnlyList<AedDeviceRecord> devices)
    {
        if (devices.Count == 0) return "AED-01";

        var maxNum = devices
            .Select(d => d.Id)
            .Where(id => id.StartsWith("AED-", StringComparison.OrdinalIgnoreCase))
            .Select(id => int.TryParse(id.AsSpan(4), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"AED-{(maxNum + 1):D2}";
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

}
