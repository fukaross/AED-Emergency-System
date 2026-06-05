using AedBackend.Core.DTOs.Upload;
using Microsoft.AspNetCore.Http;

namespace AedBackend.Infrastructure.Upload;

public sealed class OssUploadVideoStorage : IUploadVideoStorage
{
    private const string ClassroomVideosScene = "classroom-videos";
    private const string VideoExtension = ".mp4";

    private readonly OssUploadSupport support;

    public OssUploadVideoStorage(OssUploadSupport support)
    {
        this.support = support;
    }

    public async Task<UploadImageResponse> SaveAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var temp = await OssUploadSupport.CopyToTempAndHashAsync(file, cancellationToken);
        try
        {
            var fileName = $"{temp.Hash}{VideoExtension}";
            var key = support.BuildKey($"uploads/{ClassroomVideosScene}/{fileName}");
            await using var stream = File.OpenRead(temp.Path);
            await support.UploadVerifiedAsync(key, stream, "video/mp4", stream.Length, cancellationToken);
            var url = support.BuildUrl(key);
            return new UploadImageResponse(url, url, temp.Hash);
        }
        finally
        {
            if (File.Exists(temp.Path)) File.Delete(temp.Path);
        }
    }
}
