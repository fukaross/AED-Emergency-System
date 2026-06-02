using AedBackend.Core.Models.Course;
using AedBackend.Core.Services.Course;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class SqliteCourseStore : ICourseStore
{
    private readonly IDbContextFactory<AedDbContext> factory;
    public SqliteCourseStore(IDbContextFactory<AedDbContext> factory) => this.factory = factory;

    public async Task<IReadOnlyList<CourseRecord>> GetAllAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var courses = await SqliteStoreHelpers.WithCourseGraph(db.Courses.AsNoTracking()).ToArrayAsync(ct);
        return courses.Select(SqliteMapper.ToRecord)
            .OrderByDescending(c => c.Pinned)
            .ThenBy(c => c.SortOrder)
            .ThenByDescending(c => c.CreatedAt)
            .ToArray();
    }

    public async Task<CourseRecord?> FindByIdAsync(string id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await SqliteStoreHelpers.WithCourseGraph(db.Courses.AsNoTracking())
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return item is null ? null : SqliteMapper.ToRecord(item);
    }

    public async Task<CourseRecord> AddAsync(CourseRecord course, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        course.Id = await NextIdAsync(db, ct);
        course.CreatedAt = DateTimeOffset.UtcNow.ToString("O");
        course.ViewCount = 0;
        db.Courses.Add(SqliteMapper.ToEntity(course));
        await db.SaveChangesAsync(ct);
        return course;
    }

    public async Task<CourseRecord?> UpdateAsync(string id, CourseRecord updated, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await SqliteStoreHelpers.WithCourseGraph(db.Courses)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (current is null) return null;

        var next = Merge(SqliteMapper.ToRecord(current), updated);
        db.Courses.Remove(current);
        db.Courses.Add(SqliteMapper.ToEntity(next));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return next;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await SqliteStoreHelpers.WithCourseGraph(db.Courses).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (current is null) return false;
        db.Courses.Remove(current);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static CourseRecord Merge(CourseRecord existing, CourseRecord source)
    {
        existing.Title = source.Title; existing.Thumbnail = source.Thumbnail; existing.Type = source.Type;
        existing.Category = source.Category; existing.Content = source.Content; existing.VideoUrl = source.VideoUrl;
        existing.Videos = source.Videos; existing.Disclaimer = source.Disclaimer; existing.Pinned = source.Pinned;
        existing.SortOrder = source.SortOrder; existing.IsEnabled = source.IsEnabled;
        existing.CacheVersion = source.CacheVersion; existing.EstimatedTime = source.EstimatedTime;
        existing.Steps = source.Steps;
        return existing;
    }

    private static async Task<string> NextIdAsync(AedDbContext db, CancellationToken ct)
    {
        var ids = await db.Courses.Select(c => c.Id).ToArrayAsync(ct);
        var max = ids.Where(id => id.StartsWith("course-", StringComparison.OrdinalIgnoreCase))
            .Select(id => int.TryParse(id.AsSpan(7), out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"course-{max + 1:D2}";
    }
}

internal sealed class SqliteQuickActionStore : IQuickActionStore
{
    private readonly IDbContextFactory<AedDbContext> factory;
    public SqliteQuickActionStore(IDbContextFactory<AedDbContext> factory) => this.factory = factory;

    public async Task<IReadOnlyList<QuickActionRecord>> GetAllAsync(CancellationToken ct)
    {
        var actions = await LoadAllAsync(ct);
        return actions.Where(a => a.IsEnabled).OrderBy(a => a.SortOrder).ToArray();
    }

    public async Task<IReadOnlyList<QuickActionRecord>> GetAllAdminAsync(CancellationToken ct)
    {
        var actions = await LoadAllAsync(ct);
        return actions.OrderBy(a => a.SortOrder).ToArray();
    }

    public async Task<QuickActionRecord?> FindByIdAsync(string id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await SqliteStoreHelpers.WithQuickActionGraph(db.QuickActions.AsNoTracking())
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        return item is null ? null : SqliteMapper.ToRecord(item);
    }

    public async Task<QuickActionRecord> AddAsync(QuickActionRecord action, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        action.Id = await NextIdAsync(db, ct);
        db.QuickActions.Add(SqliteMapper.ToEntity(action));
        await db.SaveChangesAsync(ct);
        return action;
    }

    public async Task<QuickActionRecord?> UpdateAsync(string id, QuickActionRecord updated, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await SqliteStoreHelpers.WithQuickActionGraph(db.QuickActions)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (current is null) return null;

        var next = Merge(SqliteMapper.ToRecord(current), updated);
        db.QuickActions.Remove(current);
        db.QuickActions.Add(SqliteMapper.ToEntity(next));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return next;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await SqliteStoreHelpers.WithQuickActionGraph(db.QuickActions)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (current is null) return false;
        db.QuickActions.Remove(current);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<IReadOnlyList<QuickActionRecord>> LoadAllAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var actions = await SqliteStoreHelpers.WithQuickActionGraph(db.QuickActions.AsNoTracking()).ToArrayAsync(ct);
        return actions.Select(SqliteMapper.ToRecord).ToArray();
    }

    private static QuickActionRecord Merge(QuickActionRecord existing, QuickActionRecord source)
    {
        existing.Title = source.Title; existing.Icon = source.Icon; existing.Summary = source.Summary;
        existing.SortOrder = source.SortOrder; existing.IsEnabled = source.IsEnabled; existing.Steps = source.Steps;
        return existing;
    }

    private static async Task<string> NextIdAsync(AedDbContext db, CancellationToken ct)
    {
        var ids = await db.QuickActions.Select(a => a.Id).ToArrayAsync(ct);
        var max = ids.Where(id => id.StartsWith("qa-", StringComparison.OrdinalIgnoreCase))
            .Select(id => int.TryParse(id.AsSpan(3), out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"qa-{max + 1:D2}";
    }
}
