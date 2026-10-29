namespace AedBackend.Infrastructure.Upload;

public sealed record UploadsToOssMigrationOptions(
    string DatabasePath,
    string UploadsRoot,
    string OldBase,
    string NewBase,
    bool DryRun);

public sealed record UploadsToOssMigrationReport(
    int ScannedFields,
    int UrlReferences,
    int UniqueObjectKeys,
    int UploadedObjects,
    int ExistingObjects,
    int UpdatedFields,
    int AlreadyMigratedReferences,
    int StaticSkippedValues,
    int MissingLocalFiles,
    int OldBaseResidualReferences,
    int LocalUnreferencedFiles);

internal sealed record UploadFieldSpec(
    string Table,
    string KeyColumn,
    string ValueColumn,
    UploadFieldMode Mode);

internal enum UploadFieldMode
{
    Text,
    Json
}

internal sealed record UploadFieldValue(
    UploadFieldSpec Spec,
    string Key,
    string Value);

internal sealed record UploadFieldUpdate(
    UploadFieldSpec Spec,
    string Key,
    string Value);

internal sealed record UploadObjectPlan(
    string RelativePath,
    string LocalPath,
    string Key,
    long Length,
    string ContentType);
