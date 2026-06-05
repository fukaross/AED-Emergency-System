using Microsoft.Extensions.Configuration;

namespace AedBackend.Infrastructure.Upload;

public sealed class UploadOptions
{
    public const string SectionName = "Upload";
    public const string LocalProvider = "Local";
    public const string OssProvider = "Oss";

    public string Provider { get; set; } = LocalProvider;
    public OssUploadOptions Oss { get; set; } = new();

    public void ApplyEnvironmentAliases(IConfiguration configuration)
    {
        Provider = FirstNonEmpty(configuration["AED_UPLOAD_PROVIDER"], Provider);
        Oss.Endpoint = FirstNonEmpty(configuration["AED_OSS_ENDPOINT"], Oss.Endpoint);
        Oss.BucketName = FirstNonEmpty(configuration["AED_OSS_BUCKET_NAME"], Oss.BucketName);
        Oss.AccessKeyId = FirstNonEmpty(configuration["AED_OSS_ACCESS_KEY_ID"], Oss.AccessKeyId);
        Oss.AccessKeySecret = FirstNonEmpty(configuration["AED_OSS_ACCESS_KEY_SECRET"], Oss.AccessKeySecret);
        Oss.PublicBaseUrl = FirstNonEmpty(configuration["AED_OSS_PUBLIC_BASE_URL"], Oss.PublicBaseUrl);
        Oss.ObjectKeyPrefix = FirstNonEmpty(configuration["AED_OSS_OBJECT_KEY_PREFIX"], Oss.ObjectKeyPrefix);
    }

    public bool UseOss()
    {
        return Provider.Equals(OssProvider, StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(string? first, string second)
    {
        return string.IsNullOrWhiteSpace(first) ? second : first;
    }
}

public sealed class OssUploadOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string AccessKeySecret { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string ObjectKeyPrefix { get; set; } = string.Empty;
}
