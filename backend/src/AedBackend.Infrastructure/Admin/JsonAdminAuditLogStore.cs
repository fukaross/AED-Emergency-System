using AedBackend.Core.Models.Admin;
using AedBackend.Core.Services.Admin;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Admin;

public sealed class JsonAdminAuditLogStore : IAdminAuditLogStore
{
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
                if (database.Logs.Any(item => item.Id == record.Id)) return false;
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
            await JsonAdminAuditLogPersistence.WriteAtomicallyAsync(filePath, database);
            return result;
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<AdminAuditLogDatabase> ReadDatabaseAsync(
        CancellationToken cancellationToken) =>
        await JsonAdminAuditLogPersistence.ReadAsync(filePath, cancellationToken);
}
