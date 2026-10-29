using AedBackend.Infrastructure.Demo;

namespace AedBackend.Tests.Demo;

/**
 * init-demo-data 的 --data-root 边界：只接受绝对路径且位于本地数据范围内，
 * 拒绝源码/配置目录、磁盘根、仓外任意路径，且不提供强制重置/删除开关。
 */
public sealed class DemoDataRootPolicyTests
{
    private static readonly IReadOnlyList<string> AllowedRoots = DemoDataRootPolicy.ResolveAllowedRoots();

    [Fact]
    public void Resolve_RejectsRelativePath()
    {
        var exception = Assert.Throws<DemoInitializationException>(() =>
            DemoDataRootPolicy.Resolve("backend/data-demo", AllowedRoots));

        Assert.Contains("必须是绝对路径", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_RejectsEmptyPath()
    {
        var exception = Assert.Throws<DemoInitializationException>(() =>
            DemoDataRootPolicy.Resolve(" ", AllowedRoots));

        Assert.Contains("--data-root", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_RejectsDriveRoot()
    {
        var driveRoot = Path.GetPathRoot(Path.GetFullPath(AppContext.BaseDirectory))!;

        var exception = Assert.Throws<DemoInitializationException>(() =>
            DemoDataRootPolicy.Resolve(driveRoot, AllowedRoots));

        Assert.Contains("磁盘根目录", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_RejectsPathOutsideLocalDataScope()
    {
        var outside = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            $"aed-demo-outside-{Guid.NewGuid():N}");

        var exception = Assert.Throws<DemoInitializationException>(() =>
            DemoDataRootPolicy.Resolve(outside, AllowedRoots));

        Assert.Contains("越界", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_RejectsRepositorySourceDirectories()
    {
        var repositoryRoot = DemoDataRootPolicy.FindRepositoryRoot();
        Assert.NotNull(repositoryRoot);

        var sourcePath = Path.Combine(repositoryRoot!, "backend", "src", $"data-demo-{Guid.NewGuid():N}");
        var exception = Assert.Throws<DemoInitializationException>(() =>
            DemoDataRootPolicy.Resolve(sourcePath, AllowedRoots));

        Assert.Contains("源码/配置目录", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_AcceptsTempDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aed-demo-policy-{Guid.NewGuid():N}");

        var resolved = DemoDataRootPolicy.Resolve(tempPath, AllowedRoots);

        Assert.Equal(Path.GetFullPath(tempPath), resolved);
    }

    [Fact]
    public void Resolve_AcceptsRepositoryDataDirectory()
    {
        var repositoryRoot = DemoDataRootPolicy.FindRepositoryRoot();
        Assert.NotNull(repositoryRoot);
        var dataPath = Path.Combine(repositoryRoot!, "backend", $"data-policy-{Guid.NewGuid():N}");

        var resolved = DemoDataRootPolicy.Resolve(dataPath, AllowedRoots);

        Assert.Equal(Path.GetFullPath(dataPath), resolved);
    }

    [Fact]
    public void Resolve_AcceptsExplicitLocalDataRoot()
    {
        var declaredRoot = Path.Combine(Path.GetTempPath(), $"aed-demo-declared-{Guid.NewGuid():N}");
        var candidate = Path.Combine(declaredRoot, "data");
        var roots = AllowedRoots.Append(declaredRoot).ToArray();

        var resolved = DemoDataRootPolicy.Resolve(candidate, roots);

        Assert.Equal(Path.GetFullPath(candidate), resolved);
    }

    [Fact]
    public void ResolveAllowedRoots_IncludesEnvironmentDeclaredRoot()
    {
        var declaredRoot = Path.Combine(Path.GetTempPath(), $"aed-demo-env-root-{Guid.NewGuid():N}");
        var previous = Environment.GetEnvironmentVariable(DemoDataRootPolicy.AdditionalRootsEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                DemoDataRootPolicy.AdditionalRootsEnvironmentVariable,
                declaredRoot);

            var roots = DemoDataRootPolicy.ResolveAllowedRoots();

            Assert.Contains(Path.GetFullPath(declaredRoot), roots);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                DemoDataRootPolicy.AdditionalRootsEnvironmentVariable,
                previous);
        }
    }

    [Fact]
    public void ResolveAllowedRoots_RejectsRelativeEnvironmentRoot()
    {
        var previous = Environment.GetEnvironmentVariable(DemoDataRootPolicy.AdditionalRootsEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                DemoDataRootPolicy.AdditionalRootsEnvironmentVariable,
                "relative-root");

            Assert.Throws<DemoInitializationException>(() => DemoDataRootPolicy.ResolveAllowedRoots());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                DemoDataRootPolicy.AdditionalRootsEnvironmentVariable,
                previous);
        }
    }
}
