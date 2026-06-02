using System.Text.Json;
using AedBackend.Core.Models.Course;
using AedBackend.Infrastructure.Course;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Course;

public sealed class JsonCourseStoreTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsSeedData_WhenFileMissing()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            var courses = await store.GetAllAsync(CancellationToken.None);

            Assert.Equal(4, courses.Count);
            Assert.Equal("course-01", courses[0].Id);
            Assert.Equal("AED 使用教程", courses[0].Title);
            Assert.True(File.Exists(path));
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task GetAllAsync_ReturnsPinnedFirst()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[]
        {
            Course("course-01", "普通课程", new CourseOptions(SortOrder: 1)),
            Course("course-02", "置顶课程", new CourseOptions(Pinned: true, SortOrder: 2)),
        });
        var store = CreateStore(path);

        try
        {
            var courses = await store.GetAllAsync(CancellationToken.None);
            Assert.Equal(2, courses.Count);
            Assert.True(courses[0].Pinned);
            Assert.Equal("置顶课程", courses[0].Title);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task GetAllAsync_ReturnsNewerCoursesFirst_WhenSortOrderMatches()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[]
        {
            Course("course-01", "旧课程"),
            Course("course-02", "新课程", new CourseOptions(CreatedAt: "2026-02-01T00:00:00Z")),
        });
        var store = CreateStore(path);

        try
        {
            var courses = await store.GetAllAsync(CancellationToken.None);
            Assert.Equal("新课程", courses[0].Title);
            Assert.Equal("旧课程", courses[1].Title);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task AddAsync_GeneratesAutoId()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[] { Course("course-01", "第一课") });
        var store = CreateStore(path);

        try
        {
            var created = await store.AddAsync(new CourseRecord
            {
                Title = "新课程",
                Type = "article",
                Category = "emergency",
            }, CancellationToken.None);

            Assert.Equal("course-02", created.Id);
            Assert.NotEmpty(created.CreatedAt);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task UpdateAsync_ModifiesFields()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[] { Course("course-01", "原始标题") });
        var store = CreateStore(path);

        try
        {
            var updated = new CourseRecord
            {
                Title = "修改后的标题",
                Type = "video",
                Category = "first-aid",
                Videos = new List<CourseVideoRecord>
                {
                    new() { Order = 1, Title = "第一段", Url = "https://example.com/a.mp4" }
                },
                Pinned = true,
                SortOrder = 99,
            };

            var result = await store.UpdateAsync("course-01", updated, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("修改后的标题", result!.Title);
            Assert.Equal("video", result.Type);
            Assert.Single(result.Videos);
            Assert.Equal("第一段", result.Videos[0].Title);
            Assert.True(result.Pinned);
            Assert.Equal(99, result.SortOrder);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesCourse()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[]
        {
            Course("course-01", "A"),
            Course("course-02", "B"),
        });
        var store = CreateStore(path);

        try
        {
            var deleted = await store.DeleteAsync("course-01", CancellationToken.None);
            Assert.True(deleted);

            var remaining = await store.GetAllAsync(CancellationToken.None);
            Assert.Single(remaining);
            Assert.Equal("course-02", remaining[0].Id);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsCourse()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[] { Course("course-05", "第五课") });
        var store = CreateStore(path);

        try
        {
            var found = await store.FindByIdAsync("course-05", CancellationToken.None);
            Assert.NotNull(found);
            Assert.Equal("第五课", found!.Title);

            var missing = await store.FindByIdAsync("course-99", CancellationToken.None);
            Assert.Null(missing);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    private static JsonCourseStore CreateStore(string path)
    {
        return new JsonCourseStore(Options.Create(new WeChatOptions
        {
            CoursesFilePath = path
        }));
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"courses-{Guid.NewGuid():N}",
            "courses.json");
    }

    private static void DeleteTempDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            Directory.Delete(directory, true);
    }

    private static async Task SeedAsync(string path, CourseRecord[] courses)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, courses);
    }

    private sealed record CourseOptions(
        bool Pinned = false,
        int SortOrder = 1,
        string CreatedAt = "2026-01-01T00:00:00Z");

    private static CourseRecord Course(string id, string title, CourseOptions? options = null)
    {
        var value = options ?? new CourseOptions();
        return new CourseRecord
        {
            Id = id,
            Title = title,
            Thumbnail = "/test.jpg",
            Type = "article",
            Category = "emergency",
            Pinned = value.Pinned,
            SortOrder = value.SortOrder,
            IsEnabled = true,
            CreatedAt = value.CreatedAt,
        };
    }
}
