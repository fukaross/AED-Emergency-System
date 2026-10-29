namespace AedBackend.Core.DTOs.Upload;

public sealed record UploadImageResponse(
    string Url,
    string Thumbnail,
    string FileId);
