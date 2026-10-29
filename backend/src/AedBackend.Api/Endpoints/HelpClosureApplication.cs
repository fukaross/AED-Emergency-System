using System.Security.Cryptography;
using System.Text;
using AedBackend.Api.WebSockets;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;

namespace AedBackend.Api.Endpoints;

internal sealed record HelpClosureCommand
{
    public required string HelpId { get; init; }
    public required string Status { get; init; }
    public string? Note { get; init; }
    public HelpReportRecord? FalseAlarmReport { get; init; }
    public AdminAuditLogRecord? Audit { get; init; }
}

internal sealed record HelpClosureDependencies
{
    public required IHelpRequestStore HelpStore { get; init; }
    public required IRescueSessionStore SessionStore { get; init; }
    public required IAdminAuditLogStore AuditLogs { get; init; }
    public required RescueSessionLifecycleCoordinator Lifecycle { get; init; }
    public required RescueSessionBroadcaster Broadcaster { get; init; }
}

/**
 * 求助终态应用模块：在单一生命周期租约内收口 help、session、WS 与管理审计。
 * 一旦开始首笔持久化，后续一致性写入不再受客户端断开取消。
 */
internal sealed class HelpClosureApplication
{
    private readonly IAdminAuditLogStore auditLogs;
    private readonly RescueSessionBroadcaster broadcaster;
    private readonly IHelpRequestStore helpStore;
    private readonly RescueSessionLifecycleCoordinator lifecycle;
    private readonly IRescueSessionStore sessionStore;

    public HelpClosureApplication(HelpClosureDependencies dependencies)
    {
        helpStore = dependencies.HelpStore;
        sessionStore = dependencies.SessionStore;
        auditLogs = dependencies.AuditLogs;
        lifecycle = dependencies.Lifecycle;
        broadcaster = dependencies.Broadcaster;
    }

    /** 关闭求助并确保所有终态副作用完成；重复关闭在修复副作用后抛出明确领域冲突。 */
    public async Task<HelpRequestRecord> CloseAsync(
        HelpClosureCommand command,
        CancellationToken requestCancellation)
    {
        Validate(command);
        using var lease = await lifecycle.EnterAsync(command.HelpId, requestCancellation);
        var current = await FindRequiredAsync(command.HelpId, requestCancellation);
        var wasAlreadyClosed = HelpStatuses.IsClosed(current.Status);
        var completionToken = CancellationToken.None;

        if (!wasAlreadyClosed)
        {
            try
            {
                current = await PersistClosureAsync(command, completionToken);
            }
            catch (HelpAlreadyClosedException)
            {
                current = await FindRequiredAsync(command.HelpId, completionToken);
                wasAlreadyClosed = true;
            }
        }

        await CompleteClosureAsync(current, command.Audit, completionToken);
        if (wasAlreadyClosed) throw new HelpAlreadyClosedException();
        return current;
    }

    private async Task<HelpRequestRecord> PersistClosureAsync(
        HelpClosureCommand command,
        CancellationToken cancellationToken)
    {
        var closedAt = command.FalseAlarmReport?.SubmittedAt ?? DateTimeOffset.UtcNow;
        if (command.FalseAlarmReport is not null)
        {
            return await helpStore.AddReportAsync(
                new HelpReportUpdate(
                    command.HelpId,
                    command.FalseAlarmReport,
                    HelpStatuses.FalseAlarm,
                    closedAt),
                cancellationToken);
        }

        return await helpStore.CloseAsync(
            new HelpClosureUpdate
            {
                HelpRequestId = command.HelpId,
                Status = command.Status,
                Note = command.Note,
                ClosedAt = closedAt
            },
            cancellationToken);
    }

    private async Task CompleteClosureAsync(
        HelpRequestRecord record,
        AdminAuditLogRecord? audit,
        CancellationToken cancellationToken)
    {
        await sessionStore.CloseSessionAsync(
            record.Id,
            record.ClosedAt ?? DateTimeOffset.UtcNow,
            cancellationToken);
        await broadcaster.BroadcastTerminalAndCloseAsync(
            record.Id,
            new RescueHelpStatusDto(record.Status),
            "help closed");
        if (audit is not null)
        {
            await auditLogs.AppendAsync(NormalizeAudit(audit, record), cancellationToken);
        }
    }

    private async Task<HelpRequestRecord> FindRequiredAsync(
        string helpId,
        CancellationToken cancellationToken)
    {
        return await helpStore.FindByIdOrRequestNoAsync(helpId, cancellationToken)
            ?? throw new KeyNotFoundException("求助不存在");
    }

    private static AdminAuditLogRecord NormalizeAudit(
        AdminAuditLogRecord audit,
        HelpRequestRecord record)
    {
        var eventName = record.Status == HelpStatuses.FalseAlarm
            ? "help.false_alarm"
            : "help.close";
        var metadata = new Dictionary<string, object?>(audit.Metadata ??
            new Dictionary<string, object?>())
        {
            ["status"] = record.Status
        };
        return audit with
        {
            Id = CreateAuditId(record),
            EventName = eventName,
            TargetId = record.Id,
            Metadata = metadata,
            CreatedAt = record.ClosedAt ?? audit.CreatedAt
        };
    }

    private static string CreateAuditId(HelpRequestRecord record)
    {
        var material = $"help-close\n{record.Id}\n{record.Status}\n{record.ClosedAt:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
    }

    private static void Validate(HelpClosureCommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.HelpId);
        if (command.Status is not (HelpStatuses.Closed or HelpStatuses.FalseAlarm))
            throw new ArgumentOutOfRangeException(nameof(command), "不支持的求助终态");
        if (command.FalseAlarmReport is not null && command.Status != HelpStatuses.FalseAlarm)
            throw new ArgumentException("误报报告只能进入 false_alarm", nameof(command));
    }
}
