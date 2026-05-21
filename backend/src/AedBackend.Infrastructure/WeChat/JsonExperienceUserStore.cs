using System.Text.Json;
using AedBackend.Core.DTOs.WeChat;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.WeChat;

public sealed class JsonExperienceUserStore : IExperienceUserStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonExperienceUserStore(IOptions<WeChatOptions> options)
    {
        filePath = Path.GetFullPath(options.Value.ExperienceUsersFilePath);
    }

    public async Task<SaveLoginResult> SaveLoginAsync(
        WeChatLoginSession session,
        string tokenHash,
        CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);

        try
        {
            var database = await ReadDatabaseAsync(cancellationToken);
            var result = UpsertUser(database, session, tokenHash);
            await WriteDatabaseAsync(database, cancellationToken);
            return result;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<ExperienceUserRecord?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);

        try
        {
            var database = await ReadDatabaseAsync(cancellationToken);
            return database.Users.FirstOrDefault(user => user.SessionTokenHash == tokenHash);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task SaveSubscriptionAsync(
        string tokenHash,
        SaveSubscribeDecisionRequest request,
        CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);

        try
        {
            var database = await ReadDatabaseAsync(cancellationToken);
            SaveSubscription(database, tokenHash, request);
            await WriteDatabaseAsync(database, cancellationToken);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<ExperienceUserRecord> SavePhoneAsync(
        string tokenHash,
        string phone,
        CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);

        try
        {
            var database = await ReadDatabaseAsync(cancellationToken);
            var user = SavePhone(database, tokenHash, phone);
            await WriteDatabaseAsync(database, cancellationToken);
            return user;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<IReadOnlyList<ExperienceUserRecord>> ListUsersAsync(
        CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);

        try
        {
            var database = await ReadDatabaseAsync(cancellationToken);
            return database.Users.ToArray();
        }
        finally
        {
            fileLock.Release();
        }
    }

    private static SaveLoginResult UpsertUser(
        ExperienceUserDatabase database,
        WeChatLoginSession session,
        string tokenHash)
    {
        var existingIndex = database.Users.FindIndex(user => user.OpenId == session.OpenId);
        var now = DateTimeOffset.UtcNow;

        if (existingIndex < 0)
        {
            var user = CreateUser(session, tokenHash, now);
            database.Users.Add(user);
            return new SaveLoginResult(user, true);
        }

        var updated = database.Users[existingIndex] with
        {
            UnionId = session.UnionId,
            SessionTokenHash = tokenHash,
            LastLoginAt = now
        };
        database.Users[existingIndex] = updated;
        return new SaveLoginResult(updated, false);
    }

    private static ExperienceUserRecord CreateUser(
        WeChatLoginSession session,
        string tokenHash,
        DateTimeOffset now)
    {
        return new ExperienceUserRecord(
            session.OpenId,
            session.UnionId,
            null,
            tokenHash,
            now,
            now,
            new Dictionary<string, SubscriptionRecord>());
    }

    private static void SaveSubscription(
        ExperienceUserDatabase database,
        string tokenHash,
        SaveSubscribeDecisionRequest request)
    {
        var index = database.Users.FindIndex(user => user.SessionTokenHash == tokenHash);
        if (index < 0)
        {
            throw new InvalidOperationException("体验用户登录态不存在");
        }

        var subscriptions = database.Users[index].Subscriptions.ToDictionary();
        subscriptions[request.TemplateId] = new SubscriptionRecord(
            request.TemplateId,
            request.Status,
            DateTimeOffset.UtcNow);

        database.Users[index] = database.Users[index] with { Subscriptions = subscriptions };
    }

    private static ExperienceUserRecord SavePhone(
        ExperienceUserDatabase database,
        string tokenHash,
        string phone)
    {
        var index = database.Users.FindIndex(user => user.SessionTokenHash == tokenHash);
        if (index < 0)
        {
            throw new InvalidOperationException("体验用户登录态不存在");
        }

        var updated = database.Users[index] with { Phone = phone };
        database.Users[index] = updated;
        return updated;
    }

    private async Task<ExperienceUserDatabase> ReadDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new ExperienceUserDatabase();
        }

        await using var stream = File.OpenRead(filePath);
        var database = await JsonSerializer.DeserializeAsync<ExperienceUserDatabase>(
            stream,
            JsonOptions,
            cancellationToken);

        return database ?? new ExperienceUserDatabase();
    }

    private async Task WriteDatabaseAsync(
        ExperienceUserDatabase database,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, database, JsonOptions, cancellationToken);
    }

    private sealed class ExperienceUserDatabase
    {
        public List<ExperienceUserRecord> Users { get; init; } = [];
    }
}
