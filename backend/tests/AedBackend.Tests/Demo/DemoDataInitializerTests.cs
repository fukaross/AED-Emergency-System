using System.Text.Json.Nodes;
using AedBackend.Infrastructure.Demo;
using Microsoft.Data.Sqlite;

namespace AedBackend.Tests.Demo;

/**
 * init-demo-data 端到端：从 demo/ 只读产物建库并补齐学校身份/绑定，
 * 拒绝已有库、非空目录、越界路径与不自洽的源数据；失败不留下可误用的数据库。
 */
public sealed class DemoDataInitializerTests
{
    [Fact]
    public async Task RunAsync_InitializesCompleteDemoDatabase()
    {
        var dataRoot = NewTempPath("aed-demo-init-ok");
        try
        {
            var report = await new DemoDataInitializer().RunAsync(
                new DemoInitializationRequest { DataRoot = dataRoot },
                CancellationToken.None);

            var databasePath = Path.Combine(dataRoot, DemoDataLayout.DatabaseFileName);
            Assert.Equal("demo-v1", report.DataVersion);
            Assert.True(File.Exists(databasePath));
            Assert.True(Directory.Exists(Path.Combine(dataRoot, DemoDataLayout.UploadsDirectoryName)));
            Assert.Equal(Path.GetFullPath(dataRoot), report.DataRoot);

            Assert.Equal(7, report.Counts["experience_users"]);
            Assert.Equal(7, report.Counts["responder_profiles"]);
            Assert.Equal(10, report.Counts["aed_devices"]);
            Assert.Equal(4, report.Counts["courses"]);
            Assert.Equal(2, report.Counts["quick_actions"]);
            Assert.Equal(1, report.Counts["knowledge_summary"]);
            Assert.Equal(0, report.Counts["admin_users"]);
            Assert.Equal(0, report.Counts["admin_sessions"]);
            Assert.Equal(0, report.Counts["help_requests"]);
            Assert.Equal(0, report.Counts["rescue_sessions"]);
            Assert.Equal(3, report.Counts["school_person_profiles"]);
            Assert.Equal(3, report.Counts["school_binding_applications"]);

            Assert.Equal(7, await ReadCountAsync(databasePath, "experience_users"));
            Assert.Equal(3, await ReadCountAsync(databasePath, "school_person_profiles"));
            Assert.Equal(3, await ReadCountAsync(databasePath, "school_binding_applications"));
            Assert.Equal("active", await ReadScalarAsync(
                databasePath,
                "SELECT QualificationStatus FROM responder_profiles WHERE Id = 'DEMO-RSP-001';"));
            Assert.Equal("demo-openid-rescuer-east", await ReadScalarAsync(
                databasePath,
                "SELECT BoundOpenId FROM responder_profiles WHERE Id = 'DEMO-RSP-001';"));
            Assert.Equal("approved", await ReadScalarAsync(
                databasePath,
                "SELECT Status FROM school_binding_applications WHERE OpenId = 'demo-openid-rescuer-east';"));

            var activeRescuer = report.Roles.Single(role => role.Role == "active-rescuer");
            Assert.True(activeRescuer.Certified);
            Assert.True(activeRescuer.SchoolBindingApproved);
            Assert.Equal("DEMO-RSP-001", activeRescuer.ResponderProfileId);
            var requester = report.Roles.Single(role => role.Role == "requester");
            Assert.False(requester.Certified);
            Assert.Null(requester.ResponderProfileId);
            var unqualified = report.Roles.Single(role => role.Role == "unqualified");
            Assert.False(unqualified.Certified);
        }
        finally
        {
            DeleteDirectory(dataRoot);
        }
    }

