using System.Text.Json;
using System.Text.Json.Serialization;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Services.Admin;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Admin;

public sealed class JsonAdminUserStore : IAdminUserStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonAdminUserStore(IOptions<AdminOptions> options)
    {
        filePath = Path.GetFullPath(options.Value.AdminUsersFilePath);
    }

    public async Task<AdminUserRecord?> FindByNormalizedUsernameAsync(
        string normalizedUsername,
        CancellationToken cancellationToken)
    {
        return await WithReadAsync(
            database => database.Users.FirstOrDefault(user =>
                user.NormalizedUsername == normalizedUsername),
            cancellationToken);
    }

    public async Task<AdminUserRecord?> FindByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        return await WithReadAsync(
            database => database.Users.FirstOrDefault(user => user.Id == id),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AdminUserRecord>> ListAsync(
        CancellationToken cancellationToken)
    {
        return await WithReadAsync(
            database => database.Users.OrderBy(user => user.CreatedAt).ToArray(),
            cancellationToken);
    }

    public async Task<bool> HasSuperAdminAsync(CancellationToken cancellationToken)
    {
        return await WithReadAsync(
            database => database.Users.Any(user => user.Role == AdminRoles.SuperAdmin),
            cancellationToken);
    }

    public async Task<AdminUserRecord> AddAsync(
        AdminUserRecord user,
        CancellationToken cancellationToken)
    {
        return await WithUpdateAsync(
            database =>
            {
                if (database.Users.Any(item => item.NormalizedUsername == user.NormalizedUsername))
                {
                    throw new InvalidOperationException("管理员用户名已存在");
                }

                database.Users.Add(user);
                return user;
            },
            cancellationToken);
    }

    public async Task<AdminUserRecord> UpdateAsync(
        string id,
        Func<AdminUserRecord, AdminUserRecord> update,
        CancellationToken cancellationToken)
    {
        return await WithUpdateAsync(
            database =>
            {
                var index = database.Users.FindIndex(user => user.Id == id);
                if (index < 0) throw new KeyNotFoundException("管理员不存在");

                var next = update(database.Users[index]);
                database.Users[index] = next;
                return next;
            },
            cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await WithUpdateAsync(
            database =>
            {
                var removed = database.Users.RemoveAll(user => user.Id == id);
                if (removed == 0) throw new KeyNotFoundException("管理员不存在");
                return true;
            },
            cancellationToken);
    }

    private async Task<T> WithReadAsync<T>(
        Func<AdminUserDatabase, T> read,
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
        Func<AdminUserDatabase, T> update,
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

    private async Task<AdminUserDatabase> ReadDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new AdminUserDatabase();
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<AdminUserDatabase>(
            stream,
            JsonOptions,
            cancellationToken) ?? new AdminUserDatabase();
    }

    private async Task WriteDatabaseAsync(
        AdminUserDatabase database,
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

    private sealed class AdminUserDatabase
    {
        public List<AdminUserRecord> Users { get; init; } = [];
    }
}
