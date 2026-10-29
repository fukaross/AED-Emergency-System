using AedBackend.Core.DTOs.Upload;
using Microsoft.AspNetCore.Http;

namespace AedBackend.Infrastructure.Upload;

public sealed class OssUploadImageStorage : IUploadImageStorage
{
    private const string ClassroomScene = "classroom";
    private static readonly IReadOnlySet<string> DeduplicatedScenes = new HashSet<string>
    {
        "course",
        "quick-action"
    };

    private readonly OssUploadSupport support;

    public OssUploadImageStorage(OssUploadSupport support)
    {
        this.support = support;
    }

    public async Task<UploadImageResponse> SaveAsync(
        IFormFile file,
        string scene,
        string extension,
        CancellationToken cancellationToken)
    {
        return DeduplicatedScenes.Contains(scene)
            ? await SaveDeduplicatedAsync(file, extension, cancellationToken)
            : await SaveUniqueAsync(file, scene, extension, cancellationToken);
    }

    private async Task<UploadImageResponse> SaveUniqueAsync(
        IFormFile file,
        string scene,
        string extension,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fileId = Guid.NewGuid().ToString("N");
        var fileName = $"{fileId}{extension}";
        var key = support.BuildKey($"uploads/{scene}/{fileName}");
        using var stream = file.OpenReadStream();
        await support.UploadVerifiedAsync(
            key,
            stream,
            OssUploadSupport.ContentTypeForExtension(extension),
            file.Length,
            cancellationToken);
        return CreateResponse(key, fileId);
    }

    private async Task<UploadImageResponse> SaveDeduplicatedAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        var temp = await OssUploadSupport.CopyToTempAndHashAsync(file, cancellationToken);
        try
        {
            var fileName = $"{temp.Hash}{extension}";
            var key = support.BuildKey($"uploads/{ClassroomScene}/{fileName}");
            await using var stream = File.OpenRead(temp.Path);
            await support.UploadVerifiedAsync(
                key,
                stream,
                OssUploadSupport.ContentTypeForExtension(extension),
                stream.Length,
                cancellationToken);
            return CreateResponse(key, temp.Hash);
        }
        finally
        {
            if (File.Exists(temp.Path)) File.Delete(temp.Path);
        }
    }

    private UploadImageResponse CreateResponse(string key, string fileId)
    {
        var url = support.BuildUrl(key);
        return new UploadImageResponse(url, url, fileId);
    }
}
