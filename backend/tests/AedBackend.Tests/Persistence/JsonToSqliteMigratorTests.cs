using AedBackend.Core.Models.Course;
using AedBackend.Infrastructure.Persistence.Sqlite;

namespace AedBackend.Tests.Persistence;

public sealed class JsonToSqliteMigratorTests
{
    [Fact]
    public async Task RunAsync_ImportsRuntimeJsonAndPreservesCriticalFields()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        var dbPath = Path.Combine(temp.Root, "aed.db");

        var report = await new JsonToSqliteMigrator().RunAsync(temp.Root, dbPath, CancellationToken.None);

        Assert.True(File.Exists(dbPath));
        Assert.Equal(1, report.Counts["courses"]);
        Assert.Equal(2, report.Counts["course_videos"]);
        Assert.Equal(1, report.Counts["responder_profiles"]);
        Assert.Equal(1, report.Counts["help_reports"]);
        Assert.Equal(1, report.Counts["admin_sessions"]);
    }

    [Fact]
    public async Task RunAsync_FailsWhenDuplicateIdsExist()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await temp.WriteAsync("courses.json", new[] { SampleCourse("course-01"), SampleCourse("course-01") });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new JsonToSqliteMigrator().RunAsync(temp.Root, Path.Combine(temp.Root, "aed.db"), CancellationToken.None));

        Assert.Contains("重复键", ex.Message);
        Assert.False(File.Exists(Path.Combine(temp.Root, "aed.db")));
    }

    private static CourseRecord SampleCourse(string id)
    {
        return new CourseRecord
        {
            Id = id,
            Title = "课程",
            Thumbnail = "/uploads/course.jpg",
            Type = "video",
            Category = "first-aid",
            Content = "<p><img src=\"/uploads/classroom/a.jpg\"></p>",
            VideoUrl = "/uploads/classroom-videos/a.mp4",
            Videos =
            [
                new() { Order = 1, Title = "视频 1", Url = "/uploads/classroom-videos/a.mp4" },
                new() { Order = 2, Title = "视频 2", Url = "/uploads/classroom-videos/b.mp4" }
            ],
            CreatedAt = "2026-06-02T00:00:00Z"
        };
    }
}
