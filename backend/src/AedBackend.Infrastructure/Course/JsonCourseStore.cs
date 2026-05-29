using System.Text.Json;
using AedBackend.Core.Models.Course;
using AedBackend.Core.Services.Course;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Course;

public sealed class JsonCourseStore : ICourseStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonCourseStore(IOptions<WeChatOptions> options)
    {
        filePath = Path.GetFullPath(options.Value.CoursesFilePath);
    }

    public async Task<IReadOnlyList<CourseRecord>> GetAllAsync(CancellationToken ct)
    {
        var courses = await LoadCoursesAsync(ct);
        return courses
            .OrderByDescending(c => c.Pinned)
            .ThenBy(c => c.SortOrder)
            .ThenByDescending(c => c.CreatedAt)
            .ToArray();
    }

    public async Task<CourseRecord?> FindByIdAsync(string id, CancellationToken ct)
    {
        var courses = await LoadCoursesAsync(ct);
        return courses.FirstOrDefault(c =>
            c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<CourseRecord> AddAsync(CourseRecord course, CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            var courses = await ReadUnsafeAsync(ct);
            course.Id = NextId(courses);
            course.CreatedAt = DateTimeOffset.UtcNow.ToString("O");
            course.ViewCount = 0;
            courses.Add(course);
            await WriteUnsafeAsync(courses, ct);
            return course;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<CourseRecord?> UpdateAsync(string id, CourseRecord updated, CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            var courses = await ReadUnsafeAsync(ct);
            var index = courses.FindIndex(c =>
                c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return null;

            var existing = courses[index];
            ApplyUpdate(existing, updated);
            await WriteUnsafeAsync(courses, ct);
            return existing;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            var courses = await ReadUnsafeAsync(ct);
            var removed = courses.RemoveAll(c =>
                c.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return false;
            await WriteUnsafeAsync(courses, ct);
            return true;
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<List<CourseRecord>> LoadCoursesAsync(CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            return await ReadUnsafeAsync(ct);
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<List<CourseRecord>> ReadUnsafeAsync(CancellationToken ct)
    {
        if (!File.Exists(filePath))
        {
            var seed = DefaultCourses();
            await WriteUnsafeAsync(seed, ct);
            return seed;
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<CourseRecord>>(stream, JsonOptions, ct)
               ?? DefaultCourses();
    }

    private async Task WriteUnsafeAsync(List<CourseRecord> courses, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, courses, JsonOptions, ct);
    }

    private static string NextId(List<CourseRecord> courses)
    {
        var max = 0;
        foreach (var c in courses)
        {
            if (c.Id.StartsWith("course-") && int.TryParse(c.Id[7..], out var num))
                max = Math.Max(max, num);
        }

        return $"course-{max + 1:D2}";
    }

    private static void ApplyUpdate(CourseRecord target, CourseRecord source)
    {
        target.Title = source.Title;
        target.Thumbnail = source.Thumbnail;
        target.Type = source.Type;
        target.Category = source.Category;
        target.Content = source.Content;
        target.VideoUrl = source.VideoUrl;
        target.Disclaimer = source.Disclaimer;
        target.Pinned = source.Pinned;
        target.SortOrder = source.SortOrder;
        target.IsEnabled = source.IsEnabled;
        target.CacheVersion = source.CacheVersion;
        target.EstimatedTime = source.EstimatedTime;
        target.Steps = source.Steps;
    }

    internal static List<CourseRecord> DefaultCourses()
    {
        return new List<CourseRecord>
        {
            new()
            {
                Id = "course-01", Title = "AED 使用教程",
                Thumbnail = "/static/images/course-thumb-aed.jpg",
                Type = "article", Category = "emergency",
                Content = "<p>确认环境安全，打开 AED，按语音提示贴放电极片。</p>",
                Disclaimer = "急救内容仅用于校园培训与现场参考。",
                Pinned = true, SortOrder = 1, IsEnabled = true,
                CreatedAt = "2026-01-01T00:00:00Z",
                Steps = new List<CourseStepRecord>
                {
                    new()
                    {
                        Order = 1, Title = "打开设备",
                        Description = "取出 AED，打开电源并听从语音提示。",
                        Image = "/static/images/course-thumb-aed.jpg",
                        KeyPoints = new List<string> { "保持冷静", "按提示操作" }
                    }
                }
            },
            new()
            {
                Id = "course-02", Title = "心肺复苏教程",
                Thumbnail = "/static/images/course-thumb-cpr.jpg",
                Type = "article", Category = "emergency",
                Content = "<p>判断意识和呼吸，呼叫帮助，持续进行胸外按压。</p>",
                Disclaimer = "急救内容仅用于校园培训与现场参考。",
                Pinned = true, SortOrder = 2, IsEnabled = true,
                CreatedAt = "2026-01-01T00:00:00Z",
                Steps = new List<CourseStepRecord>
                {
                    new()
                    {
                        Order = 1, Title = "胸外按压",
                        Description = "双手交叠，垂直向下按压胸骨下半部。",
                        Image = "/static/images/course-thumb-cpr.jpg",
                        KeyPoints = new List<string> { "频率 100-120 次/分钟", "充分回弹" }
                    }
                }
            },
            new()
            {
                Id = "course-03", Title = "海姆立克急救法",
                Thumbnail = "/static/images/course-thumb-heimlich.jpg",
                Type = "article", Category = "emergency",
                Content = "<p>确认气道异物梗阻后，从背后环抱并向内上方冲击。</p>",
                Disclaimer = "急救内容仅用于校园培训与现场参考。",
                Pinned = false, SortOrder = 3, IsEnabled = true,
                CreatedAt = "2026-02-01T00:00:00Z",
                Steps = new List<CourseStepRecord>()
            },
            new()
            {
                Id = "course-04", Title = "外伤止血与包扎",
                Thumbnail = "/static/images/course-thumb-cpr.jpg",
                Type = "video", Category = "first-aid",
                Content = "<p>直接压迫止血，覆盖敷料，必要时寻求专业协助。</p>",
                Disclaimer = "急救内容仅用于校园培训与现场参考。",
                Pinned = false, SortOrder = 4, IsEnabled = true,
                CreatedAt = "2026-03-01T00:00:00Z",
                Steps = new List<CourseStepRecord>()
            }
        };
    }
}
