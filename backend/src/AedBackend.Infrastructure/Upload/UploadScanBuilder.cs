namespace AedBackend.Infrastructure.Upload;

internal sealed class UploadScanBuilder
{
    private readonly HashSet<string> references = new(StringComparer.Ordinal);
    private readonly List<UploadFieldUpdate> updates = [];
    private int scannedFields;
    private int urlReferences;
    private int alreadyMigrated;
    private int staticSkipped;
    private int missing;
    private int residual;

    public void Add(
        UploadFieldValue item,
        UrlRewriteResult rewrite,
        UploadsToOssMigrationOptions options)
    {
        scannedFields++;
        if (IsStatic(item.Value)) staticSkipped++;
        urlReferences += rewrite.References.Count;
        alreadyMigrated += rewrite.References.Count(item => item.AlreadyMigrated);
        foreach (var reference in rewrite.References.Where(item => item.ShouldMigrate))
            AddReference(reference, options);
        if (rewrite.Changed) updates.Add(new UploadFieldUpdate(item.Spec, item.Key, rewrite.Value));
        residual += rewrite.References.Count(item => IsOldBase(item.Value, options));
    }

    public UploadScanResult Build()
    {
        return new UploadScanResult(
            scannedFields,
            urlReferences,
            references,
            updates,
            alreadyMigrated,
            staticSkipped,
            missing,
            residual);
    }

    private void AddReference(UploadUrlReference reference, UploadsToOssMigrationOptions options)
    {
        references.Add(reference.RelativePath);
        var localPath = Path.Combine(options.UploadsRoot, reference.RelativePath);
        if (!File.Exists(localPath)) missing++;
    }

    private static bool IsStatic(string value)
    {
        return value.TrimStart().StartsWith("/static/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOldBase(string value, UploadsToOssMigrationOptions options)
    {
        return value.StartsWith(options.OldBase.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record UploadScanResult(
    int ScannedFields,
    int UrlReferences,
    IReadOnlySet<string> References,
    IReadOnlyList<UploadFieldUpdate> Updates,
    int AlreadyMigratedReferences,
    int StaticSkippedValues,
    int MissingLocalFiles,
    int OldBaseResidualReferences);
