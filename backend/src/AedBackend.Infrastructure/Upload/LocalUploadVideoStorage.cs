using System.Security.Cryptography;
using AedBackend.Core.DTOs.Upload;
using AedBackend.Infrastructure.WeChat;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Upload;

public sealed class LocalUploadVideoStorage : IUploadVideoStorage
{
    private const string ClassroomVideosScene = "classroom-videos";
    private const string VideoExtension = ".mp4";

    private readonly WeChatOptions options;

    public LocalUploadVideoStorage(IOptions<WeChatOptions> options)
    {
        this.options = options.Value;
    }

    public async Task<UploadImageResponse> SaveAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var directory = GetVideoDirectory();
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        var hash = await CopyAndHashAsync(file, tempPath, cancellationToken);
        var fileName = $"{hash}{VideoExtension}";
        var targetPath = Path.Combine(directory, fileName);

        if (!File.Exists(targetPath)) MoveTempFile(tempPath, targetPath);
        if (File.Exists(tempPath)) File.Delete(tempPath);

        return CreateResponse(fileName, hash);
    }

    private async Task<string> CopyAndHashAsync(
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

    private string GetVideoDirectory()
    {
        return Path.GetFullPath(Path.Combine(options.UploadsDirectory, ClassroomVideosScene));
    }

    private UploadImageResponse CreateResponse(string fileName, string fileId)
    {
        var url = $"{options.PublicBaseUrl.TrimEnd('/')}/uploads/{ClassroomVideosScene}/{fileName}";
        return new UploadImageResponse(url, url, fileId);
    }
}
