using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Upload;

public sealed class OssUploadSupport
{
    private readonly OssUploadOptions options;
    private readonly IOssObjectStore store;

    public OssUploadSupport(IOptions<UploadOptions> options, IOssObjectStore store)
    {
        this.options = options.Value.Oss;
        this.store = store;
        if (string.IsNullOrWhiteSpace(this.options.PublicBaseUrl))
            throw new InvalidOperationException("缺少 OSS PublicBaseUrl 配置");
    }

    public string BuildKey(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var prefix = options.ObjectKeyPrefix?.Trim().Trim('/') ?? string.Empty;
        return string.IsNullOrWhiteSpace(prefix) ? normalized : $"{prefix}/{normalized}";
    }

    public string BuildUrl(string key)
    {
        var path = key.Replace('\\', '/').TrimStart('/');
        return $"{options.PublicBaseUrl.TrimEnd('/')}/{path}";
    }

    public async Task UploadVerifiedAsync(
        string key,
        Stream content,
        string contentType,
        long length,
        CancellationToken ct)
    {
        var existing = await store.TryGetObjectAsync(key, ct);
        if (existing?.ContentLength == length) return;
        await store.PutObjectAsync(key, content, contentType, length, ct);
        var uploaded = await store.TryGetObjectAsync(key, ct);
        if (uploaded?.ContentLength != length)
            throw new InvalidOperationException($"OSS object 校验失败: {key}");
    }

    public static string ContentTypeForExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream"
        };
    }

    public static async Task<HashedTempFile> CopyToTempAndHashAsync(IFormFile file, CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aed-upload-{Guid.NewGuid():N}.tmp");
        await using var source = file.OpenReadStream();
        await using var target = File.Create(tempPath);
        using var sha256 = SHA256.Create();
        await source.CopyToAsync(target, ct);
        await target.FlushAsync(ct);
        target.Position = 0;
        var hash = await sha256.ComputeHashAsync(target, ct);
        return new HashedTempFile(tempPath, Convert.ToHexString(hash).ToLowerInvariant());
    }

}

public sealed record HashedTempFile(string Path, string Hash);
