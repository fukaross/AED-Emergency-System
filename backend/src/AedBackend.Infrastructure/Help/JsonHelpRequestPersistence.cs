using System.Text.Json;
using AedBackend.Core.Models.Help;

namespace AedBackend.Infrastructure.Help;

internal static class JsonHelpRequestPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<HelpRequestDatabase> ReadAsync(string filePath, CancellationToken ct)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath)) return new HelpRequestDatabase();
        try
        {
            await using var stream = File.OpenRead(fullPath);
            var envelope = await JsonSerializer.DeserializeAsync<HelpRequestEnvelope>(
                stream,
                JsonOptions,
                ct);
            if (envelope?.Requests is null || envelope.Requests.Any(item => item is null))
                throw InvalidDocument(fullPath);
            return new HelpRequestDatabase
            {
                Requests = envelope.Requests.Select(item => item!).ToList()
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

    public static async Task WriteAtomicallyAsync(
        string filePath,
        HelpRequestDatabase database)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await WriteTempFileAsync(tempPath, database);
            File.Move(tempPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static async Task WriteTempFileAsync(
        string tempPath,
        HelpRequestDatabase database)
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
        var message = $"Invalid help request JSON at '{fullPath}': "
            + "the root must contain a non-null 'requests' array of objects.";
        return inner is null
            ? new InvalidDataException(message)
            : new InvalidDataException(message, inner);
    }

    private sealed class HelpRequestEnvelope
    {
        public List<HelpRequestRecord?>? Requests { get; init; }
    }
}

internal sealed class HelpRequestDatabase
{
    public List<HelpRequestRecord> Requests { get; init; } = [];
}