    [Fact]
    public async Task RunAsync_CleansStagingDirectory()
    {
        var parent = Path.Combine(Path.GetTempPath(), $"aed-demo-staging-{Guid.NewGuid():N}");
        var dataRoot = Path.Combine(parent, "data");
        try
        {
            await RunOnceAsync(dataRoot);

            Assert.Equal(
                ["data"],
                Directory.GetDirectories(parent).Select(path => Path.GetFileName(path) ?? string.Empty).ToArray());
            Assert.Empty(Directory.GetDirectories(parent, ".demo-init-*"));
        }
        finally
        {
            DeleteDirectory(parent);
        }
    }

    [Fact]
    public async Task RunAsync_RejectsExistingDatabase()
    {
        var dataRoot = NewTempPath("aed-demo-init-twice");
        try
        {
            await RunOnceAsync(dataRoot);

            var exception = await Assert.ThrowsAsync<DemoInitializationException>(() =>
                RunOnceAsync(dataRoot));
            Assert.Contains("非空", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(dataRoot);
        }
    }

    [Fact]
    public async Task RunAsync_RejectsNonEmptyTargetDirectory()
    {
        var dataRoot = NewTempPath("aed-demo-init-nonempty");
        try
        {
            Directory.CreateDirectory(dataRoot);
            await File.WriteAllTextAsync(Path.Combine(dataRoot, "existing.txt"), "keep me");

            var exception = await Assert.ThrowsAsync<DemoInitializationException>(() =>
                RunOnceAsync(dataRoot));
            Assert.Contains("非空", exception.Message, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(dataRoot, "existing.txt")));
        }
        finally
        {
            DeleteDirectory(dataRoot);
        }
    }

