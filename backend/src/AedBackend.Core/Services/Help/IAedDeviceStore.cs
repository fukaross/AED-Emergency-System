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
}
