using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;

namespace AedBackend.Infrastructure.Rescue;

internal static class JsonRescueSessionOperations
{
    public static RescueSessionRecord? Find(JsonRescueSessionDatabase database, string helpRequestId) =>
        database.Sessions.FirstOrDefault(item => item.HelpRequestId == helpRequestId);

    public static RescueSessionRecord GetOrCreate(
        JsonRescueSessionDatabase database,
        string helpRequestId,
        DateTimeOffset now)
    {
        var existing = Find(database, helpRequestId);
        if (existing is not null)
        {
            EnsureOpen(existing);
            return existing;
        }

        var created = new RescueSessionRecord { HelpRequestId = helpRequestId, CreatedAt = now };
        database.Sessions.Add(created);
        return created;
    }

    public static RescueSessionJoinResult Join(
        JsonRescueSessionDatabase database,
        RescueSessionJoinCommand command)
    {
        var session = GetOrCreate(database, command.HelpRequestId, command.Now);
        if (FindParticipant(session, command.OpenId) is null)
        {
            var next = AddParticipant(session, command, out var joined);
            return new RescueSessionJoinResult(Replace(database, next), true, joined);
        }

        return new RescueSessionJoinResult(
            Replace(database, TouchParticipant(session, command)),
            false,
            null);
    }

    public static RescueSessionMessageRecord AppendMessage(
        JsonRescueSessionDatabase database,
        RescueTextMessageCommand command)
    {
        var session = RequireOpenSession(database, command.HelpRequestId);
        RequireParticipant(session, command.SenderOpenId);
        var message = CreateMessage(session, new JsonRescueMessageDraft
        {
            Kind = RescueMessageKinds.Text,
            SenderOpenId = command.SenderOpenId,
            SenderName = command.SenderName,
            Text = command.Text,
            CreatedAt = command.Now
        });
        var next = TouchParticipant(
            session with { Messages = [.. session.Messages, message] },
            command.SenderOpenId,
            command.Now);
        Replace(database, next);
        return message;
    }

    public static RescueSessionMessageRecord AppendImageMessage(
        JsonRescueSessionDatabase database,
        RescueImageMessageCommand command)
    {
        var session = RequireOpenSession(database, command.HelpRequestId);
        RequireParticipant(session, command.SenderOpenId);
        var message = CreateMessage(session, new JsonRescueMessageDraft
        {
            Kind = RescueMessageKinds.Image,
            SenderOpenId = command.SenderOpenId,
            SenderName = command.SenderName,
            ImageUrl = command.ImageUrl,
            CreatedAt = command.Now
        });
        var next = TouchParticipant(
            session with { Messages = [.. session.Messages, message] },
            command.SenderOpenId,
            command.Now);
        Replace(database, next);
        return message;
    }

    public static RescueSessionStatusAppendResult AppendStatusEvent(
        JsonRescueSessionDatabase database,
        RescueSessionStatusCommand command)
    {
        ValidateStatus(command);
        var session = RequireOpenSession(database, command.HelpRequestId);
        RequireParticipant(session, command.SenderOpenId);
        var latest = LatestStatus(session, command.SenderOpenId, command.EventType);
        var current = latest?.Action == RescueEventActions.Mark;
        var desired = command.Action == RescueEventActions.Mark;
        if (current == desired)
            return new RescueSessionStatusAppendResult(false, null, StateOf(session, command.SenderOpenId));

        var message = CreateMessage(session, new JsonRescueMessageDraft
        {
            Kind = RescueMessageKinds.StatusEvent,
            SenderOpenId = command.SenderOpenId,
            SenderName = command.SenderName,
            EventType = command.EventType,
            Action = command.Action,
            CreatedAt = command.Now
        });
        var next = TouchParticipant(
            session with { Messages = [.. session.Messages, message] },
            command.SenderOpenId,
            command.Now);
        Replace(database, next);
        return new RescueSessionStatusAppendResult(true, message, StateOf(next, command.SenderOpenId));
    }

