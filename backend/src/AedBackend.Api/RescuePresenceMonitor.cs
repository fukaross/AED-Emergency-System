using AedBackend.Api.Endpoints;
using AedBackend.Api.WebSockets;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Rescue;
using Microsoft.Extensions.Options;

namespace AedBackend.Api;

/** 会话在线状态监控：周期性扫描活动会话，按 3 分钟阈值追加 offline/online 事件并广播。 */
public sealed class RescuePresenceMonitor : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<RescuePresenceMonitor> logger;

    public RescuePresenceMonitor(
        IServiceScopeFactory scopeFactory,
        ILogger<RescuePresenceMonitor> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "急救会话在线状态扫描失败");
            }

            try
            {
                await Task.Delay(ScanInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ScanOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var helpStore = scope.ServiceProvider.GetRequiredService<IHelpRequestStore>();
        var sessionStore = scope.ServiceProvider.GetRequiredService<IRescueSessionStore>();
        var broadcaster = scope.ServiceProvider.GetRequiredService<RescueSessionBroadcaster>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<RescueOptions>>().Value;
        var active = await helpStore.ListOverviewsAsync(
            new HelpRequestOverviewQuery
            {
                Scope = HelpRequestOverviewScope.Active,
                Offset = 0,
                Limit = null
            },
            ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var help in active.Items)
        {
            try
            {
                await ScanSessionAsync(sessionStore, broadcaster, options, help.Id, now, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "扫描会话 {HelpId} 在线状态失败", help.Id);
            }
        }
    }

    private static async Task ScanSessionAsync(
        IRescueSessionStore sessionStore,
        RescueSessionBroadcaster broadcaster,
        RescueOptions options,
        string helpId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var session = await sessionStore.FindByHelpIdAsync(helpId, ct);
        if (session is null || session.ClosedAt is not null) return;

        var commands = RescuePresencePlanner.Plan(
            session,
            now,
            options.OfflineNotifyAfterSeconds);
        foreach (var command in commands)
        {
            var result = await sessionStore.AppendStatusEventAsync(
                new RescueSessionStatusCommand
                {
                    HelpRequestId = helpId,
                    SenderOpenId = command.OpenId,
                    SenderName = command.DisplayName,
                    EventType = command.EventType,
                    Action = RescueEventActions.Mark,
                    Now = now
                },
                ct);
            if (result.Appended && result.Event is not null)
            {
                await broadcaster.BroadcastAsync(
                    helpId,
                    "message",
                    RescueSessionMapper.ToMessage(result.Event));
            }
        }
    }
}
