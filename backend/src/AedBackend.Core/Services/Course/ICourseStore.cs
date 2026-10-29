using AedBackend.Core.Models.Course;

namespace AedBackend.Core.Services.Course;

public interface ICourseStore
{
    Task<IReadOnlyList<CourseRecord>> GetAllAsync(CancellationToken ct);
    Task<CourseRecord?> FindByIdAsync(string id, CancellationToken ct);
    Task<CourseRecord> AddAsync(CourseRecord course, CancellationToken ct);
    Task<CourseRecord?> UpdateAsync(string id, CourseRecord updated, CancellationToken ct);
    Task<bool> DeleteAsync(string id, CancellationToken ct);
    /// <summary>阅读量 +1 并返回新值；课程不存在时返回 null。</summary>
    Task<int?> IncrementViewAsync(string id, CancellationToken ct);
}
