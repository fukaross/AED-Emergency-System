using AedBackend.Infrastructure.Upload;
using Microsoft.Data.Sqlite;

namespace AedBackend.Tests.Persistence;

public sealed class UploadsToOssMigratorTests
{
    private const string OldBase = "REDACTED";
    private const string NewBase = "REDACTED";

    [Fact]
    public async Task RunAsync_DryRunScansReferencesWithoutUploadingOrUpdating()
    {
        using var temp = new TempOssData();
        await temp.CreateDatabaseAsync();
        temp.WriteUpload("uploads/avatar/user.jpg", 10);
        temp.WriteUpload("uploads/rescuer/r.jpg", 20);
        temp.WriteUpload("uploads/volunteer/id.jpg", 30);
        temp.WriteUpload("uploads/classroom/step.jpg", 40);
        temp.WriteUpload("uploads/classroom-videos/v.mp4", 45);
        temp.WriteUpload("uploads/help/unreferenced.jpg", 50);
        var store = new FakeOssObjectStore();

        var report = await new UploadsToOssMigrator(store).RunAsync(temp.Options(dryRun: true), CancellationToken.None);

        Assert.Equal(5, report.UniqueObjectKeys);
        Assert.Equal(0, report.UploadedObjects);
        Assert.Equal(0, report.UpdatedFields);
        Assert.Equal(0, report.MissingLocalFiles);
        Assert.Equal(1, report.LocalUnreferencedFiles);
        Assert.Contains(OldBase, await temp.ReadScalarAsync("experience_users", "AvatarUrl"));
    }

    [Fact]
    public async Task RunAsync_UploadsObjectsBeforeUpdatingDatabase()
    {
        using var temp = new TempOssData();
        await temp.CreateDatabaseAsync();
        foreach (var path in temp.ExpectedUploadPaths) temp.WriteUpload(path, 10);
        var store = new FakeOssObjectStore();

        var report = await new UploadsToOssMigrator(store).RunAsync(temp.Options(dryRun: false), CancellationToken.None);

        Assert.Equal(temp.ExpectedUploadPaths.Count, report.UploadedObjects);
        Assert.Equal(0, report.OldBaseResidualReferences);
        Assert.All(temp.ExpectedUploadPaths, path => Assert.Contains(path, store.Keys));
        Assert.StartsWith(NewBase, await temp.ReadScalarAsync("experience_users", "AvatarUrl"));
        Assert.Contains(NewBase, await temp.ReadScalarAsync("volunteer_applications", "PayloadJson"));
    }

    [Fact]
    public async Task RunAsync_FailsWhenDatabaseReferenceMissingLocalFile()
    {
        using var temp = new TempOssData();
        await temp.CreateDatabaseAsync();
        var store = new FakeOssObjectStore();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UploadsToOssMigrator(store).RunAsync(temp.Options(dryRun: false), CancellationToken.None));

