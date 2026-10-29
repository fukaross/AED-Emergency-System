using AedBackend.Core.DTOs.Upload;
using Microsoft.AspNetCore.Http;

namespace AedBackend.Infrastructure.Upload;

public interface IUploadVideoStorage
{
    Task<UploadImageResponse> SaveAsync(
        IFormFile file,
        CancellationToken cancellationToken);
}
