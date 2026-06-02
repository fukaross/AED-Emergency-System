using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Admin;

namespace AedBackend.Api.Endpoints;

internal static class AdminEndpointHelpers
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;

    public static string? ReadAdminKey(HttpRequest request)
    {
        return request.Headers.TryGetValue(EndpointSecurity.AdminKeyHeader, out var values)
            ? values.ToString()
            : null;
    }

    public static string? GetClientIp(HttpContext context)
    {
        return AdminClientIp.Resolve(context);
    }

    public static string? GetUserAgent(HttpRequest request)
    {
        return request.Headers.UserAgent.ToString();
    }

    public static async Task<AdminPrincipal?> RequireAdminAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var result = await authorization.RequireAdminSessionAsync(
            ReadAdminKey(context.Request),
            GetClientIp(context),
            GetUserAgent(context.Request),
            cancellationToken);

        return result.Succeeded ? result.Principal : null;
    }

    public static IResult AuthorizationFailure(AdminAuthorizationResult result)
    {
        return EndpointFailures.JsonFailure(result.ErrorCode, result.ErrorMessage, result.StatusCode);
    }

    public static IResult Forbidden()
    {
        return EndpointFailures.JsonFailure(ErrorCodes.Forbidden, "需要超级管理员权限", 403);
    }

    public static AdminAuditLogRecord CreateAudit(
        string eventType,
        string eventName,
        AdminPrincipal? actor,
        string? targetId,
        string? summary,
        HttpContext context,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        return new AdminAuditLogRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            EventType = eventType,
            EventName = eventName,
            TargetId = targetId,
            ActorType = actor is null ? "system" : "admin",
            ActorId = actor?.Id,
            ActorName = actor?.Username,
            ActorRole = actor?.Role,
            Summary = summary,
            Metadata = metadata,
            IpAddress = GetClientIp(context),
            UserAgent = GetUserAgent(context.Request),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public static AdminHelpListResponse ToAdminHelpPage(
        IReadOnlyList<HelpRequestRecord> records,
        int? page,
        int? pageSize)
    {
        var safePage = Math.Max(DefaultPage, page ?? DefaultPage);
        var safePageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, 100);
        var items = records.Select(ToAdminHelpListItem).ToArray();
        var paged = items.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToArray();
        return new AdminHelpListResponse(
            paged,
            items.Length,
            safePage,
            safePageSize,
            Math.Max(1, (int)Math.Ceiling(items.Length / (double)safePageSize)));
    }

    public static AdminHelpListItem ToAdminHelpListItem(HelpRequestRecord record)
    {
        return new AdminHelpListItem(
            record.Id,
            record.RequestNo,
            record.RequestChannel,
            record.Content,
            record.Type,
            record.Status,
            record.Address,
            record.CreatedAt,
            record.ClosedAt,
            record.CloseNote,
            GetReporters(record).Count,
            record.Responders.Count,
            GetEta(record));
    }

    public static AdminHelpDetailResponse ToAdminHelpDetail(HelpRequestRecord record)
    {
        var listItem = ToAdminHelpListItem(record);
        return new AdminHelpDetailResponse(
            listItem.Id,
            listItem.RequestNo,
            listItem.RequestChannel,
            listItem.Content,
            listItem.Type,
            listItem.Status,
            listItem.Address,
            listItem.CreatedAt,
            listItem.ClosedAt,
            listItem.CloseNote,
            listItem.ReportersCount,
            listItem.RespondersCount,
            listItem.LatestEta,
            record.Images,
            record.Reports.Select(ToReport).ToArray(),
            GetReporters(record),
            record.Timeline.Select(ToTimeline).ToArray(),
            record.Responders.Select(ToResponder).ToArray());
    }

    public static AdminAuditLogPage ToAuditPage(
        IReadOnlyList<AdminAuditLogRecord> logs,
        int? page,
        int? pageSize)
    {
        var safePage = Math.Max(DefaultPage, page ?? DefaultPage);
        var safePageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, 100);
        var items = logs.Select(ToAuditDto).ToArray();
        var paged = items.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToArray();
        return new AdminAuditLogPage(
            paged,
            items.Length,
            safePage,
            safePageSize,
            Math.Max(1, (int)Math.Ceiling(items.Length / (double)safePageSize)));
    }

    public static AdminAuditLogDto ToAuditDto(AdminAuditLogRecord log)
    {
        return new AdminAuditLogDto(
            log.Id,
            log.EventType,
            log.EventName,
            log.TargetId,
            log.ActorType,
            log.ActorId,
            log.ActorName,
            log.ActorRole,
            log.Summary,
            log.Metadata,
            log.IpAddress,
            log.UserAgent,
            log.CreatedAt);
    }

    public static AdminDashboardRecentLogItem ToRecentLog(AdminAuditLogRecord log)
    {
        return new AdminDashboardRecentLogItem(
            log.Id,
            log.EventType,
            log.EventName,
            log.ActorName,
            log.Summary,
            log.CreatedAt);
    }

    private static AdminHelpReportRecord ToReport(HelpReportRecord report)
    {
        return new AdminHelpReportRecord(
            report.Id,
            report.ReporterOpenId ?? string.Empty,
            report.RecordText,
            report.Images,
            report.SubmittedAt);
    }

    private static IReadOnlyList<AdminHelpReporter> GetReporters(HelpRequestRecord record)
    {
        return record.Reports
            .GroupBy(ReporterKey)
            .Select(group => ToReporter(group.Key, group))
            .OrderByDescending(item => item.LastReportedAt)
            .ToArray();
    }

    private static string ReporterKey(HelpReportRecord report)
    {
        return string.IsNullOrWhiteSpace(report.ReporterOpenId)
            ? $"unknown:{report.Id}"
            : report.ReporterOpenId;
    }

    private static AdminHelpReporter ToReporter(
        string key,
        IEnumerable<HelpReportRecord> reports)
    {
        var items = reports.ToArray();
        var openId = key.StartsWith("unknown:", StringComparison.Ordinal)
            ? string.Empty
            : key;

        return new AdminHelpReporter(
            openId,
            CreateReporterDisplayName(openId),
            items.Length,
            items.Max(item => item.SubmittedAt));
    }

    private static string CreateReporterDisplayName(string openId)
    {
        if (string.IsNullOrWhiteSpace(openId)) return "未知上传者";
        return openId.Length <= 10 ? openId : $"{openId[..6]}...{openId[^4..]}";
    }

    private static AdminHelpTimelineEntry ToTimeline(HelpTimelineRecord item)
    {
        return new AdminHelpTimelineEntry(item.Action, item.Note ?? item.Action, item.Timestamp);
    }

    private static AdminHelpResponder ToResponder(HelpResponderRecord responder)
    {
        return new AdminHelpResponder(
            responder.OpenId,
            responder.Name,
            FormatEta(responder.EtaSeconds),
            null);
    }

    private static string? GetEta(HelpRequestRecord record)
    {
        var eta = record.Responders
            .Where(item => item.EtaSeconds is > 0)
            .MinBy(item => item.EtaSeconds);

        return eta is null ? null : FormatEta(eta.EtaSeconds);
    }

    private static string? FormatEta(int? etaSeconds)
    {
        return etaSeconds is null or <= 0
            ? null
            : $"{Math.Max(1, (int)Math.Ceiling(etaSeconds.Value / 60d))}分钟";
    }
}
