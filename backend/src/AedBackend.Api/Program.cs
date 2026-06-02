using AedBackend.Api;
using AedBackend.Api.Endpoints;
using AedBackend.Core.DTOs.Common;
using AedBackend.Infrastructure.Extensions;
using AedBackend.Infrastructure.Persistence;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

var cliExitCode = await JsonToSqliteCli.TryRunAsync(args, CancellationToken.None);
if (cliExitCode >= 0) return cliExitCode;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddWeChatInfrastructure(builder.Configuration);

var app = builder.Build();
await RuntimeDataInitializer.EnsureReadyAsync(app.Services, CancellationToken.None);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var storageOptions = app.Services.GetRequiredService<IOptions<WeChatOptions>>().Value;
Directory.CreateDirectory(storageOptions.UploadsDirectory);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(storageOptions.UploadsDirectory)),
    RequestPath = "/uploads"
});

var adminStaticRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "admin");
if (Directory.Exists(adminStaticRoot))
{
    var adminFileProvider = new PhysicalFileProvider(adminStaticRoot);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = adminFileProvider,
        RequestPath = "/admin"
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = adminFileProvider,
        RequestPath = "/admin"
    });
}

app.MapGet("/", () => ApiResponse.Ok(new { service = "AedBackend", status = "ok" }));
app.MapGet("/health", () => ApiResponse.Ok(new { status = "healthy" }));
app.MapWeChatAuthEndpoints();
app.MapWeChatSubscriptionEndpoints();
app.MapWeChatDevEndpoints();
app.MapUploadEndpoints();
app.MapAedEndpoints();
app.MapHelpEndpoints();
app.MapRescuerEndpoints();
app.MapUserEndpoints();
app.MapKnowledgeEndpoints();
app.MapCourseEndpoints().MapQuickActionEndpoints();
app.MapOpsEndpoints();
app.MapAdminEndpoints();

app.Run();

return 0;

public partial class Program;
