using AedBackend.Api.WebSockets;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Help;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Upload;
using AedBackend.Infrastructure.WeChat;

namespace AedBackend.Api.Endpoints;

internal sealed record HelpReportInput
{
    public required string HelpId { get; init; }
    public required HelpReportRequest Request { get; init; }
    public required WeChatOptions WeChatOptions { get; init; }
    public required UploadOptions UploadOptions { get; init; }
    public required CertifiedResponderResult Responder { get; init; }
    public required IHelpRequestStore HelpStore { get; init; }
    public required RescueSessionLifecycleCoordinator Lifecycle { get; init; }
    public required RescueSessionBroadcaster Broadcaster { get; init; }
    public required HelpClosureApplication Closure { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}

/** 救助报告用例：边界校验、关单围栏、会话清理与状态扇出。 */
internal static class HelpReportFlow
{
    public static async Task<IResult> ExecuteAsync(HelpReportInput input)
    {
        var validationError = HelpPublishFlow.ValidateReport(
            input.Request,
            input.WeChatOptions,
            input.UploadOptions);
        if (validationError is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.HelpReportMissingFields, validationError, 400);

        var now = DateTimeOffset.UtcNow;
        var report = CreateReport(input, now);
        var existing = await input.HelpStore.FindByIdOrRequestNoAsync(
            input.HelpId,
            input.CancellationToken);
        if (existing is null)
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "求助不存在", 404);

        if (input.Request.IsFalseAlarm)
        {
            return await HelpEndpoints.UpdateHelpAsync(
                () => input.Closure.CloseAsync(
                    new HelpClosureCommand
                    {
                        HelpId = existing.Id,
                        Status = HelpStatuses.FalseAlarm,
                        FalseAlarmReport = report
                    },
                    input.CancellationToken),
                record => Results.Ok(ApiResponse.Ok(
                    new HelpReportResponse(report.Id, record.Status, report.SubmittedAt))));
        }

        using var lease = await input.Lifecycle.EnterAsync(existing.Id, input.CancellationToken);
        return await HelpEndpoints.UpdateHelpAsync(
            () => PersistAsync(input, existing.Id, report),
            record => Results.Ok(ApiResponse.Ok(new HelpReportResponse(report.Id, record.Status, now))));
    }

    private static HelpReportRecord CreateReport(HelpReportInput input, DateTimeOffset now)
    {
        return new HelpReportRecord(
            Guid.NewGuid().ToString("N"),
            input.Responder.User!.OpenId,
            input.Request.Images!,
            input.Request.RecordText.Trim(),
            input.Request.IsFalseAlarm,
            now);
    }

    private static async Task<HelpRequestRecord> PersistAsync(
        HelpReportInput input,
        string helpId,
        HelpReportRecord report)
    {
        var updated = await input.HelpStore.AddReportAsync(
            new HelpReportUpdate(
                helpId,
                report,
                HelpStatuses.PendingReview,
                report.SubmittedAt),
            input.CancellationToken);
        await input.Broadcaster.BroadcastAsync(
            updated.Id,
            "help_status",
            new RescueHelpStatusDto(updated.Status));
        return updated;
    }
}
