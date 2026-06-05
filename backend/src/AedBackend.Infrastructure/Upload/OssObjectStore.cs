using System.Net;
using Aliyun.OSS;
using Aliyun.OSS.Common;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Upload;

public interface IOssObjectStore
{
    Task PutObjectAsync(
        string key,
        Stream content,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken);

    Task<OssObjectInfo?> TryGetObjectAsync(string key, CancellationToken cancellationToken);
}

public sealed record OssObjectInfo(long ContentLength, string? ContentType);

public sealed class AliyunOssObjectStore : IOssObjectStore
{
    private readonly OssUploadOptions options;
    private readonly OssClient client;

    public AliyunOssObjectStore(IOptions<UploadOptions> options)
    {
        this.options = options.Value.Oss;
        Validate(this.options);
        client = new OssClient(
            this.options.Endpoint.Trim(),
            this.options.AccessKeyId.Trim(),
            this.options.AccessKeySecret.Trim());
    }

    public Task PutObjectAsync(
        string key,
        Stream content,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = new ObjectMetadata
            {
                ContentType = contentType,
                ContentLength = contentLength
            };
            client.PutObject(options.BucketName.Trim(), key, content, metadata);
        }, cancellationToken);
    }

    public Task<OssObjectInfo?> TryGetObjectAsync(string key, CancellationToken cancellationToken)
    {
        return Task.Run<OssObjectInfo?>(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var metadata = client.GetObjectMetadata(options.BucketName.Trim(), key);
                return new OssObjectInfo(metadata.ContentLength, metadata.ContentType);
            }
            catch (OssException ex) when (ex.ErrorCode == "NoSuchKey")
            {
                return null;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }, cancellationToken);
    }

    private static void Validate(OssUploadOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint)) throw new InvalidOperationException("缺少 OSS Endpoint 配置");
        if (string.IsNullOrWhiteSpace(options.BucketName)) throw new InvalidOperationException("缺少 OSS BucketName 配置");
        if (string.IsNullOrWhiteSpace(options.AccessKeyId)) throw new InvalidOperationException("缺少 OSS AccessKeyId 配置");
        if (string.IsNullOrWhiteSpace(options.AccessKeySecret)) throw new InvalidOperationException("缺少 OSS AccessKeySecret 配置");
    }
}
