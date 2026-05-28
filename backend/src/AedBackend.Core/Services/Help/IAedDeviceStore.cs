using AedBackend.Core.Models.Aed;

namespace AedBackend.Core.Services.Help;

public interface IAedDeviceStore
{
    Task<IReadOnlyList<NearbyAedDevice>> GetNearbyAsync(
        double lat,
        double lng,
        int? radiusMeters,
        CancellationToken cancellationToken);

    Task<AedDeviceRecord?> FindByIdAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AedDeviceRecord>> GetAllAsync(CancellationToken cancellationToken);

    Task<AedDeviceRecord> AddAsync(AedDeviceRecord device, CancellationToken cancellationToken);

    Task<AedDeviceRecord?> UpdateAsync(string id, AedDeviceRecord updated, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    Task<bool> ExistsByNameAsync(string name, string? excludeId, CancellationToken cancellationToken);
}
