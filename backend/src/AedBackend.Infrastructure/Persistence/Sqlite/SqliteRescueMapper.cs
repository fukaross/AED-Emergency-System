using AedBackend.Core.Models.Rescue;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal static partial class SqliteMapper
{
    public static RescueSessionEntity ToEntity(RescueSessionRecord item)
    {
        return new RescueSessionEntity
        {
            HelpRequestId = item.HelpRequestId,
            CreatedAt = item.CreatedAt,
            ClosedAt = item.ClosedAt,
            Participants = item.Participants.Select(p => ToEntity(item.HelpRequestId, p)).ToList(),
            Messages = item.Messages.Select(m => ToEntity(item.HelpRequestId, m)).ToList()
        };
    }

    public static RescueSessionRecord ToRecord(RescueSessionEntity item)
    {
        return new RescueSessionRecord
        {
            HelpRequestId = item.HelpRequestId,
            CreatedAt = item.CreatedAt,
            ClosedAt = item.ClosedAt,
            Participants = item.Participants.OrderBy(p => p.Id).Select(ToRecord).ToArray(),
            Messages = item.Messages.OrderBy(m => m.Seq).Select(ToRecord).ToArray()
        };
    }

    private static RescueSessionParticipantEntity ToEntity(string helpRequestId, RescueSessionParticipantRecord item)
    {
        return new RescueSessionParticipantEntity
        {
            HelpRequestId = helpRequestId,
            OpenId = item.OpenId,
            DisplayName = item.DisplayName,
            AvatarUrl = item.AvatarUrl,
            JoinedAt = item.JoinedAt,
            LastSeenAt = item.LastSeenAt,
            LastLat = item.LastLat,
            LastLng = item.LastLng,
            LocationReportedAt = item.LocationReportedAt,
            LastReadSeq = item.LastReadSeq
        };
    }

    internal static RescueSessionParticipantRecord ToRecord(RescueSessionParticipantEntity item)
    {
        return new RescueSessionParticipantRecord
        {
            OpenId = item.OpenId,
            DisplayName = item.DisplayName,
            AvatarUrl = item.AvatarUrl,
            JoinedAt = item.JoinedAt,
            LastSeenAt = item.LastSeenAt,
            LastLat = item.LastLat,
            LastLng = item.LastLng,
            LocationReportedAt = item.LocationReportedAt,
            LastReadSeq = item.LastReadSeq
        };
    }

    private static RescueSessionMessageEntity ToEntity(string helpRequestId, RescueSessionMessageRecord item)
    {
        return new RescueSessionMessageEntity
        {
            Id = item.Id,
            HelpRequestId = helpRequestId,
            Seq = item.Seq,
            Kind = item.Kind,
            SenderOpenId = item.SenderOpenId,
            SenderName = item.SenderName,
            Text = item.Text,
            ImageUrl = item.ImageUrl,
            EventType = item.EventType,
            Action = item.Action,
            CreatedAt = item.CreatedAt
        };
    }

    internal static RescueSessionMessageRecord ToRecord(RescueSessionMessageEntity item)
    {
        return new RescueSessionMessageRecord
        {
            Id = item.Id,
            Seq = item.Seq,
            Kind = item.Kind,
            SenderOpenId = item.SenderOpenId,
            SenderName = item.SenderName,
            Text = item.Text,
            ImageUrl = item.ImageUrl,
            EventType = item.EventType,
            Action = item.Action,
            CreatedAt = item.CreatedAt
        };
    }
}
