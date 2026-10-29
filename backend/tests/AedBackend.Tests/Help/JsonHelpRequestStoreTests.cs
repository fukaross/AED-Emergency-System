using System.Text.Json;
using AedBackend.Core.Models.Help;
using AedBackend.Infrastructure.Help;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Help;

public sealed class JsonHelpRequestStoreTests
{
    [Fact]
    public async Task ListByUserAsync_ReturnsRequesterRecords()
    {
        var path = CreateTempFilePath();
        await SeedHelpRequestsAsync(path, new[]
        {
            new HelpSeedData("help-1", "HELP01", "openid-a", HelpStatuses.Published, null),
            new HelpSeedData("help-2", "HELP02", "openid-b", HelpStatuses.Closed, null),
        });
        var store = CreateStore(path);

        try
        {
            var records = await store.ListByUserAsync("openid-a", CancellationToken.None);

            var result = Assert.Single(records);
            Assert.Equal("help-1", result.Id);
            Assert.Equal("openid-a", result.UserId);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task ListByUserAsync_IncludesReporterRecords()
    {
        var path = CreateTempFilePath();
        await SeedHelpRequestsAsync(path, new[]
        {
            new HelpSeedData(
                "help-1", "HELP01", "openid-a", "pending_review",
                new HelpReportRecord[]
                {
                    new("report-1", "openid-b", ["/uploads/r1.jpg"], "现场处理完毕", false,
                        DateTimeOffset.UtcNow)
                }),
        });
        var store = CreateStore(path);

        try
        {
            var records = await store.ListByUserAsync("openid-b", CancellationToken.None);

            var result = Assert.Single(records);
            Assert.Equal("help-1", result.Id);
            Assert.Equal("openid-a", result.UserId);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task ListByUserAsync_ExcludesUnrelatedUser()
    {
        var path = CreateTempFilePath();
        await SeedHelpRequestsAsync(path, new[]
        {
            new HelpSeedData("help-1", "HELP01", "openid-a", HelpStatuses.Published, null),
            new HelpSeedData(
                "help-2", "HELP02", "openid-c", "pending_review",
                new HelpReportRecord[]
                {
                    new("report-2", "openid-a", ["/uploads/r2.jpg"], "误报", true,
                        DateTimeOffset.UtcNow)
                }),
        });
        var store = CreateStore(path);

        try
        {
            var records = await store.ListByUserAsync("openid-b", CancellationToken.None);

            Assert.Empty(records);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task ListByUserAsync_SelfPublishedAndSelfReported_NoDuplicate()
    {
        var path = CreateTempFilePath();
        await SeedHelpRequestsAsync(path, new[]
        {
            new HelpSeedData(
                "help-1", "HELP01", "openid-a", "pending_review",
                new HelpReportRecord[]
                {
                    new("report-1", "openid-a", ["/uploads/r1.jpg"], "自行处理", false,
                        DateTimeOffset.UtcNow)
                }),
        });
        var store = CreateStore(path);

        try
        {
            var records = await store.ListByUserAsync("openid-a", CancellationToken.None);

            Assert.Single(records);
            Assert.Equal("help-1", records[0].Id);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task ListByUserAsync_ReturnsBothRolesForSameUser()
    {
        var path = CreateTempFilePath();
        await SeedHelpRequestsAsync(path, new[]
        {
            new HelpSeedData("help-1", "HELP01", "openid-a", HelpStatuses.Closed, null),
            new HelpSeedData(
                "help-2", "HELP02", "openid-b", "pending_review",
                new HelpReportRecord[]
                {
                    new("report-2", "openid-a", ["/uploads/r2.jpg"], "已处置", false,
                        DateTimeOffset.UtcNow)
                }),
        });
        var store = CreateStore(path);

        try
        {
            var records = await store.ListByUserAsync("openid-a", CancellationToken.None);

            Assert.Equal(2, records.Count);
            Assert.Contains(records, r => r.Id == "help-1");
            Assert.Contains(records, r => r.Id == "help-2");
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    private static JsonHelpRequestStore CreateStore(string path)
    {
        return new JsonHelpRequestStore(Options.Create(new WeChatOptions
        {
            HelpRequestsFilePath = path
        }));
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"aed-help-requests-{Guid.NewGuid():N}",
            "help-requests.json");
    }

    private static void DeleteTempDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private static async Task SeedHelpRequestsAsync(string path, HelpSeedData[] seeds)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var items = seeds.Select(seed =>
        {
            var reports = seed.Reports is not null
                ? string.Join(",\n", seed.Reports.Select(r => $$"""
                          {
                            "id": "{{r.Id}}",
                            "reporterOpenId": "{{r.ReporterOpenId}}",
                            "images": {{SerializeStringArray(r.Images)}},
                            "recordText": "{{r.RecordText}}",
                            "isFalseAlarm": {{r.IsFalseAlarm.ToString().ToLowerInvariant()}},
                            "submittedAt": "{{r.SubmittedAt:O}}"
                          }
                  """))
                : string.Empty;

            return $$"""
                {
                  "id": "{{seed.Id}}",
                  "requestNo": "{{seed.RequestNo}}",
                  "userId": "{{seed.UserId}}",
                  "requestChannel": "user",
                  "lat": 1,
                  "lng": 1,
                  "address": "测试地点",
                  "content": "测试内容",
                  "type": "general",
                  "status": "{{seed.Status}}",
                  "createdAt": "{{DateTimeOffset.UtcNow:O}}",
                  "closedAt": null,
                  "closeNote": null,
                  "requesterPhone": "13800138000",
                  "images": [],
                  "responders": [],
                  "timeline": [],
                  "reports": [{{reports}}]
                }
            """;
        });

        var json = $$"""
        {
          "requests": [
        {{string.Join(",\n", items)}}
          ]
        }
        """;
        await File.WriteAllTextAsync(path, json);
    }

    private static string SerializeStringArray(IReadOnlyList<string> items)
    {
        if (items.Count == 0) return "[]";
        return "[\"" + string.Join("\", \"", items) + "\"]";
    }

    private sealed record HelpSeedData(
        string Id,
        string RequestNo,
        string UserId,
        string Status,
        IReadOnlyList<HelpReportRecord>? Reports)
    {
        public HelpSeedData(string id, string requestNo, string userId, string status)
            : this(id, requestNo, userId, status, null)
        {
        }
    }
}