    [Fact]
    public async Task RunAsync_RejectsNonAbsoluteDataRoot()
    {
        var exception = await Assert.ThrowsAsync<DemoInitializationException>(() =>
            new DemoDataInitializer().RunAsync(
                new DemoInitializationRequest { DataRoot = "relative-demo-data" },
                CancellationToken.None));

        Assert.Contains("绝对路径", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_RejectsOutOfScopeDataRoot()
    {
        var outside = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            $"aed-demo-outside-{Guid.NewGuid():N}");

        var exception = await Assert.ThrowsAsync<DemoInitializationException>(() =>
            new DemoDataInitializer().RunAsync(
                new DemoInitializationRequest { DataRoot = outside },
                CancellationToken.None));

        Assert.Contains("越界", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(outside));
    }

    [Fact]
    public async Task RunAsync_RejectsMissingSiteConfiguration()
    {
        var dataRoot = NewTempPath("aed-demo-init-missing-site");
        var sitePath = Path.Combine(Path.GetTempPath(), $"missing-site-{Guid.NewGuid():N}", "site.json");
        try
        {
            await Assert.ThrowsAsync<AedBackend.Infrastructure.Site.SiteConfigurationException>(() =>
                new DemoDataInitializer().RunAsync(
                    new DemoInitializationRequest { DataRoot = dataRoot, SiteConfigPath = sitePath },
                    CancellationToken.None));
            Assert.False(File.Exists(Path.Combine(dataRoot, DemoDataLayout.DatabaseFileName)));
        }
        finally
        {
            DeleteDirectory(dataRoot);
        }
    }

    [Fact]
    public async Task RunAsync_RejectsMissingRolesArtifact()
    {
        var dataRoot = NewTempPath("aed-demo-init-missing-roles");
        var rolesPath = Path.Combine(Path.GetTempPath(), $"missing-roles-{Guid.NewGuid():N}", "demo-roles.json");
        try
        {
            var exception = await Assert.ThrowsAsync<DemoConfigurationException>(() =>
                new DemoDataInitializer().RunAsync(
                    new DemoInitializationRequest { DataRoot = dataRoot, RolesFilePath = rolesPath },
                    CancellationToken.None));
            Assert.Contains("演示角色注册表", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(dataRoot);
        }
    }

    [Fact]
    public async Task RunAsync_LeavesNoDatabaseWhenSourceDataIsInvalid()
    {
        var dataRoot = NewTempPath("aed-demo-init-bad-source");
        var sourceRoot = NewTempPath("aed-demo-bad-source");
        try
        {
            CopyDemoData(sourceRoot);
            await File.WriteAllTextAsync(
                Path.Combine(sourceRoot, "rescuer-profiles.json"),
                "{ this is not valid json");

            var exception = await Assert.ThrowsAsync<DemoInitializationException>(() =>
                new DemoDataInitializer().RunAsync(
                    new DemoInitializationRequest { DataRoot = dataRoot, DemoDataRoot = sourceRoot },
                    CancellationToken.None));

            Assert.Contains("不是合法 JSON", exception.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(dataRoot));
        }
        finally
        {
            DeleteDirectory(dataRoot);
            DeleteDirectory(sourceRoot);
        }
    }

    [Fact]
    public async Task RunAsync_RejectsOrphanCampusCodeInDemoData()
    {
        var dataRoot = NewTempPath("aed-demo-init-orphan-campus");
        var sourceRoot = NewTempPath("aed-demo-orphan-source");
        try
        {
            CopyDemoData(sourceRoot);
            await RewriteFirstResponderCampusAsync(sourceRoot, "ghost");

            var exception = await Assert.ThrowsAsync<DemoInitializationException>(() =>
                new DemoDataInitializer().RunAsync(
                    new DemoInitializationRequest { DataRoot = dataRoot, DemoDataRoot = sourceRoot },
                    CancellationToken.None));

            Assert.Contains("campus=ghost", exception.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(dataRoot));
        }
        finally
        {
            DeleteDirectory(dataRoot);
            DeleteDirectory(sourceRoot);
        }
    }

    [Fact]
    public async Task RunAsync_RejectsPreSeededAdminUser()
    {
        var dataRoot = NewTempPath("aed-demo-init-admin");
        var sourceRoot = NewTempPath("aed-demo-admin-source");
        try
        {
            CopyDemoData(sourceRoot);
            await File.WriteAllTextAsync(
                Path.Combine(sourceRoot, "admin-users.json"),
                """
                { "users": [ { "id": "admin-01", "username": "root", "normalizedUsername": "ROOT", "passwordHash": "x", "role": "super_admin", "status": "active", "createdAt": "2026-01-01T00:00:00+00:00" } ] }
                """);

            var exception = await Assert.ThrowsAsync<DemoInitializationException>(() =>
                new DemoDataInitializer().RunAsync(
                    new DemoInitializationRequest { DataRoot = dataRoot, DemoDataRoot = sourceRoot },
                    CancellationToken.None));

            Assert.Contains("不得预置管理员", exception.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(dataRoot));
        }
        finally
        {
            DeleteDirectory(dataRoot);
            DeleteDirectory(sourceRoot);
        }
    }

    private static Task RunOnceAsync(string dataRoot)
    {
        return new DemoDataInitializer().RunAsync(
            new DemoInitializationRequest { DataRoot = dataRoot },
            CancellationToken.None);
    }

    private static void CopyDemoData(string targetRoot)
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "config", "demo-data");
        Directory.CreateDirectory(targetRoot);
        foreach (var fileName in DemoDataLayout.RequiredFiles)
        {
            File.Copy(Path.Combine(sourceRoot, fileName), Path.Combine(targetRoot, fileName));
        }
    }

    private static async Task RewriteFirstResponderCampusAsync(string sourceRoot, string campus)
    {
        var path = Path.Combine(sourceRoot, "rescuer-profiles.json");
        var node = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsArray();
        node[0]!["campus"] = campus;
        await File.WriteAllTextAsync(path, node.ToJsonString());
    }

    private static async Task<int> ReadCountAsync(string databasePath, string table)
    {
        var value = await ReadScalarAsync(databasePath, $"SELECT COUNT(*) FROM {table};");
        return int.Parse(value);
    }

    private static async Task<string> ReadScalarAsync(string databasePath, string sql)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={Path.GetFullPath(databasePath)};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private static string NewTempPath(string prefix)
    {
        return Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
