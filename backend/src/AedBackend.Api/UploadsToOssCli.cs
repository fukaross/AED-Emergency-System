using System.Diagnostics;
using AedBackend.Infrastructure.Upload;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AedBackend.Api;

internal static class UploadsToOssCli
{
    public const string CommandName = "migrate-uploads-to-oss";

    public static async Task<int> TryRunAsync(string[] args, CancellationToken ct)
    {
        if (args.Length == 0 || args[0] != CommandName) return -1;

        try
        {
            var options = Parse(args);
            EnsureServiceStopped(options.DryRun);
            var store = options.DryRun ? new DryRunOssObjectStore() : CreateOssStore();
            var report = await new UploadsToOssMigrator(store).RunAsync(options, ct);
            PrintReport(report, options.DryRun);
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"OSS uploads migration failed: {ex.Message}");
            return 2;
        }
    }

    private static UploadsToOssMigrationOptions Parse(string[] args)
    {
        return new UploadsToOssMigrationOptions(
            Required(args, "--db"),
            Required(args, "--uploads-root"),
            Required(args, "--old-base"),
            Required(args, "--new-base"),
            args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase));
    }

    private static string Required(string[] args, string name)
    {
        var value = ReadOption(args, name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"缺少参数: {name}")
            : value;
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (var i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return null;
    }

    private static IOssObjectStore CreateOssStore()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var options = new UploadOptions();
        configuration.GetSection(UploadOptions.SectionName).Bind(options);
        options.ApplyEnvironmentAliases(configuration);
        return new AliyunOssObjectStore(Options.Create(options));
    }

    private static void EnsureServiceStopped(bool dryRun)
    {
        if (dryRun || OperatingSystem.IsWindows()) return;
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "systemctl",
            ArgumentList = { "is-active", "--quiet", "aed-backend.service" },
            RedirectStandardError = true
        });
        process?.WaitForExit();
        if (process?.ExitCode == 0) throw new InvalidOperationException("aed-backend.service 仍处于 active，必须先停服");
    }

    private static void PrintReport(UploadsToOssMigrationReport report, bool dryRun)
    {
        Console.WriteLine(dryRun ? "OSS uploads migration dry-run completed" : "OSS uploads migration completed");
        Console.WriteLine($"scannedFields={report.ScannedFields}");
        Console.WriteLine($"urlReferences={report.UrlReferences}");
        Console.WriteLine($"uniqueObjectKeys={report.UniqueObjectKeys}");
        Console.WriteLine($"uploadedObjects={report.UploadedObjects}");
        Console.WriteLine($"existingObjects={report.ExistingObjects}");
        Console.WriteLine($"updatedFields={report.UpdatedFields}");
        Console.WriteLine($"alreadyMigratedReferences={report.AlreadyMigratedReferences}");
        Console.WriteLine($"staticSkippedValues={report.StaticSkippedValues}");
        Console.WriteLine($"missingLocalFiles={report.MissingLocalFiles}");
        Console.WriteLine($"oldBaseResidualReferences={report.OldBaseResidualReferences}");
        Console.WriteLine($"localUnreferencedFiles={report.LocalUnreferencedFiles}");
    }

    private sealed class DryRunOssObjectStore : IOssObjectStore
    {
        public Task PutObjectAsync(
            string key,
            Stream content,
            string contentType,
            long contentLength,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException("dry-run 不上传对象");
        }

        public Task<OssObjectInfo?> TryGetObjectAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult<OssObjectInfo?>(null);
        }
    }
}
