using System.Text.Json;
using AedBackend.Core.Models.Rescue;

namespace AedBackend.Infrastructure.Rescue;

internal static class JsonRescueSessionPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<JsonRescueSessionDatabase> ReadAsync(string filePath, CancellationToken ct)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath)) return new JsonRescueSessionDatabase();
        try
        {
            await using var stream = File.OpenRead(fullPath);
            var envelope = await JsonSerializer.DeserializeAsync<JsonRescueSessionEnvelope>(stream, JsonOptions, ct);
            if (envelope?.Sessions is null || envelope.Sessions.Any(item => item is null))
                throw InvalidDocument(fullPath);
            return new JsonRescueSessionDatabase
            {
                Sessions = envelope.Sessions.Select(item => item!).ToList()
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
        JsonRescueSessionDatabase database) =>
        WriteAtomicallyAsync(filePath, database, static _ => Task.CompletedTask);

    /** Provides a deterministic boundary for testing failures before the atomic replace. */
    internal static async Task WriteAtomicallyAsync(
        string filePath,
        JsonRescueSessionDatabase database,
        Func<string, Task> beforeReplace)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, database, JsonOptions, CancellationToken.None);
                await stream.FlushAsync(CancellationToken.None);
            }
            await beforeReplace(tempPath);
            File.Move(tempPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static InvalidDataException InvalidDocument(string fullPath, Exception? inner = null)
    {
        var message = $"Invalid rescue session JSON at '{fullPath}': "
            + "the root must contain a non-null 'sessions' array of objects.";
        return inner is null
            ? new InvalidDataException(message)
            : new InvalidDataException(message, inner);
    }

    private sealed class JsonRescueSessionEnvelope
    {
        public List<RescueSessionRecord?>? Sessions { get; init; }
    }
}

internal sealed class JsonRescueSessionDatabase
{
    public List<RescueSessionRecord> Sessions { get; init; } = [];
}
