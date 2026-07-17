using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;

namespace AedBackend.Tests.Rescue;

public sealed class RescueSessionProjectionTests
{
    private const int LostAfterSeconds = 45;
    private const int GoneAfterSeconds = 90;
    private static readonly DateTimeOffset BaseTime = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FoldParticipantStates_LatestActionWins()
    {
        var session = SessionWith(
            [Participant("a")],
            [
                Event(1, "a", RescueEventTypes.TakenAed, RescueEventActions.Mark),
                Event(2, "a", RescueEventTypes.TakenAed, RescueEventActions.Revoke),
                Event(3, "a", RescueEventTypes.Arrived, RescueEventActions.Revoke),
                Event(4, "a", RescueEventTypes.Arrived, RescueEventActions.Mark),
                Text(5, "a")
            ]);

        var states = RescueSessionProjection.FoldParticipantStates(session);

        Assert.False(states["a"].TakenAed);
        Assert.True(states["a"].Arrived);
    }

    [Fact]
    public void FoldParticipantStates_TracksParticipantsIndependently()
    {
        var session = SessionWith(
            [Participant("a"), Participant("b")],
            [Event(1, "a", RescueEventTypes.TakenAed, RescueEventActions.Mark)]);

        var states = RescueSessionProjection.FoldParticipantStates(session);

        Assert.True(states["a"].TakenAed);
        Assert.False(states["b"].TakenAed);
        Assert.False(states["b"].Arrived);
    }

    [Fact]
    public void FoldParticipantStates_WithoutStatusEvents_AllFalse()
    {
        var session = SessionWith(
            [Participant("a")],
            [Event(1, "a", RescueEventTypes.Joined, RescueEventActions.Mark)]);

        var states = RescueSessionProjection.FoldParticipantStates(session);

        Assert.False(states["a"].TakenAed);
        Assert.False(states["a"].Arrived);
    }

    [Theory]
    [InlineData(0, RescueSignalStates.Ok)]
    [InlineData(45, RescueSignalStates.Ok)]
    [InlineData(46, RescueSignalStates.Lost)]
    [InlineData(90, RescueSignalStates.Lost)]
    [InlineData(91, RescueSignalStates.Gone)]
    public void SignalOf_ClassifiesByThreshold(int elapsedSeconds, string expected)
    {
        var signal = RescueSessionProjection.SignalOf(
            BaseTime.AddSeconds(-elapsedSeconds),
            BaseTime,
            LostAfterSeconds,
            GoneAfterSeconds);

        Assert.Equal(expected, signal);
    }

    [Fact]
    public void SignalOf_WithoutLocationReport_IsGone()
    {
        var signal = RescueSessionProjection.SignalOf(
            null,
            BaseTime,
            LostAfterSeconds,
            GoneAfterSeconds);

        Assert.Equal(RescueSignalStates.Gone, signal);
    }

    [Fact]
    public void UnreadCount_ExcludesOwnAndAlreadyReadMessages()
    {
        var session = SessionWith(
            [Participant("a"), Participant("b")],
            [Text(1, "a"), Text(2, "b"), Text(3, "a"), Text(4, "b")]);

        Assert.Equal(2, RescueSessionProjection.UnreadCount(session, "a", 0));
        Assert.Equal(1, RescueSessionProjection.UnreadCount(session, "a", 2));
        Assert.Equal(0, RescueSessionProjection.UnreadCount(session, "a", 4));
    }

    private static RescueSessionRecord SessionWith(
        IReadOnlyList<RescueSessionParticipantRecord> participants,
        IReadOnlyList<RescueSessionMessageRecord> messages)
    {
        return new RescueSessionRecord
        {
            HelpRequestId = "help-1",
            CreatedAt = BaseTime,
            Participants = participants,
            Messages = messages
        };
    }

    private static RescueSessionParticipantRecord Participant(string openId)
    {
        return new RescueSessionParticipantRecord
        {
            OpenId = openId,
            DisplayName = openId,
            JoinedAt = BaseTime,
            LastSeenAt = BaseTime
        };
    }

    private static RescueSessionMessageRecord Event(long seq, string sender, string eventType, string action)
    {
        return new RescueSessionMessageRecord
        {
            Id = $"m{seq}",
            Seq = seq,
            Kind = RescueMessageKinds.StatusEvent,
            SenderOpenId = sender,
            SenderName = sender,
            EventType = eventType,
            Action = action,
            CreatedAt = BaseTime
        };
    }

    private static RescueSessionMessageRecord Text(long seq, string sender)
    {
        return new RescueSessionMessageRecord
        {
            Id = $"m{seq}",
            Seq = seq,
            Kind = RescueMessageKinds.Text,
            SenderOpenId = sender,
            SenderName = sender,
            Text = "hi",
            CreatedAt = BaseTime
        };
    }
}
