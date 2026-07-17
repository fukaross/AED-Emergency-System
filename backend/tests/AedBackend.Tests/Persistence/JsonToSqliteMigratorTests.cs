using AedBackend.Core.Models.Course;
using AedBackend.Core.Models.Rescue;
using AedBackend.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;

namespace AedBackend.Tests.Persistence;

public sealed class JsonToSqliteMigratorTests
{
    [Fact]
    public async Task RunAsync_ImportsRuntimeJsonAndPreservesCriticalFields()
    {
        using var temp = new ProviderTempData();
        await temp.WriteValidDataAsync();
        await temp.WriteRescueSessionJsonAsync();
        var dbPath = Path.Combine(temp.Root, "aed.db");

        var report = await new JsonToSqliteMigrator().RunAsync(temp.Root, dbPath, CancellationToken.None);

        Assert.True(File.Exists(dbPath));
        Assert.Equal(1, report.Counts["courses"]);
        Assert.Equal(2, report.Counts["course_videos"]);
        Assert.Equal(1, report.Counts["responder_profiles"]);
        Assert.Equal(1, report.Counts["help_reports"]);
        Assert.Equal(1, report.Counts["admin_sessions"]);
        Assert.Equal(1, report.Counts["rescue_sessions"]);
        Assert.Equal(1, report.Counts["rescue_session_participants"]);
        Assert.Equal(2, report.Counts["rescue_session_messages"]);
        await AssertRescueAggregateAsync(dbPath);
    }

    private static async Task AssertRescueAggregateAsync(string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        await connection.OpenAsync();

        await using var participant = connection.CreateCommand();
        participant.CommandText = """
            SELECT DisplayName, LastLat, LastLng, LocationReportedAt, LastReadSeq
            FROM rescue_session_participants
            WHERE HelpRequestId = 'help-01' AND OpenId = 'openid-2';
            """;
        await using (var reader = await participant.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal("急救员甲", reader.GetString(0));
            Assert.Equal(20.123456, reader.GetDouble(1), 6);
            Assert.Equal(110.654321, reader.GetDouble(2), 6);
            Assert.False(reader.IsDBNull(3));
            Assert.Equal(1, reader.GetInt64(4));
        }

        await using var message = connection.CreateCommand();
        message.CommandText = """
            SELECT SenderName, Text, Seq
            FROM rescue_session_messages
            WHERE Id = 'rescue-message-2';
            """;
        await using var messageReader = await message.ExecuteReaderAsync();
        Assert.True(await messageReader.ReadAsync());
        Assert.Equal("急救员甲", messageReader.GetString(0));
        Assert.Equal("已出发", messageReader.GetString(1));
        Assert.Equal(2, messageReader.GetInt64(2));
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

    [Fact]
    public async Task RunAsync_FailsWhenRescueSessionReferencesMissingHelp()
    {
        using var temp = new ProviderTempData();
        await temp.WriteMinimalJsonAsync();
        await temp.WriteAsync("rescue-sessions.json", new
        {
            sessions = new[]
            {
                new RescueSessionRecord { HelpRequestId = "missing-help", CreatedAt = DateTimeOffset.UtcNow }
            }
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None));

        Assert.Contains("缺少关联求助", ex.Message);
        Assert.False(File.Exists(temp.DbPath));
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
