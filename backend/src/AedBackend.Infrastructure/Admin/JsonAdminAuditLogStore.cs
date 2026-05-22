using System.Text.Json;
using System.Text.Json.Serialization;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Services.Admin;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Admin;

public sealed class JsonAdminAuditLogStore : IAdminAuditLogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonAdminAuditLogStore(IOptions<AdminOptions> options)
    {
        filePath = Path.GetFullPath(options.Value.AdminAuditLogsFilePath);
    }

    public async Task AppendAsync(
        AdminAuditLogRecord record,
        CancellationToken cancellationToken)
    {
        await WithUpdateAsync(
            database =>
            {
                database.Logs.Insert(0, record);
                return true;
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<AdminAuditLogRecord>> ListAsync(
        CancellationToken cancellationToken)
    {
        return await WithReadAsync(
            database => database.Logs.OrderByDescending(log => log.CreatedAt).ToArray(),
            cancellationToken);
    }

    private async Task<T> WithReadAsync<T>(
        Func<AdminAuditLogDatabase, T> read,
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
        Func<AdminAuditLogDatabase, T> update,
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

    private async Task<AdminAuditLogDatabase> ReadDatabaseAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new AdminAuditLogDatabase();
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<AdminAuditLogDatabase>(
            stream,
            JsonOptions,
            cancellationToken) ?? new AdminAuditLogDatabase();
    }

    private async Task WriteDatabaseAsync(
        AdminAuditLogDatabase database,
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

    private sealed class AdminAuditLogDatabase
    {
        public List<AdminAuditLogRecord> Logs { get; init; } = [];
    }
}