        Assert.Contains("本地文件缺失", ex.Message);
        Assert.Empty(store.Keys);
        Assert.Contains(OldBase, await temp.ReadScalarAsync("experience_users", "AvatarUrl"));
    }

    private sealed class TempOssData : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"aed-oss-test-{Guid.NewGuid():N}");
        private readonly string dbPath;

        public TempOssData()
        {
            dbPath = Path.Combine(root, "aed.db");
            Directory.CreateDirectory(root);
        }

        public IReadOnlyList<string> ExpectedUploadPaths { get; } =
        [
            "uploads/avatar/user.jpg",
            "uploads/rescuer/r.jpg",
            "uploads/volunteer/id.jpg",
            "uploads/classroom/step.jpg",
            "uploads/classroom-videos/v.mp4"
        ];

        public UploadsToOssMigrationOptions Options(bool dryRun)
        {
            return new UploadsToOssMigrationOptions(dbPath, root, OldBase, NewBase, dryRun);
        }

        public async Task CreateDatabaseAsync()
        {
            await using var connection = new SqliteConnection(ConnectionString());
            await connection.OpenAsync();
            foreach (var sql in CreateSchemaSql()) await ExecuteAsync(connection, sql);
            foreach (var sql in SeedSql()) await ExecuteAsync(connection, sql);
        }

        public void WriteUpload(string path, int length)
        {
            var file = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, Enumerable.Repeat((byte)1, length).ToArray());
        }

        public async Task<string> ReadScalarAsync(string table, string column)
        {
            await using var connection = new SqliteConnection(ConnectionString());
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {column} FROM {table} LIMIT 1";
            return (await command.ExecuteScalarAsync())?.ToString() ?? string.Empty;
        }

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        private static async Task ExecuteAsync(SqliteConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        private string ConnectionString()
        {
            return $"Data Source={dbPath};Pooling=False";
        }

        private static IEnumerable<string> CreateSchemaSql()
        {
            yield return "CREATE TABLE experience_users (OpenId TEXT PRIMARY KEY, AvatarUrl TEXT)";
            yield return "CREATE TABLE responder_profiles (Id TEXT PRIMARY KEY, AvatarUrl TEXT)";
            yield return "CREATE TABLE aed_devices (Id TEXT PRIMARY KEY, Image TEXT)";
            yield return "CREATE TABLE help_images (Id INTEGER PRIMARY KEY, Url TEXT)";
            yield return "CREATE TABLE help_report_images (Id INTEGER PRIMARY KEY, Url TEXT)";
            yield return "CREATE TABLE courses (Id TEXT PRIMARY KEY, Thumbnail TEXT, VideoUrl TEXT, Content TEXT)";
            yield return "CREATE TABLE course_videos (Id INTEGER PRIMARY KEY, Url TEXT)";
            yield return "CREATE TABLE course_steps (Id INTEGER PRIMARY KEY, Image TEXT)";
            yield return "CREATE TABLE quick_actions (Id TEXT PRIMARY KEY, Icon TEXT)";
            yield return "CREATE TABLE quick_action_steps (Id INTEGER PRIMARY KEY, Image TEXT)";
            yield return "CREATE TABLE volunteer_applications (Id INTEGER PRIMARY KEY, PayloadJson TEXT)";
            yield return "CREATE TABLE school_binding_applications (Id INTEGER PRIMARY KEY, PayloadJson TEXT)";
            yield return "CREATE TABLE responder_claim_applications (Id INTEGER PRIMARY KEY, PayloadJson TEXT)";
        }

        private static IEnumerable<string> SeedSql()
        {
            const string content = "<img src=\"/uploads/classroom/step.jpg\">";
            var volunteerPayload = "{\"idPhoto\":{\"url\":\"" + OldBase + "/uploads/volunteer/id.jpg\"}}";
            yield return $"INSERT INTO experience_users VALUES ('u1', '{OldBase}/uploads/avatar/user.jpg')";
            yield return "INSERT INTO responder_profiles VALUES ('r1', '/uploads/rescuer/r.jpg')";
            yield return "INSERT INTO aed_devices VALUES ('aed1', '/static/images/aed-placeholder.png')";
            yield return "INSERT INTO help_images VALUES (1, '')";
            yield return "INSERT INTO help_report_images VALUES (1, '')";
            yield return $"INSERT INTO courses VALUES ('c1', '{OldBase}/uploads/classroom/step.jpg', '{OldBase}/uploads/classroom-videos/v.mp4', '{content}')";
            yield return "INSERT INTO course_videos VALUES (1, '')";
            yield return "INSERT INTO course_steps VALUES (1, '')";
            yield return "INSERT INTO quick_actions VALUES ('qa1', '')";
            yield return "INSERT INTO quick_action_steps VALUES (1, '')";
            yield return $"INSERT INTO volunteer_applications VALUES (1, '{volunteerPayload}')";
            yield return "INSERT INTO school_binding_applications VALUES (1, '{}')";
            yield return "INSERT INTO responder_claim_applications VALUES (1, '{}')";
        }
    }

    private sealed class FakeOssObjectStore : IOssObjectStore
    {
        private readonly Dictionary<string, OssObjectInfo> objects = [];

        public IReadOnlyCollection<string> Keys => objects.Keys;

        public Task PutObjectAsync(
            string key,
            Stream content,
            string contentType,
            long contentLength,
            CancellationToken cancellationToken)
        {
            objects[key] = new OssObjectInfo(contentLength, contentType);
            return Task.CompletedTask;
        }

        public Task<OssObjectInfo?> TryGetObjectAsync(string key, CancellationToken cancellationToken)
        {
            objects.TryGetValue(key, out var info);
            return Task.FromResult(info);
        }
    }
}
