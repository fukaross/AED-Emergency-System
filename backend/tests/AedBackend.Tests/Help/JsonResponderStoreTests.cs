using System.Text.Json;
using AedBackend.Core.Models.Responder;
using AedBackend.Infrastructure.Help;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Help;

/**
 * JSON provider 已移除真实急救员种子：档案只来自既有 JSON 文件，缺失时为空且不写盘。
 * 本测试使用自建合成 fixture（非真实姓名/证书/身份），校验读取、更新与脱敏语义未变。
 */
public sealed class JsonResponderStoreTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsEmptyAndDoesNotWriteFile_WhenFileMissing()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            var profiles = await store.GetAllAsync(CancellationToken.None);

            Assert.Empty(profiles);
            Assert.False(File.Exists(path));
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task GetAllAsync_ReadsSyntheticFixtureProfiles()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, SyntheticResponder("rescuer-001"), SyntheticResponder("rescuer-002", campus: "west"));
        var store = CreateStore(path);

        try
        {
            var profiles = await store.GetAllAsync(CancellationToken.None);

            Assert.Equal(2, profiles.Count);
            Assert.Contains(profiles, p => p.Id == "rescuer-001" && p.Name == "演示急救员甲");
            Assert.Contains(profiles, p => p.Id == "rescuer-002" && p.Campus == "west");
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsCorrectRescuer()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, SyntheticResponder("rescuer-001"));
        var store = CreateStore(path);

        try
        {
            var profile = await store.FindByIdAsync("rescuer-001", CancellationToken.None);
            Assert.NotNull(profile);
            Assert.Equal("演示急救员甲", profile.Name);
            Assert.Equal("female", profile.Gender);
            Assert.Equal("1990-01-01", profile.BirthDate);
            Assert.Equal("13900000001", profile.Phone);
            Assert.Equal("990000199001010017", profile.IdNumber);
            Assert.Equal("TESTCERT0001", profile.CertificateNo);
            Assert.Equal("TEST****0001", profile.CertificateNoMasked);
            Assert.Equal("east", profile.Campus);
            Assert.Equal("official", profile.Level);
            Assert.True(profile.SkillCpr);
            Assert.True(profile.SkillAed);
            Assert.True(profile.SkillStb);

            var missing = await store.FindByIdAsync("rescuer-999", CancellationToken.None);
            Assert.Null(missing);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    /** 缺文件时不得生成任何内置档案（原实现会静默写入真实档案，已移除）。 */
    [Fact]
    public async Task GetAllAsync_DoesNotFabricateSensitiveFields_WhenFileMissing()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            var profiles = await store.GetAllAsync(CancellationToken.None);

            Assert.Empty(profiles);
            Assert.DoesNotContain(profiles, p => !string.IsNullOrEmpty(p.Phone));
            Assert.DoesNotContain(profiles, p => !string.IsNullOrEmpty(p.CertificateNo));
            Assert.DoesNotContain(profiles, p => !string.IsNullOrEmpty(p.IdNumber));
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task GetAllAsync_PreservesSyntheticIdNumbers()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, SyntheticResponder("rescuer-001"), SyntheticResponder("rescuer-002"));
        var store = CreateStore(path);

        try
        {
            var profiles = await store.GetAllAsync(CancellationToken.None);

            Assert.All(profiles, profile => Assert.Equal(18, profile.IdNumber.Length));
            Assert.Equal("990000199001010017",
                profiles.First(p => p.Id == "rescuer-001").IdNumber);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task UpdateAsync_UpdatesMutableFields()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, SyntheticResponder("rescuer-001"));
        var store = CreateStore(path);

        try
        {
            var updated = new ResponderProfileRecord
            {
                Id = "rescuer-001",
                Name = "演示急救员甲（已更新）",
                Phone = "13900000009",
                Campus = "west",
                Level = "official",
                QualificationStatus = "inactive",
                Lat = 31.2188,
                Lng = 121.4536,
                SkillCpr = false
            };

            var result = await store.UpdateAsync("rescuer-001", updated, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal("演示急救员甲（已更新）", result.Name);
            Assert.Equal("13900000009", result.Phone);
            Assert.Equal("west", result.Campus);
            Assert.Equal("official", result.Level);
            Assert.Equal("inactive", result.QualificationStatus);
            Assert.Equal(31.2188, result.Lat);
            Assert.Equal(121.4536, result.Lng);
            Assert.False(result.SkillCpr);
            Assert.Equal("female", result.Gender);

            var persisted = await store.FindByIdAsync("rescuer-001", CancellationToken.None);
            Assert.NotNull(persisted);
            Assert.Equal("演示急救员甲（已更新）", persisted.Name);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullForMissingRescuer()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            var result = await store.UpdateAsync("rescuer-999",
                new ResponderProfileRecord { Id = "rescuer-999", Name = "x" },
                CancellationToken.None);
            Assert.Null(result);
            Assert.False(File.Exists(path));
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task GetAllAsync_PreservesFileOrder()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, SyntheticResponder("rescuer-001"), SyntheticResponder("rescuer-002"));
        var store = CreateStore(path);

        try
        {
            var profiles = await store.GetAllAsync(CancellationToken.None);
            var ids = profiles.Select(p => p.Id).ToArray();

            Assert.Equal("rescuer-001", ids[0]);
            Assert.Equal("rescuer-002", ids[1]);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task GetAllAsync_PreservesCampusCodesFromFile()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path,
            SyntheticResponder("rescuer-001"),
            SyntheticResponder("rescuer-002", campus: "west"));
        var store = CreateStore(path);

        try
        {
            var profiles = await store.GetAllAsync(CancellationToken.None);
            var east = profiles.Count(p => p.Campus == "east");
            var west = profiles.Count(p => p.Campus == "west");

            Assert.Equal(1, east);
            Assert.Equal(1, west);
            Assert.Equal("west", profiles.First(p => p.Id == "rescuer-002").Campus);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task UpdateAsync_RecalculatesCertificateNoMasked()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, SyntheticResponder("rescuer-001"));
        var store = CreateStore(path);

        try
        {
            var updated = new ResponderProfileRecord
            {
                Id = "rescuer-001",
                Name = "演示急救员甲",
                AvatarUrl = "",
                Phone = "",
                Campus = "east",
                Level = "trainee",
                QualificationStatus = "active",
                Lat = 31.245,
                Lng = 121.505,
                CertificateNo = "123456789012345678",
                SkillCpr = true,
                SkillAed = true,
                SkillStb = true
            };

            var result = await store.UpdateAsync("rescuer-001", updated, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal("123456789012345678", result.CertificateNo);
            Assert.Equal("1234****5678", result.CertificateNoMasked);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task UpdateAsync_ClearsCertificateNoMasked()
    {
        var path = CreateTempFilePath();
        await SeedAsync(path, SyntheticResponder("rescuer-001"));
        var store = CreateStore(path);

        try
        {
            var updated = new ResponderProfileRecord
            {
                Id = "rescuer-001",
                Name = "演示急救员甲",
                AvatarUrl = "",
                Phone = "",
                Campus = "east",
                Level = "trainee",
                QualificationStatus = "active",
                Lat = 31.245,
                Lng = 121.505,
                CertificateNo = null,
                SkillCpr = true,
                SkillAed = true,
                SkillStb = true
            };

            var result = await store.UpdateAsync("rescuer-001", updated, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Null(result.CertificateNo);
            Assert.Null(result.CertificateNoMasked);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    private static ResponderProfileRecord SyntheticResponder(string id, string campus = "east")
    {
        return new ResponderProfileRecord
        {
            Id = id,
            RecordId = $"FIXTURE-{id}",
            Name = id == "rescuer-001" ? "演示急救员甲" : "演示急救员乙",
            Gender = "female",
            BirthDate = "1990-01-01",
            Phone = "13900000001",
            IdNumber = "990000199001010017",
            AvatarUrl = "/static/images/avatar-default.png",
            Campus = campus,
            Level = "official",
            QualificationStatus = "active",
            Lat = 31.2459,
            Lng = 121.5061,
            CertificateNo = "TESTCERT0001",
            CertificateNoMasked = "TEST****0001",
            IssuingAuthority = "演示培训中心（合成）",
            IssueDate = "2026-04-18",
            SkillCpr = true,
            SkillAed = true,
            SkillStb = true
        };
    }

    private static JsonResponderStore CreateStore(string path)
    {
        return new JsonResponderStore(Options.Create(new WeChatOptions
        {
            RescuerProfilesFilePath = path
        }));
    }

    private static async Task SeedAsync(string path, params ResponderProfileRecord[] profiles)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, profiles, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(),
            $"rescuer-profiles-{Guid.NewGuid():N}",
            "rescuer-profiles.json");
    }

    private static void DeleteTempDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (directory is not null && Directory.Exists(directory))
            Directory.Delete(directory, true);
    }
}
