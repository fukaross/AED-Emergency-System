using System.Text.Json;
using AedBackend.Core.Models.Course;
using AedBackend.Infrastructure.Course;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Course;

public sealed class JsonQuickActionStoreTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsSeedData_WhenFileMissing()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            var actions = await store.GetAllAsync(CancellationToken.None);

            Assert.Equal(2, actions.Count);
            Assert.Equal("qa-01", actions[0].Id);
            Assert.Equal("心肺复苏", actions[0].Title);
            Assert.Equal(3, actions[0].Steps.Count);
            Assert.Equal("qa-02", actions[1].Id);
            Assert.Equal("海姆立克急救法", actions[1].Title);
            Assert.True(File.Exists(path));
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task GetAllAsync_FiltersDisabledActions()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[]
        {
            Action("qa-01", "可见", enabled: true),
            Action("qa-02", "隐藏", enabled: false),
        });
        var store = CreateStore(path);

        try
        {
            var actions = await store.GetAllAsync(CancellationToken.None);
            Assert.Single(actions);
            Assert.Equal("可见", actions[0].Title);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task AddAsync_GeneratesAutoId()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[] { Action("qa-01", "第一个") });
        var store = CreateStore(path);

        try
        {
            var created = await store.AddAsync(new QuickActionRecord
            {
                Title = "新卡片",
                Icon = "/test.png",
            }, CancellationToken.None);

            Assert.Equal("qa-02", created.Id);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task UpdateAsync_ModifiesFields()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[] { Action("qa-01", "原始") });
        var store = CreateStore(path);

        try
        {
            var updated = new QuickActionRecord
            {
                Title = "已修改",
                SortOrder = 5,
                IsEnabled = false,
            };

            var result = await store.UpdateAsync("qa-01", updated, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("已修改", result!.Title);
            Assert.Equal(5, result.SortOrder);
            Assert.False(result.IsEnabled);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesAction()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[]
        {
            Action("qa-01", "A"),
            Action("qa-02", "B"),
        });
        var store = CreateStore(path);

        try
        {
            var deleted = await store.DeleteAsync("qa-01", CancellationToken.None);
            Assert.True(deleted);

            var all = await store.FindByIdAsync("qa-01", CancellationToken.None);
            Assert.Null(all);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    private static JsonQuickActionStore CreateStore(string path)
    {
        return new JsonQuickActionStore(Options.Create(new WeChatOptions
        {
            QuickActionsFilePath = path
        }));
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"quickactions-{Guid.NewGuid():N}",
            "quick-actions.json");
    }

    private static void DeleteTempDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            Directory.Delete(directory, true);
    }

    private static async Task SeedAsync(string path, QuickActionRecord[] actions)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, actions);
    }

    private static QuickActionRecord Action(string id, string title, bool enabled = true)
    {
        return new QuickActionRecord
        {
            Id = id,
            Title = title,
            Icon = "/test.png",
            SortOrder = 1,
            IsEnabled = enabled,
        };
    }
}
