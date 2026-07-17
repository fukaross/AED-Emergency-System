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
        if (!File.Exists(filePath)) return new JsonRescueSessionDatabase();
        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<JsonRescueSessionDatabase>(stream, JsonOptions, ct)
            ?? new JsonRescueSessionDatabase();
    }

    public static async Task WriteAtomicallyAsync(
        string filePath,
        JsonRescueSessionDatabase database)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        var tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
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
            File.Move(tempPath, filePath, true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}

internal sealed class JsonRescueSessionDatabase
{
    public List<RescueSessionRecord> Sessions { get; init; } = [];
}
