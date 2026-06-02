using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.Models.Course;

namespace AedBackend.Api.Endpoints;

internal static class AdminCourseVideoMapper
{
    private const string ArticleType = "article";
    private const string VideoType = "video";
    private const string DefaultVideoTitle = "视频";
    private const string ClassroomVideoPrefix = "/uploads/classroom-videos/";
    private const string Mp4Suffix = ".mp4";

    public static void NormalizeCourseVideos(CourseRecord course)
    {
        if (!course.Type.Equals(VideoType, StringComparison.OrdinalIgnoreCase))
        {
            course.Type = ArticleType;
            course.VideoUrl = null;
            course.Videos = new List<CourseVideoRecord>();
            return;
        }

        course.Type = VideoType;
        course.Videos = NormalizeVideos(course.Videos, course.VideoUrl);
        course.VideoUrl = course.Videos.FirstOrDefault()?.Url;
    }

    public static IResult? ValidateCourseVideos(CourseRecord course)
    {
        if (!course.Type.Equals(VideoType, StringComparison.OrdinalIgnoreCase)) return null;
        if (course.Videos.Count == 0)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "视频课程至少需要一个视频", 400);

        return course.Videos.Any(video => !IsValidVideoUrl(video.Url))
            ? EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "视频 URL 不合法", 400)
            : null;
    }

    public static List<CourseVideoRecord> NormalizeVideos(
        List<CourseVideoRecord>? videos,
        string? fallbackUrl)
    {
        var normalized = videos?
            .Where(v => !string.IsNullOrWhiteSpace(v.Url))
            .OrderBy(v => v.Order)
            .Select((v, index) => new CourseVideoRecord
            {
                Order = index + 1,
                Title = ResolveVideoTitle(v.Title, index + 1),
                Url = v.Url.Trim()
            })
            .ToList() ?? new List<CourseVideoRecord>();

        if (normalized.Count == 0 && !string.IsNullOrWhiteSpace(fallbackUrl))
            normalized.Add(CreateDefaultVideo(fallbackUrl, 1));
        return normalized;
    }

    public static List<CourseVideoRecord> MapVideos(
        List<CourseVideoDto>? dtos,
        string? fallbackUrl)
    {
        var videos = dtos?.Select(dto => new CourseVideoRecord
        {
            Order = dto.Order,
            Title = dto.Title?.Trim() ?? string.Empty,
            Url = dto.Url?.Trim() ?? string.Empty
        }).ToList();
        return NormalizeVideos(videos, fallbackUrl);
    }

    private static CourseVideoRecord CreateDefaultVideo(string url, int order)
    {
        return new CourseVideoRecord
        {
            Order = order,
            Title = ResolveVideoTitle(null, order),
            Url = url.Trim()
        };
    }

    private static string ResolveVideoTitle(string? title, int order)
    {
        return string.IsNullOrWhiteSpace(title) ? $"{DefaultVideoTitle} {order}" : title.Trim();
    }

    private static bool IsValidVideoUrl(string url)
    {
        if (url.StartsWith(ClassroomVideoPrefix, StringComparison.OrdinalIgnoreCase)
            && url.EndsWith(Mp4Suffix, StringComparison.OrdinalIgnoreCase))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
