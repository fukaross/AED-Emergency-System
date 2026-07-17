using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal static class SqliteStoreHelpers
{
    public const int BusyTimeoutMilliseconds = 5000;

    public static IQueryable<CourseEntity> WithCourseGraph(IQueryable<CourseEntity> query)
    {
        return query
            .Include(c => c.Videos)
            .Include(c => c.Steps)
            .ThenInclude(s => s.KeyPoints);
    }

    public static IQueryable<QuickActionEntity> WithQuickActionGraph(IQueryable<QuickActionEntity> query)
    {
        return query
            .Include(a => a.Steps)
            .ThenInclude(s => s.KeyPoints);
    }

    public static IQueryable<HelpRequestEntity> WithHelpGraph(IQueryable<HelpRequestEntity> query)
    {
        return query
            .Include(h => h.GuestSession)
            .Include(h => h.Images)
            .Include(h => h.Responders)
            .Include(h => h.Timeline)
            .Include(h => h.Reports)
            .ThenInclude(r => r.Images);
    }

    public static IQueryable<RescueSessionEntity> WithRescueGraph(IQueryable<RescueSessionEntity> query)
    {
        return query
            .Include(s => s.Participants)
            .Include(s => s.Messages);
    }

    public static string BuildConnectionString(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path),
            ForeignKeys = true,
            Pooling = false
        };
        return builder.ToString();
    }

    public static async Task EnsureReadyAsync(
        IDbContextFactory<AedDbContext> factory,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        await SqliteSchemaUpgrade.ApplyAsync(db, ct);
        await ApplyPragmasAsync(db, ct);
    }

    public static async Task ApplyPragmasAsync(AedDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
    }
}
