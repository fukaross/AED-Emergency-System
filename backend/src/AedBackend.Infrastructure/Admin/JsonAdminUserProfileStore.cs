using System.Text.Json;
using System.Text.Json.Serialization;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Services.Admin;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Admin;

public sealed class JsonAdminUserProfileStore : IAdminUserProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonAdminUserProfileStore(IOptions<AdminOptions> options)
    {
        filePath = Path.GetFullPath(options.Value.AdminUserProfilesFilePath);
    }

    public async Task<AdminUserProfileRecord?> FindAsync(
        string openId,
        CancellationToken cancellationToken)
    {
        return await WithReadAsync(
            database => database.Profiles.TryGetValue(openId, out var profile) ? profile : null,
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, AdminUserProfileRecord>> ListAsync(
        CancellationToken cancellationToken)
    {
        return await WithReadAsync(
            database => new Dictionary<string, AdminUserProfileRecord>(database.Profiles),
            cancellationToken);
    }

    public async Task<AdminUserProfileRecord> UpsertAsync(
        string openId,
        Func<AdminUserProfileRecord?, AdminUserProfileRecord> update,
        CancellationToken cancellationToken)
    {
        return await WithUpdateAsync(
            database =>
            {
                database.Profiles.TryGetValue(openId, out var current);
                var next = update(current);
                database.Profiles[openId] = next;
                return next;
            },
            cancellationToken);
    }

    private async Task<T> WithReadAsync<T>(
        Func<AdminUserProfileDatabase, T> read,
        CancellationToken cancellationToken)
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

    private async Task<T> WithUpdateAsync<T>(
        Func<AdminUserProfileDatabase, T> update,
        CancellationToken cancellationToken)
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

    private async Task<AdminUserProfileDatabase> ReadDatabaseAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new AdminUserProfileDatabase();
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<AdminUserProfileDatabase>(
            stream,
            JsonOptions,
            cancellationToken) ?? new AdminUserProfileDatabase();
    }

    private async Task WriteDatabaseAsync(
        AdminUserProfileDatabase database,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{filePath}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, database, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, filePath, true);
    }

    private sealed class AdminUserProfileDatabase
    {
        public Dictionary<string, AdminUserProfileRecord> Profiles { get; init; } = [];
    }
}
