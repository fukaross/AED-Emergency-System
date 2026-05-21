using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Upload;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

public static class UploadEndpoints
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
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
            cancellationToken);
        if (validationError is not null) return validationError;

        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        validationError = ValidateFile(file);
        if (validationError is not null) return validationError;

        var response = await SaveFileAsync(file!, scene, options.Value, cancellationToken);
        return Results.Ok(ApiResponse.Ok(response));
    }

    private static async Task<IResult?> ValidateAsync(
        string scene,
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        CancellationToken cancellationToken)
    {
        if (!PublicScenes.Contains(scene) && !AuthScenes.Contains(scene))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "scene 不支持", 400);
        }

        if (PublicScenes.Contains(scene)) return null;

        var user = await EndpointUsers.FindCurrentUserAsync(
            request,
            tokenService,
            userStore,
            cancellationToken);
        return user is null ? EndpointFailures.JsonFailure(ErrorCodes.Unauthorized, "登录 token 无效", 401) : null;
    }

    private static IResult? ValidateFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "file 不能为空", 400);
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.FileTooLarge, "文件超过大小限制", 400);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedContentTypes.TryGetValue(extension, out var contentType))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.FileTypeNotAllowed, "不支持的文件格式", 400);
        }

        return file.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase)
            ? null
            : EndpointFailures.JsonFailure(ErrorCodes.FileTypeNotAllowed, "文件 MIME 类型不匹配", 400);
    }

    private static async Task<UploadImageResponse> SaveFileAsync(
        IFormFile file,
        string scene,
        WeChatOptions options,
        CancellationToken cancellationToken)
    {
        var fileId = Guid.NewGuid().ToString("N");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var directory = Path.GetFullPath(Path.Combine(options.UploadsDirectory, scene));
        Directory.CreateDirectory(directory);

        var fileName = $"{fileId}{extension}";
        var filePath = Path.Combine(directory, fileName);
        await using var stream = File.Create(filePath);
        await file.CopyToAsync(stream, cancellationToken);

        var url = $"{options.PublicBaseUrl.TrimEnd('/')}/uploads/{scene}/{fileName}";
        return new UploadImageResponse(url, url, fileId);
    }
}
