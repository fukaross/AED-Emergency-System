using AedBackend.Core.Models.Rescue;

namespace AedBackend.Tests.Persistence;

internal sealed partial class ProviderTempData
{
    public Task WriteRescueSessionJsonAsync() =>
        WriteAsync("rescue-sessions.json", new RescueSessionsDb { Sessions = [SampleRescueSession()] });

    private static RescueSessionRecord SampleRescueSession()
    {
        var createdAt = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        return new RescueSessionRecord
        {
            HelpRequestId = "help-01",
            CreatedAt = createdAt,
            Participants = [SampleParticipant(createdAt)],
            Messages = [JoinedMessage(createdAt), TextMessage(createdAt)]
        };
    }

    private static RescueSessionParticipantRecord SampleParticipant(DateTimeOffset createdAt) => new()
    {
        OpenId = "openid-2",
        DisplayName = "急救员甲",
        JoinedAt = createdAt.AddSeconds(1),
        LastSeenAt = createdAt.AddSeconds(2),
        LastLat = 20.123456,
        LastLng = 110.654321,
        LocationReportedAt = createdAt.AddSeconds(2),
        LastReadSeq = 1
    };

    private static RescueSessionMessageRecord JoinedMessage(DateTimeOffset createdAt) => new()
    {
        Id = "rescue-message-1",
        Seq = 1,
        Kind = RescueMessageKinds.StatusEvent,
        SenderOpenId = "openid-2",
        SenderName = "急救员甲",
        EventType = RescueEventTypes.Joined,
        Action = RescueEventActions.Mark,
        CreatedAt = createdAt.AddSeconds(1)
    };

    private static RescueSessionMessageRecord TextMessage(DateTimeOffset createdAt) => new()
    {
        Id = "rescue-message-2",
        Seq = 2,
        Kind = RescueMessageKinds.Text,
        SenderOpenId = "openid-2",
        SenderName = "急救员甲",
        Text = "已出发",
        CreatedAt = createdAt.AddSeconds(3)
    };

    private sealed class RescueSessionsDb
    {
        public List<RescueSessionRecord> Sessions { get; init; } = [];
    }
}
