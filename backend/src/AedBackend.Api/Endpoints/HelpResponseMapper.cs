using AedBackend.Core.DTOs.Help;
using AedBackend.Core.Models.Help;

namespace AedBackend.Api.Endpoints;

internal static class HelpResponseMapper
{
    public static HelpListItem ToListItem(HelpRequestRecord record, string? userId = null)
    {
        var userRole = userId is null || record.UserId == userId ? "requester" : "reporter";
        return new HelpListItem(
            record.Id,
            record.RequestNo,
            record.Content,
            record.Type,
            record.Status,
            record.Address,
            record.CreatedAt,
            record.Responders.Count,
            GetEstimatedArrivalTime(record),
            userRole);
    }

    public static HelpDetailResponse ToDetail(HelpRequestRecord record)
    {
        return new HelpDetailResponse(
            record.Id,
            record.RequestNo,
            record.Content,
            record.Type,
            record.Status,
            record.Address,
            record.Lat,
            record.Lng,
            record.RequesterPhone,
            record.CreatedAt,
            record.Images,
            record.Responders.Select(ToResponder).ToArray(),
            GetEstimatedArrivalTime(record),
            record.Timeline.Select(ToTimeline).ToArray(),
            record.Reports.Select(ToReport).ToArray());
    }

    public static GuestHelpLookupResponse ToGuestLookup(HelpRequestRecord record)
    {
        return new GuestHelpLookupResponse(
            record.RequestNo,
            record.Status,
            record.Content,
            record.Address,
            record.CreatedAt,
            record.Responders.Count,
            GetEstimatedArrivalTime(record),
            record.Status == HelpStatuses.Published && record.GuestSession is not null,
            record.ClosedAt);
    }

    private static HelpResponderResponse ToResponder(HelpResponderRecord responder)
    {
        // TODO: Compute from responder and help coordinates when response records are enabled.
        return new HelpResponderResponse(
            responder.Name,
            responder.Role,
            0,
            FormatEta(responder.EtaSeconds));
    }

    private static HelpTimelineEntryResponse ToTimeline(HelpTimelineRecord item)
    {
        return new HelpTimelineEntryResponse(item.Action, item.Timestamp, item.Note);
    }

    private static HelpReportRecordResponse ToReport(HelpReportRecord report)
    {
        return new HelpReportRecordResponse(
            report.Id,
            report.ReporterOpenId,
            report.Images,
            report.RecordText,
            report.IsFalseAlarm,
            report.SubmittedAt);
    }

    private static string GetEstimatedArrivalTime(HelpRequestRecord record)
    {
        var eta = record.Responders
            .Where(item => item.EtaSeconds is > 0)
            .MinBy(item => item.EtaSeconds);

        return eta is null ? "待响应" : FormatEta(eta.EtaSeconds);
    }

    private static string FormatEta(int? etaSeconds)
    {
        if (etaSeconds is null or <= 0)
        {
            return "待响应";
        }

        return $"{Math.Max(1, (int)Math.Ceiling(etaSeconds.Value / 60d))}分钟";
    }
}
