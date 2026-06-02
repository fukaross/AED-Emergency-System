using AedBackend.Core.Models.Help;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal static partial class SqliteMapper
{
    public static HelpRequestEntity ToEntity(HelpRequestRecord item)
    {
        return new HelpRequestEntity
        {
            Id = item.Id,
            RequestNo = item.RequestNo,
            UserId = item.UserId,
            RequestChannel = item.RequestChannel,
            Lat = item.Lat,
            Lng = item.Lng,
            Address = item.Address,
            Content = item.Content,
            Type = item.Type,
            Status = item.Status,
            CreatedAt = item.CreatedAt,
            ClosedAt = item.ClosedAt,
            CloseNote = item.CloseNote,
            RequesterPhone = item.RequesterPhone,
            GuestSession = item.GuestSession is null ? null : ToEntity(item.GuestSession),
            Images = item.Images.Select((url, index) => new HelpImageEntity { Order = index + 1, Url = url }).ToList(),
            Responders = item.Responders.Select(ToEntity).ToList(),
            Timeline = item.Timeline.Select((record, index) => ToEntity(record, index)).ToList(),
            Reports = item.Reports.Select(ToEntity).ToList()
        };
    }

    public static HelpRequestRecord ToRecord(HelpRequestEntity item)
    {
        return new HelpRequestRecord
        {
            Id = item.Id,
            RequestNo = item.RequestNo,
            UserId = item.UserId,
            RequestChannel = item.RequestChannel,
            Lat = item.Lat,
            Lng = item.Lng,
            Address = item.Address,
            Content = item.Content,
            Type = item.Type,
            Status = item.Status,
            CreatedAt = item.CreatedAt,
            ClosedAt = item.ClosedAt,
            CloseNote = item.CloseNote,
            RequesterPhone = item.RequesterPhone,
            GuestSession = item.GuestSession is null ? null : ToRecord(item.GuestSession),
            Images = item.Images.OrderBy(i => i.Order).Select(i => i.Url).ToArray(),
            Responders = item.Responders.Select(ToRecord).ToArray(),
            Timeline = item.Timeline.OrderBy(t => t.Order).Select(ToRecord).ToArray(),
            Reports = item.Reports.Select(ToRecord).ToArray()
        };
    }

    private static HelpGuestSessionEntity ToEntity(HelpGuestSessionRecord item)
    {
        return new HelpGuestSessionEntity
        {
            GuestName = item.GuestName,
            FollowupCode = item.FollowupCode,
            CloseToken = item.CloseToken,
            AnonymousId = item.AnonymousId,
            CallbackAllowed = item.CallbackAllowed,
            PrivacyConsentAt = item.PrivacyConsentAt
        };
    }

    private static HelpGuestSessionRecord ToRecord(HelpGuestSessionEntity item)
    {
        return new HelpGuestSessionRecord(
            item.GuestName,
            item.FollowupCode,
            item.CloseToken,
            item.AnonymousId,
            item.CallbackAllowed,
            item.PrivacyConsentAt);
    }

    private static HelpResponderEntity ToEntity(HelpResponderRecord item)
    {
        return new HelpResponderEntity
        {
            OpenId = item.OpenId,
            Name = item.Name,
            Role = item.Role,
            Lat = item.Lat,
            Lng = item.Lng,
            EtaSeconds = item.EtaSeconds,
            RespondedAt = item.RespondedAt
        };
    }

    private static HelpResponderRecord ToRecord(HelpResponderEntity item)
    {
        return new HelpResponderRecord(
            item.OpenId,
            item.Name,
            item.Role,
            item.Lat,
            item.Lng,
            item.EtaSeconds,
            item.RespondedAt);
    }

    private static HelpTimelineEntity ToEntity(HelpTimelineRecord item, int index)
    {
        return new HelpTimelineEntity
        {
            Order = index + 1,
            Action = item.Action,
            Timestamp = item.Timestamp,
            Note = item.Note
        };
    }

    private static HelpTimelineRecord ToRecord(HelpTimelineEntity item)
    {
        return new HelpTimelineRecord(item.Action, item.Timestamp, item.Note);
    }

    private static HelpReportEntity ToEntity(HelpReportRecord item)
    {
        return new HelpReportEntity
        {
            Id = item.Id,
            ReporterOpenId = item.ReporterOpenId,
            RecordText = item.RecordText,
            IsFalseAlarm = item.IsFalseAlarm,
            SubmittedAt = item.SubmittedAt,
            Images = item.Images.Select((url, index) =>
                new HelpReportImageEntity { Order = index + 1, Url = url }).ToList()
        };
    }

    private static HelpReportRecord ToRecord(HelpReportEntity item)
    {
        return new HelpReportRecord(
            item.Id,
            item.ReporterOpenId,
            item.Images.OrderBy(i => i.Order).Select(i => i.Url).ToArray(),
            item.RecordText,
            item.IsFalseAlarm,
            item.SubmittedAt);
    }
}
