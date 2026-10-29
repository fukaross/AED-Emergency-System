using System.Text.Json;
using System.Text.Json.Serialization;
using AedBackend.Core.Models.Admin;

namespace AedBackend.Infrastructure.Admin;

internal static class JsonAdminAuditLogPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<AdminAuditLogDatabase> ReadAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath)) return new AdminAuditLogDatabase();

        try
        {
            await using var stream = File.OpenRead(fullPath);
            var envelope = await JsonSerializer.DeserializeAsync<AdminAuditLogEnvelope>(
                stream,
                JsonOptions,
                cancellationToken);
            if (envelope?.Logs is null || envelope.Logs.Any(item => item is null))
                throw InvalidDocument(fullPath);
            return new AdminAuditLogDatabase
            {
                Logs = envelope.Logs.Select(item => item!).ToList()
            };
        }
        catch (JsonException ex)
        {
            throw InvalidDocument(fullPath, ex);
        }
        catch (NotSupportedException ex)
        {
            throw InvalidDocument(fullPath, ex);
        }
    }

    public static Task WriteAtomicallyAsync(
        string filePath,
        AdminAuditLogDatabase database) =>
        WriteAtomicallyAsync(filePath, database, static _ => Task.CompletedTask);

    /** Provides a deterministic boundary for testing failures before the atomic replace. */
    internal static async Task WriteAtomicallyAsync(
        string filePath,
        AdminAuditLogDatabase database,
        Func<string, Task> beforeReplace)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await WriteTempFileAsync(tempPath, database);
            await beforeReplace(tempPath);
            File.Move(tempPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static async Task WriteTempFileAsync(
        string tempPath,
        AdminAuditLogDatabase database)
    {
        await using var stream = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await JsonSerializer.SerializeAsync(stream, database, JsonOptions, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);
    }

    private static InvalidDataException InvalidDocument(string fullPath, Exception? inner = null)
    {
        var message = $"Invalid admin audit log JSON at '{fullPath}': "
            + "the root must contain a non-null 'logs' array of objects.";
        return inner is null
            ? new InvalidDataException(message)
            : new InvalidDataException(message, inner);
    }

    private sealed class AdminAuditLogEnvelope
    {
        public List<AdminAuditLogRecord?>? Logs { get; init; }
    }
}

internal sealed class AdminAuditLogDatabase
{
    public List<AdminAuditLogRecord> Logs { get; init; } = [];
}
