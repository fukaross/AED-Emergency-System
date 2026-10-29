using AedBackend.Api;
using AedBackend.Api.Endpoints;
using AedBackend.Api.WebSockets;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Extensions;
using AedBackend.Infrastructure.Persistence;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

var cliExitCode = await JsonToSqliteCli.TryRunAsync(args, CancellationToken.None);
if (cliExitCode >= 0) return cliExitCode;
cliExitCode = await UploadsToOssCli.TryRunAsync(args, CancellationToken.None);
if (cliExitCode >= 0) return cliExitCode;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddWeChatInfrastructure(builder.Configuration);
builder.Services.AddSingleton<RescueSessionLifecycleCoordinator>();
builder.Services.AddSingleton(services => new RescueAuthorizationDependencies
{
    Tokens = services.GetRequiredService<ISessionTokenService>(),
    Users = services.GetRequiredService<IExperienceUserStore>(),
    AccountStatus = services.GetRequiredService<IUserAccountStatusService>(),
    Responders = services.GetRequiredService<IResponderStore>(),
    Helps = services.GetRequiredService<IHelpRequestStore>(),
    Sessions = services.GetRequiredService<IRescueSessionStore>(),
});
builder.Services.AddSingleton<RescueSessionAuthorizer>();
builder.Services.AddSingleton<IRescueConnectionAuthorizer>(
    services => services.GetRequiredService<RescueSessionAuthorizer>());
builder.Services.AddSingleton<RescueSessionBroadcaster>();
builder.Services.AddSingleton<RescueWebSocketReceivePump>();
builder.Services.AddSingleton(services => new HelpClosureDependencies
{
    HelpStore = services.GetRequiredService<IHelpRequestStore>(),
    SessionStore = services.GetRequiredService<IRescueSessionStore>(),
    AuditLogs = services.GetRequiredService<IAdminAuditLogStore>(),
    Lifecycle = services.GetRequiredService<RescueSessionLifecycleCoordinator>(),
    Broadcaster = services.GetRequiredService<RescueSessionBroadcaster>(),
});
builder.Services.AddSingleton<HelpClosureApplication>();
if (builder.Configuration.GetValue<bool>("Rescue:PresenceMonitorEnabled", true))
{
    builder.Services.AddHostedService<RescuePresenceMonitor>();
}

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
app.UseWebSockets(RescueWebSocketRuntimeOptions.Create());
app.MapWeChatAuthEndpoints();
app.MapWeChatSubscriptionEndpoints();
app.MapWeChatDevEndpoints();
app.MapUploadEndpoints();
app.MapAedEndpoints();
app.MapHelpEndpoints();
app.MapRescueSessionEndpoints();
app.MapRescueWebSocketEndpoints();
app.MapRescuerEndpoints();
app.MapUserEndpoints();
app.MapKnowledgeEndpoints();
app.MapCourseEndpoints().MapQuickActionEndpoints();
app.MapOpsEndpoints();
app.MapAdminEndpoints();

app.Run();

return 0;

public partial class Program;
