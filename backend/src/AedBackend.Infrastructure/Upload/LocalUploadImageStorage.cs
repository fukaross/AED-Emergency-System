using System.Security.Cryptography;
using AedBackend.Core.DTOs.Upload;
using AedBackend.Infrastructure.WeChat;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Upload;

public sealed class LocalUploadImageStorage : IUploadImageStorage
{
    private const string ClassroomScene = "classroom";
    private static readonly IReadOnlySet<string> DeduplicatedScenes = new HashSet<string>
    {
        "course",
        "quick-action"
    };

    private readonly WeChatOptions options;

    public LocalUploadImageStorage(IOptions<WeChatOptions> options)
    {
        this.options = options.Value;
    }

    public Task<UploadImageResponse> SaveAsync(
        IFormFile file,
        string scene,
        string extension,
        CancellationToken cancellationToken)
    {
        return DeduplicatedScenes.Contains(scene)
            ? SaveDeduplicatedAsync(file, extension, cancellationToken)
            : SaveUniqueAsync(file, scene, extension, cancellationToken);
    }

    private async Task<UploadImageResponse> SaveUniqueAsync(
        IFormFile file,
        string scene,
        string extension,
        CancellationToken cancellationToken)
    {
        var fileId = Guid.NewGuid().ToString("N");
        var fileName = $"{fileId}{extension}";
        var directory = GetSceneDirectory(scene);
        Directory.CreateDirectory(directory);

        await using var stream = File.Create(Path.Combine(directory, fileName));
        await file.CopyToAsync(stream, cancellationToken);

        return CreateResponse(scene, fileName, fileId);
    }

    private async Task<UploadImageResponse> SaveDeduplicatedAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        var directory = GetSceneDirectory(ClassroomScene);
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        var hash = await CopyAndHashAsync(file, tempPath, cancellationToken);
        var fileName = $"{hash}{extension}";
        var targetPath = Path.Combine(directory, fileName);

        if (!File.Exists(targetPath)) MoveTempFile(tempPath, targetPath);
        if (File.Exists(tempPath)) File.Delete(tempPath);

        return CreateResponse(ClassroomScene, fileName, hash);
    }

    private static async Task<string> CopyAndHashAsync(
        IFormFile file,
        string tempPath,
        CancellationToken cancellationToken)
    {
        await using var source = file.OpenReadStream();
        await using var target = File.Create(tempPath);
        using var sha256 = SHA256.Create();
        await source.CopyToAsync(target, cancellationToken);
        await target.FlushAsync(cancellationToken);
        target.Position = 0;
        var hash = await sha256.ComputeHashAsync(target, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void MoveTempFile(string tempPath, string targetPath)
    {
        try
        {
            File.Move(tempPath, targetPath, overwrite: false);
        }
        catch (IOException) when (File.Exists(targetPath))
        {
            File.Delete(tempPath);
        }
    }

    private string GetSceneDirectory(string scene)
    {
        return Path.GetFullPath(Path.Combine(options.UploadsDirectory, scene));
    }

    private UploadImageResponse CreateResponse(string scene, string fileName, string fileId)
    {
        var url = $"{options.PublicBaseUrl.TrimEnd('/')}/uploads/{scene}/{fileName}";
        return new UploadImageResponse(url, url, fileId);
    }
}
