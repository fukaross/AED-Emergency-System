using AedBackend.Core.Models.Course;

namespace AedBackend.Core.Services.Course;

public interface IQuickActionStore
{
    Task<IReadOnlyList<QuickActionRecord>> GetAllAsync(CancellationToken ct);
    Task<QuickActionRecord?> FindByIdAsync(string id, CancellationToken ct);
    Task<QuickActionRecord> AddAsync(QuickActionRecord action, CancellationToken ct);
    Task<QuickActionRecord?> UpdateAsync(string id, QuickActionRecord updated, CancellationToken ct);
    Task<bool> DeleteAsync(string id, CancellationToken ct);
}
