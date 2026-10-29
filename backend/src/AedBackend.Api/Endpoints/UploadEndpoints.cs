using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Upload;
using Microsoft.AspNetCore.Mvc;

namespace AedBackend.Api.Endpoints;

public static class UploadEndpoints
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private const long MaxSessionImageBytes = 10 * 1024 * 1024;
    private const long MaxVideoSizeBytes = 100 * 1024 * 1024;
    private const long MaxVideoRequestBodyBytes = 110 * 1024 * 1024;
    private const int SignatureReadLength = 12;
    private const int Mp4SignatureReadLength = 12;
    private const string OctetStreamContentType = "application/octet-stream";
    private const string CourseScene = "course";
    private const string SessionScene = "session";
    private const string Mp4ContentType = "video/mp4";
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] RiffSignature = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WebpSignature = [0x57, 0x45, 0x42, 0x50];
    private static readonly byte[] FtypSignature = [0x66, 0x74, 0x79, 0x70];
    private static readonly IReadOnlySet<string> PublicScenes = new HashSet<string>
    {
        "help",
        "report"
    };

    private static readonly IReadOnlySet<string> AuthScenes = new HashSet<string>
    {
        "avatar",
        "volunteer",
        SessionScene
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

        app.MapPost("/api/upload/video", UploadVideoAsync)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxVideoRequestBodyBytes))
            .WithTags("Upload")
            .WithName("UploadVideo");

        return app;
    }

    private static async Task<IResult> UploadImageAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        IAdminAuthorizationService adminAuthorization,
        IUploadImageStorage storage,
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
            accountStatus,
            adminAuthorization,
            cancellationToken);
        if (validationError is not null) return validationError;

        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        var fileValidation = await ValidateFileAsync(
            file,
            scene == SessionScene ? MaxSessionImageBytes : MaxFileSizeBytes,
            cancellationToken);
        if (fileValidation.Error is not null) return fileValidation.Error;

        var response = await storage.SaveAsync(
            file!,
            scene,
            fileValidation.Extension!,
            cancellationToken);
        return Results.Ok(ApiResponse.Ok(response));
    }

    private static async Task<IResult> UploadVideoAsync(
        HttpRequest request,
        IAdminAuthorizationService adminAuthorization,
        IUploadVideoStorage storage,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "必须使用 multipart/form-data", 400);

        var form = await request.ReadFormAsync(cancellationToken);
        var scene = form["scene"].ToString();
        var sceneError = ValidateVideoScene(scene);
        if (sceneError is not null) return sceneError;

        var authError = await ValidateAdminAsync(request, adminAuthorization, cancellationToken);
        if (authError is not null) return authError;

        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        var fileError = await ValidateVideoFileAsync(file, cancellationToken);
        if (fileError is not null) return fileError;

        var response = await storage.SaveAsync(file!, cancellationToken);
        return Results.Ok(ApiResponse.Ok(response));
    }

    private static async Task<IResult?> ValidateAsync(
        string scene,
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
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
            var user = await EndpointUsers.RequireActiveUserAsync(
                request,
                new ActiveUserDependencies(tokenService, userStore, accountStatus),
                cancellationToken);
            return user.Failure;
        }

        return await ValidateAdminAsync(request, adminAuthorization, cancellationToken);
    }

    private static async Task<IResult?> ValidateAdminAsync(
        HttpRequest request,
        IAdminAuthorizationService adminAuthorization,
        CancellationToken cancellationToken)
    {
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

    private static IResult? ValidateVideoScene(string scene)
    {
        return scene == CourseScene ? null
            : EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "scene 不支持", 400);
    }

    private static async Task<FileValidationResult> ValidateFileAsync(
        IFormFile? file,
        long maxFileSizeBytes,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return FileValidationResult.Fail(
                EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "file 不能为空", 400));
        }

        if (file.Length > maxFileSizeBytes)
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

    private static async Task<IResult?> ValidateVideoFileAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "file 不能为空", 400);
        if (file.Length > MaxVideoSizeBytes)
            return EndpointFailures.JsonFailure(ErrorCodes.FileTooLarge, "文件超过大小限制", 400);
        if (!IsVideoContentTypeCompatible(file.ContentType))
            return EndpointFailures.JsonFailure(ErrorCodes.FileTypeNotAllowed, "文件 MIME 类型不匹配", 400);

        return await IsMp4Async(file, cancellationToken) ? null
            : EndpointFailures.JsonFailure(ErrorCodes.FileTypeNotAllowed, "不支持的文件格式", 400);
    }

    private static async Task<bool> IsMp4Async(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var header = new byte[Mp4SignatureReadLength];
        await using var stream = file.OpenReadStream();
        var length = await stream.ReadAsync(header, cancellationToken);
        return length >= 8 && header.AsSpan(4, FtypSignature.Length).SequenceEqual(FtypSignature);
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

    private static bool IsVideoContentTypeCompatible(string contentType)
    {
        return string.IsNullOrWhiteSpace(contentType)
            || contentType.Equals(OctetStreamContentType, StringComparison.OrdinalIgnoreCase)
            || contentType.Equals(Mp4ContentType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameImageContentType(string actual, string allowed)
    {
        return actual.Equals(allowed, StringComparison.OrdinalIgnoreCase)
            || (allowed == "image/jpeg"
                && actual.Equals("image/jpg", StringComparison.OrdinalIgnoreCase));
    }

}
