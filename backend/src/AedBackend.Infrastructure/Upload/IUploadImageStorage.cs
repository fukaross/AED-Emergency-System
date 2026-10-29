using AedBackend.Core.DTOs.Upload;
using Microsoft.AspNetCore.Http;

namespace AedBackend.Infrastructure.Upload;

public interface IUploadImageStorage
{
    Task<UploadImageResponse> SaveAsync(
        IFormFile file,
        string scene,
        string extension,
        CancellationToken cancellationToken);
}
