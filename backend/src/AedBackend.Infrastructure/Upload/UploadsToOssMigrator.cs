using Microsoft.Data.Sqlite;

namespace AedBackend.Infrastructure.Upload;

public sealed class UploadsToOssMigrator
{
    private static readonly UploadFieldSpec[] Fields =
    [
        new("experience_users", "OpenId", "AvatarUrl", UploadFieldMode.Text),
        new("responder_profiles", "Id", "AvatarUrl", UploadFieldMode.Text),
        new("aed_devices", "Id", "Image", UploadFieldMode.Text),
        new("help_images", "Id", "Url", UploadFieldMode.Text),
        new("help_report_images", "Id", "Url", UploadFieldMode.Text),
        new("courses", "Id", "Thumbnail", UploadFieldMode.Text),
        new("courses", "Id", "VideoUrl", UploadFieldMode.Text),
        new("course_videos", "Id", "Url", UploadFieldMode.Text),
        new("course_steps", "Id", "Image", UploadFieldMode.Text),
        new("quick_actions", "Id", "Icon", UploadFieldMode.Text),
        new("quick_action_steps", "Id", "Image", UploadFieldMode.Text),
        new("courses", "Id", "Content", UploadFieldMode.Text),
        new("volunteer_applications", "Id", "PayloadJson", UploadFieldMode.Json),
        new("school_binding_applications", "Id", "PayloadJson", UploadFieldMode.Json),
        new("responder_claim_applications", "Id", "PayloadJson", UploadFieldMode.Json)
    ];

    private readonly IOssObjectStore objectStore;

    public UploadsToOssMigrator(IOssObjectStore objectStore)
    {
        this.objectStore = objectStore;
    }

    public async Task<UploadsToOssMigrationReport> RunAsync(
        UploadsToOssMigrationOptions options,
        CancellationToken ct)
    {
        ValidateOptions(options);
        await using var connection = new SqliteConnection($"Data Source={Path.GetFullPath(options.DatabasePath)};Pooling=False");
        await connection.OpenAsync(ct);
        var scan = await ScanAsync(connection, null, options, ct);
        var objects = CreateObjectPlan(scan.References, options);
        var localUnreferenced = CountLocalUnreferenced(objects, options.UploadsRoot);
        if (scan.MissingLocalFiles > 0) throw new InvalidOperationException($"存在数据库引用但本地文件缺失: {scan.MissingLocalFiles}");

        var upload = options.DryRun ? new UploadStats(0, 0) : await UploadObjectsAsync(objects, ct);
        var updates = options.DryRun ? 0 : await ApplyUpdatesAsync(connection, options, scan.Updates, ct);
        var residual = options.DryRun ? scan.OldBaseResidualReferences
            : await CountResidualAsync(connection, options, ct);
        if (!options.DryRun && residual > 0)
            throw new InvalidOperationException($"迁移后仍存在旧域名引用: {residual}");

        return new UploadsToOssMigrationReport(
            scan.ScannedFields,
            scan.UrlReferences,
            objects.Count,
            upload.Uploaded,
            upload.Existing,
            updates,
            scan.AlreadyMigratedReferences,
            scan.StaticSkippedValues,
            scan.MissingLocalFiles,
            residual,
            localUnreferenced);
    }

