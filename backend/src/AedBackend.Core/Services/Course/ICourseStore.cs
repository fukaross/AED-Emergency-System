using AedBackend.Core.Models.Course;

namespace AedBackend.Core.Services.Course;

public interface ICourseStore
{
    Task<IReadOnlyList<CourseRecord>> GetAllAsync(CancellationToken ct);
    Task<CourseRecord?> FindByIdAsync(string id, CancellationToken ct);
    Task<CourseRecord> AddAsync(CourseRecord course, CancellationToken ct);
    Task<CourseRecord?> UpdateAsync(string id, CourseRecord updated, CancellationToken ct);
    Task<bool> DeleteAsync(string id, CancellationToken ct);
}
