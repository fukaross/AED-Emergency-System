using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Infrastructure.Site;

namespace AedBackend.Infrastructure.Demo;

/**
 * init-demo-data 的实现：从 demo/ 的只读产物准备迁移器输入 → 用既有 JsonToSqliteMigrator
 * 建库 → 补齐学校档案/身份绑定并对账 → 原子移动到 --data-root。
 * 只初始化数据：不启动服务、不发 HTTP、不创建管理员；不提供强制重置/覆盖开关。
 */
public sealed class DemoDataInitializer
{
    public async Task<DemoInitializationReport> RunAsync(
        DemoInitializationRequest request,
        CancellationToken ct)
    {
        var site = SiteConfigurationLoader.Load(request.SiteConfigPath);
        var roles = DemoRoleCatalog.LoadOrThrow(request.RolesFilePath);
        var demoDataRoot = Path.GetFullPath(request.DemoDataRoot);
        var source = await DemoSourceValidator.ValidateAsync(demoDataRoot, site, roles.Document, ct);

        var allowedRoots = DemoDataRootPolicy.ResolveAllowedRoots()
            .Concat(request.AdditionalAllowedRoots)
            .ToArray();
        var dataRoot = DemoDataRootPolicy.Resolve(request.DataRoot, allowedRoots);
        EnsureTargetDirectoryEmpty(dataRoot);

        var parent = Path.GetDirectoryName(dataRoot)
            ?? throw new DemoInitializationException($"无法解析 --data-root 的父目录：{dataRoot}");
        Directory.CreateDirectory(parent);
        var stagingRoot = Path.Combine(parent, $".demo-init-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);

        var databasePath = Path.Combine(dataRoot, DemoDataLayout.DatabaseFileName);
        var uploadsPath = Path.Combine(dataRoot, DemoDataLayout.UploadsDirectoryName);
        var dataRootExisted = Directory.Exists(dataRoot);
        var databaseMoved = false;

        try
        {
            var stagingData = Path.Combine(stagingRoot, "data");
            var stagingDatabase = Path.Combine(stagingRoot, DemoDataLayout.DatabaseFileName);
            CopyDemoData(demoDataRoot, stagingData);

            await new JsonToSqliteMigrator().RunAsync(stagingData, stagingDatabase, ct);
            var (counts, readiness) = await DemoSeedSupplement.ApplyAsync(
                stagingDatabase, roles.Document, source, ct);
            DemoSeedSupplement.DeleteSidecarFiles(stagingDatabase);

            // 发布前再次确认目标仍为空，避免与并发写入竞争后覆盖。
            EnsureTargetDirectoryEmpty(dataRoot);
            Directory.CreateDirectory(dataRoot);
            Directory.CreateDirectory(uploadsPath);
            File.Move(stagingDatabase, databasePath);
            databaseMoved = true;

            return new DemoInitializationReport(
                dataRoot,
                databasePath,
                uploadsPath,
                roles.Document.DataVersion ?? "unknown",
                readiness,
                SortCounts(counts));
        }
        catch
        {
            CleanupAfterFailure(dataRoot, dataRootExisted, databaseMoved, databasePath, uploadsPath);
            throw;
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    private static void EnsureTargetDirectoryEmpty(string dataRoot)
    {
        if (File.Exists(dataRoot))
        {
            throw new DemoInitializationException($"--data-root 指向已存在的文件：{dataRoot}");
        }

        if (Directory.Exists(dataRoot) && Directory.EnumerateFileSystemEntries(dataRoot).Any())
        {
            throw new DemoInitializationException(
                $"--data-root 目标目录非空，拒绝覆盖：{dataRoot}。"
                + "初始化不提供强制重置/删除开关，请换一个空目录或先人工确认后移除旧数据。");
        }
    }

    private static void CopyDemoData(string demoDataRoot, string stagingData)
    {
        Directory.CreateDirectory(stagingData);
        foreach (var fileName in DemoDataLayout.RequiredFiles)
        {
            File.Copy(Path.Combine(demoDataRoot, fileName), Path.Combine(stagingData, fileName));
        }
    }

    private static void CleanupAfterFailure(
        string dataRoot,
        bool dataRootExisted,
        bool databaseMoved,
        string databasePath,
        string uploadsPath)
    {
        if (databaseMoved && File.Exists(databasePath)) File.Delete(databasePath);
        if (Directory.Exists(uploadsPath) && !Directory.EnumerateFileSystemEntries(uploadsPath).Any())
        {
            Directory.Delete(uploadsPath);
        }

        if (dataRootExisted || !Directory.Exists(dataRoot)) return;
        if (Directory.EnumerateFileSystemEntries(dataRoot).Any()) return;
        Directory.Delete(dataRoot);
    }

    private static IReadOnlyDictionary<string, int> SortCounts(IReadOnlyDictionary<string, int> counts)
    {
        return counts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch (IOException)
        {
            // 临时目录清理失败不应掩盖初始化结果；下一次初始化会使用新的临时目录。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