    private static void ValidateOptions(UploadsToOssMigrationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DatabasePath)) throw new InvalidOperationException("--db 必填");
        if (string.IsNullOrWhiteSpace(options.UploadsRoot)) throw new InvalidOperationException("--uploads-root 必填");
        if (string.IsNullOrWhiteSpace(options.OldBase)) throw new InvalidOperationException("--old-base 必填");
        if (string.IsNullOrWhiteSpace(options.NewBase)) throw new InvalidOperationException("--new-base 必填");
        if (!File.Exists(options.DatabasePath)) throw new FileNotFoundException("SQLite 数据库不存在", options.DatabasePath);
        if (!Directory.Exists(options.UploadsRoot)) throw new DirectoryNotFoundException($"运行数据目录不存在: {options.UploadsRoot}");
    }

    private static async Task<UploadScanResult> ScanAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        UploadsToOssMigrationOptions options,
        CancellationToken ct)
    {
        var rewriter = new UploadUrlRewriter(options.OldBase, options.NewBase);
        var result = new UploadScanBuilder();
        foreach (var spec in Fields)
        {
            foreach (var item in await ReadFieldValuesAsync(connection, transaction, spec, ct))
                result.Add(item, Rewrite(item, rewriter), options);
        }
        return result.Build();
    }

    private static UrlRewriteResult Rewrite(UploadFieldValue item, UploadUrlRewriter rewriter)
    {
        return item.Spec.Mode == UploadFieldMode.Json
            ? rewriter.RewriteJson(item.Value)
            : rewriter.RewriteText(item.Value);
    }

    private static async Task<List<UploadFieldValue>> ReadFieldValuesAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        UploadFieldSpec spec,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {spec.KeyColumn}, {spec.ValueColumn} FROM {spec.Table}";
        var values = new List<UploadFieldValue>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (reader.IsDBNull(1)) continue;
            values.Add(new UploadFieldValue(spec, reader.GetValue(0).ToString()!, reader.GetString(1)));
        }
        return values;
    }

    private static List<UploadObjectPlan> CreateObjectPlan(
        IReadOnlySet<string> references,
        UploadsToOssMigrationOptions options)
    {
        return references
            .Select(path => ToObjectPlan(path, options))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToList();
    }

    private static UploadObjectPlan ToObjectPlan(string relativePath, UploadsToOssMigrationOptions options)
    {
        var localPath = Path.GetFullPath(Path.Combine(options.UploadsRoot, relativePath));
        var length = File.Exists(localPath) ? new FileInfo(localPath).Length : -1;
        return new UploadObjectPlan(
            relativePath,
            localPath,
            relativePath.Replace('\\', '/'),
            length,
            OssUploadSupport.ContentTypeForExtension(Path.GetExtension(relativePath)));
    }

    private async Task<UploadStats> UploadObjectsAsync(
        IReadOnlyList<UploadObjectPlan> objects,
        CancellationToken ct)
    {
        var stats = new UploadStats(0, 0);
        foreach (var item in objects)
        {
            ct.ThrowIfCancellationRequested();
            var existing = await objectStore.TryGetObjectAsync(item.Key, ct);
            if (existing?.ContentLength == item.Length)
            {
                stats = stats with { Existing = stats.Existing + 1 };
                continue;
            }
            using var stream = File.OpenRead(item.LocalPath);
            await objectStore.PutObjectAsync(item.Key, stream, item.ContentType, item.Length, ct);
            await VerifyUploadedAsync(item, ct);
            stats = stats with { Uploaded = stats.Uploaded + 1 };
        }
        return stats;
    }

    private async Task VerifyUploadedAsync(UploadObjectPlan item, CancellationToken ct)
    {
        var metadata = await objectStore.TryGetObjectAsync(item.Key, ct);
        if (metadata?.ContentLength != item.Length)
            throw new InvalidOperationException($"OSS object 校验失败: {item.Key}");
    }

    private static async Task<int> ApplyUpdatesAsync(
        SqliteConnection connection,
        UploadsToOssMigrationOptions options,
        IReadOnlyList<UploadFieldUpdate> updates,
        CancellationToken ct)
    {
        await using var transaction = await connection.BeginTransactionAsync(ct);
        foreach (var update in updates)
            await ExecuteUpdateAsync(connection, (SqliteTransaction)transaction, update, ct);
        var residual = await CountResidualAsync(connection, options, (SqliteTransaction)transaction, ct);
        if (residual > 0) throw new InvalidOperationException($"迁移后仍存在旧域名引用: {residual}");
        await transaction.CommitAsync(ct);
        return updates.Count;
    }

    private static async Task ExecuteUpdateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        UploadFieldUpdate update,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            UPDATE {update.Spec.Table}
            SET {update.Spec.ValueColumn} = $value
            WHERE {update.Spec.KeyColumn} = $key
            """;
        command.Parameters.AddWithValue("$value", update.Value);
        command.Parameters.AddWithValue("$key", update.Key);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> CountResidualAsync(
        SqliteConnection connection,
        UploadsToOssMigrationOptions options,
        CancellationToken ct)
    {
        return await CountResidualAsync(connection, options, null, ct);
    }

    private static async Task<int> CountResidualAsync(
        SqliteConnection connection,
        UploadsToOssMigrationOptions options,
        SqliteTransaction? transaction,
        CancellationToken ct)
    {
        var scan = await ScanAsync(connection, transaction, options, ct);
        return scan.OldBaseResidualReferences;
    }

    private static int CountLocalUnreferenced(
        IReadOnlyList<UploadObjectPlan> objects,
        string uploadsRoot)
    {
        var uploadsDir = Path.Combine(uploadsRoot, "uploads");
        if (!Directory.Exists(uploadsDir)) return 0;
        var referenced = objects.Select(item => item.LocalPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(uploadsDir, "*", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .Count(path => !referenced.Contains(path));
    }

    private sealed record UploadStats(int Uploaded, int Existing);
}
