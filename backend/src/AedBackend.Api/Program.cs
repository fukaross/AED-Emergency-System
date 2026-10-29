using AedBackend.Api;
using AedBackend.Api.Endpoints;
using AedBackend.Api.WebSockets;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Demo;
using AedBackend.Infrastructure.Extensions;
using AedBackend.Infrastructure.Persistence;
using AedBackend.Infrastructure.Upload;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

var demoInitExitCode = await InitDemoDataCli.TryRunAsync(args, CancellationToken.None);
if (demoInitExitCode >= 0) return demoInitExitCode;
var cliExitCode = await JsonToSqliteCli.TryRunAsync(args, CancellationToken.None);
if (cliExitCode >= 0) return cliExitCode;
cliExitCode = await UploadsToOssCli.TryRunAsync(args, CancellationToken.None);
if (cliExitCode >= 0) return cliExitCode;

var builder = WebApplication.CreateBuilder(args);
DevelopmentLocalSecrets.Apply(builder, args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSiteConfiguration();
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
ValidateDemoMode(app);
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

/**
 * CONTRACT-003 启动门禁：Demo 只在 Development + Demo:Enabled=true + 仅回环监听
 * （且本地上传、本机回链、SQLite 数据源）时可用；不满足即启动失败。
 */
static void ValidateDemoMode(WebApplication app)
{
    var demo = app.Services.GetRequiredService<IOptions<DemoOptions>>().Value;
    if (!demo.Enabled) return;

    DemoModeGuard.ValidateStartup(
        app.Configuration,
        app.Environment,
        demo,
        app.Services.GetRequiredService<IOptions<DataOptions>>().Value,
        app.Services.GetRequiredService<IOptions<UploadOptions>>().Value,
        app.Services.GetRequiredService<IOptions<WeChatOptions>>().Value,
        app.Services.GetRequiredService<DemoRoleCatalog>());
}

public partial class Program;