    public static RescueSessionParticipantRecord UpdateLocation(
        JsonRescueSessionDatabase database,
        RescueSessionLocationCommand command)
    {
        var session = RequireOpenSession(database, command.HelpRequestId);
        var participant = RequireParticipant(session, command.OpenId);
        if (participant.LocationReportedAt is not null
            && command.Now < participant.LocationReportedAt)
            return participant;
        var next = session with
        {
            Participants = session.Participants.Select(participant =>
                participant.OpenId == command.OpenId
                    ? participant with
                    {
                        LastLat = command.Lat,
                        LastLng = command.Lng,
                        LocationReportedAt = command.Now,
                        LastSeenAt = command.Now
                    }
                    : participant).ToArray()
        };
        Replace(database, next);
        return RequireParticipant(next, command.OpenId);
    }

    public static long MarkRead(
        JsonRescueSessionDatabase database,
        RescueSessionReadCommand command)
    {
        var session = RequireOpenSession(database, command.HelpRequestId);
        RequireParticipant(session, command.OpenId);
        var next = session with
        {
            Participants = session.Participants.Select(participant =>
                participant.OpenId == command.OpenId
                    ? participant with
                    {
                        LastReadSeq = Math.Max(participant.LastReadSeq, command.LastReadSeq)
                    }
                    : participant).ToArray()
        };
        Replace(database, next);
        return RequireParticipant(next, command.OpenId).LastReadSeq;
    }

    public static bool Close(
        JsonRescueSessionDatabase database,
        string helpRequestId,
        DateTimeOffset closedAt)
    {
        var session = Find(database, helpRequestId);
        if (session is null)
        {
            database.Sessions.Add(new RescueSessionRecord
            {
                HelpRequestId = helpRequestId,
                CreatedAt = closedAt,
                ClosedAt = closedAt
            });
            return true;
        }
        var hasLocation = session.Participants.Any(participant =>
            participant.LastLat is not null
            || participant.LastLng is not null
            || participant.LocationReportedAt is not null);
        if (session.ClosedAt is not null && !hasLocation) return false;

        Replace(database, session with
        {
            ClosedAt = session.ClosedAt ?? closedAt,
            Participants = session.Participants.Select(participant => participant with
            {
                LastLat = null,
                LastLng = null,
                LocationReportedAt = null
            }).ToArray()
        });
        return true;
    }

    private static RescueSessionRecord AddParticipant(
        RescueSessionRecord session,
        RescueSessionJoinCommand command,
        out RescueSessionMessageRecord joined)
    {
        var participant = new RescueSessionParticipantRecord
        {
            OpenId = command.OpenId,
            DisplayName = command.DisplayName,
            AvatarUrl = command.AvatarUrl,
            JoinedAt = command.Now,
            LastSeenAt = command.Now
        };
        joined = CreateMessage(session, new JsonRescueMessageDraft
        {
            Kind = RescueMessageKinds.StatusEvent,
            SenderOpenId = command.OpenId,
            SenderName = command.DisplayName,
            EventType = RescueEventTypes.Joined,
            Action = RescueEventActions.Mark,
            CreatedAt = command.Now
        });
        return session with
        {
            Participants = [.. session.Participants, participant],
            Messages = [.. session.Messages, joined]
        };
    }

    private static RescueSessionMessageRecord CreateMessage(
        RescueSessionRecord session,
        JsonRescueMessageDraft draft) => new()
        {
            Id = Guid.NewGuid().ToString(),
            Seq = session.Messages.Count == 0 ? 1 : session.Messages.Max(item => item.Seq) + 1,
            Kind = draft.Kind,
            SenderOpenId = draft.SenderOpenId,
            SenderName = draft.SenderName,
            Text = draft.Text,
            ImageUrl = draft.ImageUrl,
            EventType = draft.EventType,
            Action = draft.Action,
            CreatedAt = draft.CreatedAt
        };

