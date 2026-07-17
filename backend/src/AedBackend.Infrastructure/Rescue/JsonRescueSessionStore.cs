using System.Text.Json;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Rescue;

// [Transitional: JSON -> MySQL] Keep schema close to BackEnd.md tables.
public sealed class JsonRescueSessionStore : IRescueSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonRescueSessionStore(IOptions<WeChatOptions> options)
    {
        filePath = Path.GetFullPath(options.Value.RescueSessionsFilePath);
    }

    public async Task<RescueSessionRecord?> FindByHelpIdAsync(string helpRequestId, CancellationToken cancellationToken)
    {
        return await WithReadAsync(database => FindSession(database, helpRequestId), cancellationToken);
    }

    public async Task<RescueSessionRecord> GetOrCreateAsync(
        string helpRequestId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await WithUpdateAsync(database => GetOrCreateSession(database, helpRequestId, now), cancellationToken);
    }

    public async Task<RescueSessionRecord> JoinAsync(RescueSessionJoinCommand command, CancellationToken cancellationToken)
    {
        return await WithUpdateAsync(database => JoinSession(database, command), cancellationToken);
    }

    public async Task<RescueSessionRecord> AppendMessageAsync(RescueSessionAppendCommand command, CancellationToken cancellationToken)
    {
        return await WithUpdateAsync(database => AppendMessage(database, command), cancellationToken);
    }

    public async Task<RescueSessionRecord> UpdateLocationAsync(RescueSessionLocationCommand command, CancellationToken cancellationToken)
    {
        return await WithUpdateAsync(database => UpdateLocation(database, command), cancellationToken);
    }

    public async Task<RescueSessionRecord> MarkReadAsync(
        string helpRequestId, string openId, long lastReadSeq, CancellationToken cancellationToken)
    {
        return await WithUpdateAsync(database =>
        {
            var session = RequireSession(database, helpRequestId);
            RequireParticipant(session, openId);
            return ReplaceSession(database, session with
            {
                Participants = session.Participants.Select(p =>
                    p.OpenId == openId ? p with { LastReadSeq = lastReadSeq } : p).ToArray()
            });
        }, cancellationToken);
    }

    public async Task CloseSessionAsync(string helpRequestId, CancellationToken cancellationToken)
    {
        await WithUpdateAsync(database =>
        {
            var session = FindSession(database, helpRequestId);
            // 关闭是幂等收尾：会话不存在即无位置可清，静默返回属于合理行为。
            if (session is null) return true;
            ReplaceSession(database, session with
            {
                Participants = session.Participants.Select(p => p with
                { LastLat = null, LastLng = null, LocationReportedAt = null }).ToArray()
            });
            return true;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<RescueSessionRecord>> ListByParticipantAsync(
        string openId,
        CancellationToken cancellationToken)
    {
        return await WithReadAsync(
            database => database.Sessions
                .Where(item => item.Participants.Any(p => p.OpenId == openId))
                .OrderByDescending(item => item.CreatedAt)
                .ToArray(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<RescueSessionRecord>> ListAllAsync(CancellationToken cancellationToken)
    {
        return await WithReadAsync(
            database => database.Sessions.OrderByDescending(item => item.CreatedAt).ToArray(),
            cancellationToken);
    }

    private static RescueSessionRecord JoinSession(RescueSessionDatabase database, RescueSessionJoinCommand command)
    {
        var session = GetOrCreateSession(database, command.HelpRequestId, command.Now);
        var next = FindParticipant(session, command.OpenId) is null
            ? AddParticipant(session, command)
            : TouchParticipant(session, command.OpenId, command.Now);
        return ReplaceSession(database, next);
    }

    private static RescueSessionRecord AppendMessage(RescueSessionDatabase database, RescueSessionAppendCommand command)
    {
        var session = RequireSession(database, command.HelpRequestId);
        RequireParticipant(session, command.SenderOpenId);
        var message = new RescueSessionMessageRecord
        {
            Id = NewMessageId(),
            Seq = NextSeq(session),
            Kind = command.Kind,
            SenderOpenId = command.SenderOpenId,
            SenderName = command.SenderName,
            Text = command.Text,
            EventType = command.EventType,
            Action = command.Action,
            CreatedAt = command.Now
        };
        var next = TouchParticipant(
            session with { Messages = [.. session.Messages, message] },
            command.SenderOpenId,
            command.Now);
        return ReplaceSession(database, next);
    }

    private static RescueSessionRecord UpdateLocation(RescueSessionDatabase database, RescueSessionLocationCommand command)
    {
        var session = RequireSession(database, command.HelpRequestId);
        RequireParticipant(session, command.OpenId);
        var next = session with
        {
            Participants = session.Participants.Select(p =>
                p.OpenId == command.OpenId
                    ? p with
                    {
                        LastLat = command.Lat,
                        LastLng = command.Lng,
                        LocationReportedAt = command.Now,
                        LastSeenAt = command.Now
                    }
                    : p).ToArray()
        };
        return ReplaceSession(database, next);
    }

    private static RescueSessionRecord AddParticipant(RescueSessionRecord session, RescueSessionJoinCommand command)
    {
        var participant = new RescueSessionParticipantRecord
        {
            OpenId = command.OpenId,
            DisplayName = command.DisplayName,
            JoinedAt = command.Now,
            LastSeenAt = command.Now
        };
        var joined = new RescueSessionMessageRecord
        {
            Id = NewMessageId(),
            Seq = NextSeq(session),
            Kind = RescueMessageKinds.StatusEvent,
            SenderOpenId = command.OpenId,
            SenderName = command.DisplayName,
            EventType = RescueEventTypes.Joined,
            Action = RescueEventActions.Mark,
            CreatedAt = command.Now
        };
        return session with
        {
            Participants = [.. session.Participants, participant],
            Messages = [.. session.Messages, joined]
        };
    }

    private static RescueSessionRecord TouchParticipant(RescueSessionRecord session, string openId, DateTimeOffset now)
    {
        return session with
        {
            Participants = session.Participants.Select(p =>
                p.OpenId == openId ? p with { LastSeenAt = now } : p).ToArray()
        };
    }

    private static RescueSessionRecord? FindSession(RescueSessionDatabase database, string helpRequestId)
    {
        return database.Sessions.FirstOrDefault(item => item.HelpRequestId == helpRequestId);
    }

    private static RescueSessionRecord RequireSession(RescueSessionDatabase database, string helpRequestId)
    {
        return FindSession(database, helpRequestId)
            ?? throw new KeyNotFoundException("急救会话不存在");
    }

    private static RescueSessionRecord GetOrCreateSession(
        RescueSessionDatabase database,
        string helpRequestId,
        DateTimeOffset now)
    {
        var existing = FindSession(database, helpRequestId);
        if (existing is not null) return existing;
        var created = new RescueSessionRecord { HelpRequestId = helpRequestId, CreatedAt = now };
        database.Sessions.Add(created);
        return created;
    }

    private static RescueSessionParticipantRecord? FindParticipant(RescueSessionRecord session, string openId)
    {
        return session.Participants.FirstOrDefault(p => p.OpenId == openId);
    }

    private static RescueSessionParticipantRecord RequireParticipant(RescueSessionRecord session, string openId)
    {
        return FindParticipant(session, openId)
            ?? throw new KeyNotFoundException("参与者不在急救会话中");
    }

    private static RescueSessionRecord ReplaceSession(RescueSessionDatabase database, RescueSessionRecord next)
    {
        var index = database.Sessions.FindIndex(item => item.HelpRequestId == next.HelpRequestId);
        if (index < 0) throw new KeyNotFoundException("急救会话不存在");
        database.Sessions[index] = next;
        return next;
    }

    private static long NextSeq(RescueSessionRecord session) =>
        session.Messages.Count == 0 ? 1 : session.Messages.Max(m => m.Seq) + 1;

    private static string NewMessageId() => Guid.NewGuid().ToString();

    private async Task<T> WithReadAsync<T>(Func<RescueSessionDatabase, T> read, CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            return read(await ReadDatabaseAsync(cancellationToken));
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<T> WithUpdateAsync<T>(Func<RescueSessionDatabase, T> update, CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            var database = await ReadDatabaseAsync(cancellationToken);
            var result = update(database);
            await WriteDatabaseAsync(database, cancellationToken);
            return result;
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<RescueSessionDatabase> ReadDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath)) return new RescueSessionDatabase();
        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<RescueSessionDatabase>(
            stream, JsonOptions, cancellationToken) ?? new RescueSessionDatabase();
    }

    private async Task WriteDatabaseAsync(RescueSessionDatabase database, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, database, JsonOptions, cancellationToken);
    }

    private sealed class RescueSessionDatabase
    {
        public List<RescueSessionRecord> Sessions { get; init; } = [];
    }
}
