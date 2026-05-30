using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Upload;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

public static class UploadEndpoints
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private const int SignatureReadLength = 12;
    private const string OctetStreamContentType = "application/octet-stream";
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] RiffSignature = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WebpSignature = [0x57, 0x45, 0x42, 0x50];
    private static readonly IReadOnlySet<string> PublicScenes = new HashSet<string>
    {
        "help",
        "report"
    };

    private static readonly IReadOnlySet<string> AuthScenes = new HashSet<string>
    {
        "avatar",
        "volunteer"
    };

    private static readonly IReadOnlySet<string> AdminScenes = new HashSet<string>
    {
        "device",
        "rescuer",
        "course",
        "quick-action"
    };

    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

    public static IEndpointRouteBuilder MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/upload/image", UploadImageAsync)
            .DisableAntiforgery()
            .WithTags("Upload")
            .WithName("UploadImage");

        return app;
    }

    private static async Task<IResult> UploadImageAsync(
        HttpRequest request,
        IOptions<WeChatOptions> options,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IAdminAuthorizationService adminAuthorization,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "必须使用 multipart/form-data", 400);
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var scene = form["scene"].ToString();
        var validationError = await ValidateAsync(
            scene,
            request,
            tokenService,
            userStore,
            adminAuthorization,
            cancellationToken);
        if (validationError is not null) return validationError;

        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        var fileValidation = await ValidateFileAsync(file, cancellationToken);
        if (fileValidation.Error is not null) return fileValidation.Error;

        var response = await SaveFileAsync(
            file!,
            scene,
            fileValidation.Extension!,
            options.Value,
            cancellationToken);
        return Results.Ok(ApiResponse.Ok(response));
    }

    private static async Task<IResult?> ValidateAsync(
        string scene,
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IAdminAuthorizationService adminAuthorization,
        CancellationToken cancellationToken)
    {
        if (!PublicScenes.Contains(scene) && !AuthScenes.Contains(scene) && !AdminScenes.Contains(scene))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "scene 不支持", 400);
        }

        if (PublicScenes.Contains(scene)) return null;

        if (AuthScenes.Contains(scene))
        {
            var user = await EndpointUsers.FindCurrentUserAsync(
                request,
                tokenService,
                userStore,
                cancellationToken);
            return user is null ? EndpointFailures.JsonFailure(ErrorCodes.Unauthorized, "登录 token 无效", 401) : null;
        }

        var adminKey = request.Headers.TryGetValue(EndpointSecurity.AdminKeyHeader, out var values)
            ? values.ToString()
            : null;
        var clientIp = request.HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = request.Headers.UserAgent.ToString();

        var adminAuth = await adminAuthorization.RequireAdminSessionAsync(
            adminKey, clientIp, userAgent, cancellationToken);
        return adminAuth.Succeeded ? null
            : EndpointFailures.JsonFailure(adminAuth.ErrorCode, adminAuth.ErrorMessage, adminAuth.StatusCode);
    }

    private static async Task<FileValidationResult> ValidateFileAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return FileValidationResult.Fail(
                EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "file 不能为空", 400));
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return FileValidationResult.Fail(
                EndpointFailures.JsonFailure(ErrorCodes.FileTooLarge, "文件超过大小限制", 400));
        }

        var extension = await DetectImageExtensionAsync(file, cancellationToken);
        if (extension is null)
        {
            return FileValidationResult.Fail(
                EndpointFailures.JsonFailure(ErrorCodes.FileTypeNotAllowed, "不支持的文件格式", 400));
        }

        return IsContentTypeCompatible(file.ContentType, extension)
            ? FileValidationResult.Ok(extension)
            : FileValidationResult.Fail(
                EndpointFailures.JsonFailure(ErrorCodes.FileTypeNotAllowed, "文件 MIME 类型不匹配", 400));
    }

    private static async Task<UploadImageResponse> SaveFileAsync(
        IFormFile file,
        string scene,
        string extension,
        WeChatOptions options,
        CancellationToken cancellationToken)
    {
        var fileId = Guid.NewGuid().ToString("N");
        var directory = Path.GetFullPath(Path.Combine(options.UploadsDirectory, scene));
        Directory.CreateDirectory(directory);

        var fileName = $"{fileId}{extension}";
        var filePath = Path.Combine(directory, fileName);
        await using var stream = File.Create(filePath);
        await file.CopyToAsync(stream, cancellationToken);

        var url = $"{options.PublicBaseUrl.TrimEnd('/')}/uploads/{scene}/{fileName}";
        return new UploadImageResponse(url, url, fileId);
    }

    private static async Task<string?> DetectImageExtensionAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var header = new byte[SignatureReadLength];
        await using var stream = file.OpenReadStream();
        var length = await stream.ReadAsync(header, cancellationToken);

        if (StartsWith(header, length, JpegSignature)) return ".jpg";
        if (StartsWith(header, length, PngSignature)) return ".png";
        return IsWebp(header, length) ? ".webp" : null;
    }

    private static bool StartsWith(byte[] header, int length, byte[] signature)
    {
        return length >= signature.Length && header.AsSpan(0, signature.Length).SequenceEqual(signature);
    }

    private static bool IsWebp(byte[] header, int length)
    {
        return length >= SignatureReadLength
            && header.AsSpan(0, RiffSignature.Length).SequenceEqual(RiffSignature)
            && header.AsSpan(8, WebpSignature.Length).SequenceEqual(WebpSignature);
    }

    private static bool IsContentTypeCompatible(string contentType, string extension)
    {
        if (string.IsNullOrWhiteSpace(contentType)
            || contentType.Equals(OctetStreamContentType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return AllowedContentTypes.TryGetValue(extension, out var allowed)
            && IsSameImageContentType(contentType, allowed);
    }

    private static bool IsSameImageContentType(string actual, string allowed)
    {
        return actual.Equals(allowed, StringComparison.OrdinalIgnoreCase)
            || (allowed == "image/jpeg"
                && actual.Equals("image/jpg", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record FileValidationResult(string? Extension, IResult? Error)
    {
        public static FileValidationResult Ok(string extension)
        {
            return new FileValidationResult(extension, null);
        }

        public static FileValidationResult Fail(IResult error)
        {
            return new FileValidationResult(null, error);
        }
    }
}