    private static RescueSessionMessageRecord? LatestStatus(
        RescueSessionRecord session,
        string senderOpenId,
        string eventType) =>
        session.Messages
            .Where(item => item.Kind == RescueMessageKinds.StatusEvent
                && item.SenderOpenId == senderOpenId
                && item.EventType == eventType)
            .MaxBy(item => item.Seq);

    private static RescueParticipantState StateOf(RescueSessionRecord session, string openId)
    {
        return new RescueParticipantState(
            IsMarked(session, openId, RescueEventTypes.TakenAed),
            IsMarked(session, openId, RescueEventTypes.Arrived));
    }

    private static bool IsMarked(
        RescueSessionRecord session,
        string openId,
        string eventType)
    {
        return LatestStatus(session, openId, eventType)?.Action == RescueEventActions.Mark;
    }

    private static void ValidateStatus(RescueSessionStatusCommand command)
    {
        var allowed = command.EventType is RescueEventTypes.TakenAed
            or RescueEventTypes.Arrived
            or RescueEventTypes.Offline
            or RescueEventTypes.Online;
        if (!allowed)
            throw new ArgumentException("不支持的急救状态事件", nameof(command));
        if (command.Action is not (RescueEventActions.Mark or RescueEventActions.Revoke))
            throw new ArgumentException("不支持的急救状态动作", nameof(command));
        if (command.EventType is RescueEventTypes.Offline or RescueEventTypes.Online
            && command.Action != RescueEventActions.Mark)
            throw new ArgumentException("离线/上线事件不支持撤销", nameof(command));
    }

    private static RescueSessionRecord TouchParticipant(
        RescueSessionRecord session,
        string openId,
        DateTimeOffset now) => session with
        {
            Participants = session.Participants.Select(participant =>
                participant.OpenId == openId ? participant with { LastSeenAt = now } : participant).ToArray()
        };

    private static RescueSessionRecord TouchParticipant(
        RescueSessionRecord session,
        RescueSessionJoinCommand command) => session with
        {
            Participants = session.Participants.Select(participant =>
                participant.OpenId == command.OpenId
                    ? participant with
                    {
                        DisplayName = string.IsNullOrWhiteSpace(command.DisplayName)
                            ? participant.DisplayName
                            : command.DisplayName,
                        AvatarUrl = string.IsNullOrWhiteSpace(command.AvatarUrl)
                            ? participant.AvatarUrl
                            : command.AvatarUrl,
                        LastSeenAt = command.Now
                    }
                    : participant).ToArray()
        };

    private static RescueSessionRecord RequireOpenSession(
        JsonRescueSessionDatabase database,
        string helpRequestId)
    {
        var session = Find(database, helpRequestId)
            ?? throw new KeyNotFoundException("急救会话不存在");
        EnsureOpen(session);
        return session;
    }

    private static void EnsureOpen(RescueSessionRecord session)
    {
        if (session.ClosedAt is not null) throw new RescueSessionClosedException(session.HelpRequestId);
    }

    private static RescueSessionParticipantRecord? FindParticipant(
        RescueSessionRecord session,
        string openId) =>
        session.Participants.FirstOrDefault(participant => participant.OpenId == openId);

    private static RescueSessionParticipantRecord RequireParticipant(
        RescueSessionRecord session,
        string openId)
    {
        return FindParticipant(session, openId)
            ?? throw new KeyNotFoundException("参与者不在急救会话中");
    }

    private static RescueSessionRecord Replace(
        JsonRescueSessionDatabase database,
        RescueSessionRecord next)
    {
        var index = database.Sessions.FindIndex(item => item.HelpRequestId == next.HelpRequestId);
        if (index < 0) throw new KeyNotFoundException("急救会话不存在");
        database.Sessions[index] = next;
        return next;
    }
}
