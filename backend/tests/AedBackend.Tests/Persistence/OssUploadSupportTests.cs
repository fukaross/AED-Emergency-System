using AedBackend.Infrastructure.Upload;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Persistence;

public sealed class OssUploadSupportTests
{
    private const string PublicBaseUrl = "REDACTED";

    [Fact]
    public void BuildUrl_KeepsObjectKeyPrefixInPublicUrl()
    {
        var support = CreateSupport("aed");

        var key = support.BuildKey("uploads/help/a.png");
        var url = support.BuildUrl(key);

        Assert.Equal("aed/uploads/help/a.png", key);
        Assert.Equal($"{PublicBaseUrl}/aed/uploads/help/a.png", url);
    }

    [Fact]
    public void RewriteText_PreservesQueryAndFragmentWithoutAddingThemToObjectKey()
    {
        var rewriter = new UploadUrlRewriter(
            "REDACTED",
            PublicBaseUrl);
        var source = "REDACTED/uploads/classroom/a.jpg?x-oss-process=image/resize#cover";

        var result = rewriter.RewriteText($"<img src=\"{source}\">");

        var reference = Assert.Single(result.References);
        Assert.True(result.Changed);
        Assert.Equal("uploads/classroom/a.jpg", reference.RelativePath);
        Assert.Equal("?x-oss-process=image/resize#cover", reference.Suffix);
        Assert.Contains($"{PublicBaseUrl}/uploads/classroom/a.jpg?x-oss-process=image/resize#cover", result.Value);
    }

    private static OssUploadSupport CreateSupport(string objectKeyPrefix)
    {
        var options = Options.Create(new UploadOptions
        {
            Oss = new OssUploadOptions
            {
                PublicBaseUrl = PublicBaseUrl,
                ObjectKeyPrefix = objectKeyPrefix
            }
        });
        return new OssUploadSupport(options, new FakeOssObjectStore());
    }

    private sealed class FakeOssObjectStore : IOssObjectStore
    {
        public Task PutObjectAsync(
            string key,
            Stream content,
            string contentType,
            long contentLength,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<OssObjectInfo?> TryGetObjectAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult<OssObjectInfo?>(null);
        }
    }
}
