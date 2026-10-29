using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;

namespace AedBackend.Tests.Rescue;

public sealed class RescuePresencePlannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Plan_StaleParticipant_EmitsOfflineOnce()
    {
        var session = Session(
            participant: Participant(
                locationReportedAt: Now.AddSeconds(-200)),
            messages: []);

        var commands = RescuePresencePlanner.Plan(session, Now, 180);

        var command = Assert.Single(commands);
        Assert.Equal(RescueEventTypes.Offline, command.EventType);
        Assert.Equal("openid-a", command.OpenId);
        Assert.Equal("急救员甲", command.DisplayName);
    }

    [Fact]
    public void Plan_AlreadyOffline_DoesNotRepeat()
    {
        var session = Session(
            participant: Participant(
                locationReportedAt: Now.AddSeconds(-200)),
            messages: [PresenceMessage(1, RescueEventTypes.Offline)]);

        Assert.Empty(RescuePresencePlanner.Plan(session, Now, 180));
    }

    [Fact]
    public void Plan_FreshAfterOffline_EmitsOnline()
    {
        var session = Session(
            participant: Participant(
                locationReportedAt: Now.AddSeconds(-30)),
            messages: [PresenceMessage(1, RescueEventTypes.Offline)]);

        var command = Assert.Single(RescuePresencePlanner.Plan(session, Now, 180));
        Assert.Equal(RescueEventTypes.Online, command.EventType);
    }

    [Fact]
    public void Plan_FreshWithoutOffline_EmitsNothing()
    {
        var session = Session(
            participant: Participant(
                locationReportedAt: Now.AddSeconds(-30)),
            messages: []);

        Assert.Empty(RescuePresencePlanner.Plan(session, Now, 180));
    }

    private static RescueSessionRecord Session(
        RescueSessionParticipantRecord participant,
        IReadOnlyList<RescueSessionMessageRecord> messages) => new()
        {
            HelpRequestId = "help-1",
            CreatedAt = Now.AddMinutes(-30),
            Participants = [participant],
            Messages = messages
        };

    private static RescueSessionParticipantRecord Participant(
        DateTimeOffset? locationReportedAt) => new()
        {
            OpenId = "openid-a",
            DisplayName = "急救员甲",
            JoinedAt = Now.AddMinutes(-30),
            LocationReportedAt = locationReportedAt
        };

    private static RescueSessionMessageRecord PresenceMessage(
        long seq,
        string eventType) => new()
        {
            Id = $"message-{seq}",
            Seq = seq,
            Kind = RescueMessageKinds.StatusEvent,
            SenderOpenId = "openid-a",
            SenderName = "急救员甲",
            EventType = eventType,
            Action = RescueEventActions.Mark,
            CreatedAt = Now.AddSeconds(-200)
        };
}
