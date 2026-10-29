using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal static class SqliteRescueSessionWriteSupport
{
    public static void ValidateStatus(RescueSessionStatusCommand command)
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

    public static void EnsureOpen(RescueSessionEntity session)
    {
        if (session.ClosedAt is not null) throw new RescueSessionClosedException(session.HelpRequestId);
    }

    public static RescueSessionMessageEntity CreateStatusMessage(
        RescueSessionEntity session,
        RescueSessionStatusCommand command,
        long seq) => new()
        {
            Id = Guid.NewGuid().ToString(),
            HelpRequestId = session.HelpRequestId,
            Seq = seq,
            Kind = RescueMessageKinds.StatusEvent,
            SenderOpenId = command.SenderOpenId,
            SenderName = command.SenderName,
            EventType = command.EventType,
            Action = command.Action,
            CreatedAt = command.Now
        };
}
