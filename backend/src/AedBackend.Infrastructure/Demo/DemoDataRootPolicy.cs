namespace AedBackend.Infrastructure.Demo;

/**
 * init-demo-data 的 --data-root 越界保护。
 * 只接受绝对路径，且必须严格位于本地数据范围内：
 *   - 本机临时目录；
 *   - 可识别的本仓根目录之下（但不得写进 config/demo/backend/src 等源目录）；
 *   - 环境变量 AED_DEMO_LOCAL_DATA_ROOTS 明确声明的本地数据根。
 * 不提供强制重置/删除开关：目标目录必须为空或不存在。
 */
public static class DemoDataRootPolicy
{
    public const string AdditionalRootsEnvironmentVariable = "AED_DEMO_LOCAL_DATA_ROOTS";

    private static readonly string[] ProtectedRepositoryPrefixes =
    [
        "backend/src",
        "backend/tests",
        "config",
        "demo",
        "miniprogram",
        "admin",
        ".git",
        ".github",
        "scripts"
    ];

    public static IReadOnlyList<string> ResolveAllowedRoots()
    {
        var roots = new List<string> { Path.GetTempPath() };
        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is not null) roots.Add(repositoryRoot);
        roots.AddRange(ReadAdditionalRoots());
        return roots;
    }

    /** 返回规范化后的绝对路径；越界或非法时抛 DemoInitializationException。 */
    public static string Resolve(string? dataRoot, IReadOnlyList<string> allowedRoots)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
        {
            throw new DemoInitializationException("缺少参数 --data-root（必须是绝对路径）");
        }

        if (!Path.IsPathFullyQualified(dataRoot))
        {
            throw new DemoInitializationException($"--data-root 必须是绝对路径：{dataRoot}");
        }

        var fullPath = Path.GetFullPath(dataRoot);
        if (IsDriveRoot(fullPath))
        {
            throw new DemoInitializationException($"--data-root 不能是磁盘根目录：{fullPath}");
        }

        foreach (var allowedRoot in allowedRoots)
        {
            if (!IsUnder(fullPath, allowedRoot)) continue;
            EnsureNotProtectedSourcePath(fullPath, allowedRoot);
            return fullPath;
        }

        throw new DemoInitializationException(
            $"--data-root 越界：{fullPath} 不在本地数据范围内。"
            + "只允许本仓目录之下、系统临时目录或 AED_DEMO_LOCAL_DATA_ROOTS 明确声明的本地数据根。");
    }

    /** 从构建输出向上寻找包含 backend/AedBackend.sln 与 config/site.json 的仓库根。 */
    public static string? FindRepositoryRoot(string? startDirectory = null)
    {
        var directory = new DirectoryInfo(startDirectory ?? AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend", "AedBackend.sln"))
                && File.Exists(Path.Combine(directory.FullName, "config", "site.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    public static bool IsUnder(string candidate, string root)
    {
        var fullCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (string.Equals(fullCandidate, fullRoot, StringComparison.OrdinalIgnoreCase)) return false;
        if (!fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) return false;
        if (fullCandidate.Length <= fullRoot.Length) return false;
        var boundary = fullCandidate[fullRoot.Length];
        return boundary == Path.DirectorySeparatorChar || boundary == Path.AltDirectorySeparatorChar;
    }

    private static void EnsureNotProtectedSourcePath(string fullPath, string allowedRoot)
    {
        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null || !IsUnder(fullPath, repositoryRoot)) return;

        var relative = Path.GetRelativePath(repositoryRoot, fullPath).Replace('\\', '/');
        foreach (var prefix in ProtectedRepositoryPrefixes)
        {
            if (relative.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith($"{prefix}/", StringComparison.OrdinalIgnoreCase))
            {
                throw new DemoInitializationException(
                    $"--data-root 不能指向源码/配置目录：{fullPath}（命中 {prefix}）。请改用后端 .gitignore 已忽略的本地数据目录。");
            }
        }
    }

    private static bool IsDriveRoot(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        return !string.IsNullOrEmpty(root)
            && string.Equals(
                Path.TrimEndingDirectorySeparator(fullPath),
                Path.TrimEndingDirectorySeparator(root),
                StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ReadAdditionalRoots()
    {
        var raw = Environment.GetEnvironmentVariable(AdditionalRootsEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        foreach (var item in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Path.IsPathFullyQualified(item))
            {
                throw new DemoInitializationException(
                    $"{AdditionalRootsEnvironmentVariable} 中的路径必须是绝对路径：{item}");
            }

            yield return Path.GetFullPath(item);
        }
    }
}
