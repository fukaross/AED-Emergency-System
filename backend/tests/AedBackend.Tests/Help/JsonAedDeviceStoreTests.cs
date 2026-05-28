using System.Text.Json;
using AedBackend.Core.Models.Aed;
using AedBackend.Infrastructure.Help;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Help;

public sealed class JsonAedDeviceStoreTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsAllDevices()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[]
        {
            Device("AED-01", "实验楼", "online"),
            Device("AED-02", "图书馆", "offline", "电池已耗尽"),
        });
        var store = CreateStore(path);

        try
        {
            var devices = await store.GetAllAsync(CancellationToken.None);

            Assert.Equal(2, devices.Count);
            Assert.Contains(devices, d => d.Id == "AED-01" && d.Status == "online");
            Assert.Contains(devices, d => d.Id == "AED-02" && d.Status == "offline" && d.StatusNote == "电池已耗尽");
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task AddAsync_AutoGeneratesId()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[]
        {
            Device("AED-01", "实验楼", "online"),
        });
        var store = CreateStore(path);

        try
        {
            var device = new AedDeviceRecord
            {
                Name = "新设备",
                Lat = 20.0,
                Lng = 110.0,
                Status = "online"
            };

            var created = await store.AddAsync(device, CancellationToken.None);

            Assert.Equal("AED-02", created.Id);
            Assert.Equal("新设备", created.Name);

            var all = await store.GetAllAsync(CancellationToken.None);
            Assert.Equal(2, all.Count);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task AddAsync_ThrowsOnDuplicateName()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[]
        {
            Device("AED-01", "实验楼", "online"),
        });
        var store = CreateStore(path);

        try
        {
            var device = new AedDeviceRecord
            {
                Name = "实验楼",
                Lat = 20.0,
                Lng = 110.0,
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.AddAsync(device, CancellationToken.None));
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFieldsAndEnforcesOfflineNote()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[]
        {
            Device("AED-01", "实验楼", "online"),
        });
        var store = CreateStore(path);

        try
        {
            var updated = new AedDeviceRecord
            {
                Name = "实验楼A",
                Status = "offline",
                StatusNote = "设备检修",
                Address = "新地址",
            };

            var result = await store.UpdateAsync("AED-01", updated, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("实验楼A", result!.Name);
            Assert.Equal("offline", result.Status);
            Assert.Equal("设备检修", result.StatusNote);
            Assert.Equal("新地址", result.Address);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenOfflineWithoutNote()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[]
        {
            Device("AED-01", "实验楼", "online"),
        });
        var store = CreateStore(path);

        try
        {
            var updated = new AedDeviceRecord
            {
                Status = "offline",
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.UpdateAsync("AED-01", updated, CancellationToken.None));
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesDevice()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[]
        {
            Device("AED-01", "实验楼", "online"),
            Device("AED-02", "图书馆", "online"),
        });
        var store = CreateStore(path);

        try
        {
            var deleted = await store.DeleteAsync("AED-01", CancellationToken.None);

            Assert.True(deleted);

            var remaining = await store.GetAllAsync(CancellationToken.None);
            Assert.Single(remaining);
            Assert.Equal("AED-02", remaining[0].Id);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseWhenNotFound()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[]
        {
            Device("AED-01", "实验楼", "online"),
        });
        var store = CreateStore(path);

        try
        {
            var deleted = await store.DeleteAsync("AED-99", CancellationToken.None);

            Assert.False(deleted);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task ExistsByNameAsync_DetectsDuplicateWithExclude()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, new[]
        {
            Device("AED-01", "实验楼", "online"),
            Device("AED-02", "图书馆", "online"),
        });
        var store = CreateStore(path);

        try
        {
            Assert.True(await store.ExistsByNameAsync("实验楼", null, CancellationToken.None));
            Assert.True(await store.ExistsByNameAsync("图书馆", null, CancellationToken.None));
            Assert.False(await store.ExistsByNameAsync("不存在的设备", null, CancellationToken.None));

            Assert.False(await store.ExistsByNameAsync("实验楼", "AED-01", CancellationToken.None));
            Assert.True(await store.ExistsByNameAsync("实验楼", "AED-02", CancellationToken.None));
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task AddAsync_EmptyListStartsWithAed01()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, Array.Empty<AedDeviceRecord>());
        var store = CreateStore(path);

        try
        {
            var device = new AedDeviceRecord
            {
                Name = "首台设备",
                Lat = 20.0,
                Lng = 110.0,
            };

            var created = await store.AddAsync(device, CancellationToken.None);

            Assert.Equal("AED-01", created.Id);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    private static JsonAedDeviceStore CreateStore(string path)
    {
        return new JsonAedDeviceStore(Options.Create(new WeChatOptions
        {
            AedDevicesFilePath = path
        }));
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"aed-devices-{Guid.NewGuid():N}",
            "aed-devices.json");
    }

    private static void DeleteTempDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private static async Task SeedAsync(string path, AedDeviceRecord[] devices)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, devices);
    }

    private static AedDeviceRecord Device(
        string id,
        string name,
        string status,
        string? statusNote = null)
    {
        return new AedDeviceRecord
        {
            Id = id,
            Name = name,
            Lat = 19.97,
            Lng = 110.51,
            Status = status,
            StatusNote = statusNote,
            Address = $"REDACTED{name}",
            LastMaintenance = "2026-03-15",
            NextMaintenance = "2026-09-15",
            Maintainer = "待分配"
        };
    }
}
