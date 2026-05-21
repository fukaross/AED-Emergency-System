using AedBackend.Api.Endpoints;
using AedBackend.Core.DTOs.Common;
using AedBackend.Infrastructure.Extensions;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddWeChatInfrastructure(builder.Configuration);

var app = builder.Build();

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

app.MapGet("/", () => ApiResponse.Ok(new { service = "AedBackend", status = "ok" }));
app.MapGet("/health", () => ApiResponse.Ok(new { status = "healthy" }));
app.MapWeChatAuthEndpoints();
app.MapWeChatSubscriptionEndpoints();
app.MapWeChatDevEndpoints();
app.MapUploadEndpoints();
app.MapAedEndpoints();
app.MapHelpEndpoints();
app.MapOpsEndpoints();

app.Run();
