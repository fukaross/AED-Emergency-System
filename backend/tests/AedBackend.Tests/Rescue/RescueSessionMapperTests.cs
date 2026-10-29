using AedBackend.Api.Endpoints;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Infrastructure.Rescue;

namespace AedBackend.Tests.Rescue;

public sealed class RescueSessionMapperTests
{
    [Fact]
    public void RescueView_PreservesParticipantIdsWhenEarlierTimestampJoinsLater()
    {
        var now = DateTimeOffset.Parse("2026-07-18T00:00:00+00:00");
        var first = Session(
            [Participant("open-b", now)],
            [Joined("open-b", "B", 1, now)]);
        var firstView = MapView(first, Options(), now);
        Assert.Equal("p-1", Assert.Single(firstView.Participants).ParticipantId);

        var later = Session(
            [Participant("open-a", now.AddMinutes(-1)), Participant("open-b", now)],
            [Joined("open-b", "B", 1, now), Joined("open-a", "A", 2, now.AddSeconds(1))]);
        var laterView = MapView(later, Options(), now);

        Assert.Equal("p-1", laterView.Participants.Single(item => item.DisplayName == "open-b").ParticipantId);
        Assert.Equal("p-2", laterView.Participants.Single(item => item.DisplayName == "open-a").ParticipantId);
    }

    [Fact]
    public void SnapshotAndRescueView_ExposeConfiguredTiming()
    {
        var now = DateTimeOffset.UtcNow;
        var session = Session(
            [Participant("open-a", now)],
            [Joined("open-a", "A", 1, now)]);
        var options = Options();

        var snapshot = MapSnapshot(session, options, now);
        var view = MapView(session, options, now);

        Assert.Equal(30, snapshot.Timing.SignalLostAfterSeconds);
        Assert.Equal(60, snapshot.Timing.LocationGoneAfterSeconds);
        Assert.Equal(4, snapshot.Timing.LocationReportIntervalSeconds);
        Assert.Equal(snapshot.Timing, view.Timing);
    }

    private static RescueSessionSnapshotDto MapSnapshot(
        RescueSessionRecord session,
        RescueOptions options,
        DateTimeOffset now) =>
        RescueSessionMapper.ToSnapshot(new RescueSnapshotMappingInput
        {
            Session = session,
            Help = Help(),
            CurrentOpenId = "open-a",
            Context = new RescueMappingContext(options, now),
        });

    private static RescueViewDto MapView(
        RescueSessionRecord session,
        RescueOptions options,
        DateTimeOffset now) =>
        RescueSessionMapper.ToRescueView(new RescueViewMappingInput
        {
            Help = Help(),
            Session = session,
            Context = new RescueMappingContext(options, now),
        });

    private static RescueOptions Options() => new()
    {
        SignalLostAfterSeconds = 30,
        LocationGoneAfterSeconds = 60,
        LocationReportIntervalSeconds = 4
    };

    private static HelpRequestRecord Help() => new()
    {
        Id = "help-1",
        RequestNo = "HELP1",
        Status = HelpStatuses.Responding,
        Address = "教学楼"
    };

    private static RescueSessionRecord Session(
        IReadOnlyList<RescueSessionParticipantRecord> participants,
        IReadOnlyList<RescueSessionMessageRecord> messages) => new()
        {
            HelpRequestId = "help-1",
            Participants = participants,
            Messages = messages
        };

    private static RescueSessionParticipantRecord Participant(
        string openId,
        DateTimeOffset joinedAt) => new()
        {
            OpenId = openId,
            DisplayName = openId,
            JoinedAt = joinedAt,
            LastSeenAt = joinedAt
        };

    private static RescueSessionMessageRecord Joined(
        string openId,
        string name,
        long seq,
        DateTimeOffset at) => new()
        {
            Id = $"message-{seq}",
            Seq = seq,
            Kind = RescueMessageKinds.StatusEvent,
            SenderOpenId = openId,
            SenderName = name,
            EventType = RescueEventTypes.Joined,
            Action = RescueEventActions.Mark,
            CreatedAt = at
        };
}
