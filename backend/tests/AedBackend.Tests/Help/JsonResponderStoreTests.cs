using System.Text.Json;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Validators;
using AedBackend.Infrastructure.Help;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.Help;

public sealed class JsonResponderStoreTests
{
    [Fact]
    public async Task GetAllAsync_Returns31SeedRescuers()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            var profiles = await store.GetAllAsync(CancellationToken.None);

            Assert.Equal(31, profiles.Count);
            Assert.Contains(profiles, p => p.Id == "rescuer-001" && p.Name == "REDACTED");
            Assert.Contains(profiles, p => p.Id == "rescuer-031" && p.Name == "REDACTED");
            Assert.Equal("honor", profiles.First(p => p.Id == "rescuer-031").Level);
            Assert.All(profiles.Where(p => p.Id != "rescuer-031"), p =>
                Assert.Equal("trainee", p.Level));
            Assert.All(profiles, p => Assert.Equal("active", p.QualificationStatus));
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
        var store = CreateStore(path);

        try
        {
            var profile = await store.FindByIdAsync("rescuer-001", CancellationToken.None);
            Assert.NotNull(profile);
            Assert.Equal("REDACTED", profile.Name);
            Assert.Equal("女", profile.Gender);
            Assert.Equal("2004-12-22", profile.BirthDate);
            Assert.Equal("REDACTED", profile.Phone);
            Assert.Equal("REDACTED", profile.IdNumber);
            Assert.Equal("460000232004231001532", profile.CertificateNo);
            Assert.Equal("4600****1532", profile.CertificateNoMasked);
            Assert.Equal("REDACTED", profile.RecordId);
            Assert.Equal("guilinyang", profile.Campus);
            Assert.Equal("trainee", profile.Level);
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

    [Fact]
    public async Task GetAllAsync_SeedsRealArchiveSensitiveFields()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            var profiles = await store.GetAllAsync(CancellationToken.None);

            Assert.DoesNotContain(profiles, p => p.Phone.StartsWith("1390000"));
            Assert.DoesNotContain(profiles, p => p.CertificateNo?.StartsWith("TEST") == true);
            Assert.DoesNotContain(profiles, p => p.IdNumber.StartsWith("46010020000101"));
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task GetAllAsync_SeedsArchiveIdNumbers()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            var profiles = await store.GetAllAsync(CancellationToken.None);

            Assert.All(profiles, profile => Assert.Equal(18, profile.IdNumber.Length));
            Assert.Equal("REDACTED",
                profiles.First(p => p.Id == "rescuer-001").IdNumber);
            Assert.Equal("REDACTED",
                profiles.First(p => p.Id == "rescuer-031").IdNumber);
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
        var store = CreateStore(path);

        try
        {
            var updated = new ResponderProfileRecord
            {
                Id = "rescuer-001",
                Name = "REDACTED(已更新)",
                Phone = "REDACTED",
                Campus = "fucheng",
                Level = "official",
                QualificationStatus = "inactive",
                Lat = 20.0,
                Lng = 111.0,
                SkillCpr = false
            };

            var result = await store.UpdateAsync("rescuer-001", updated, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal("REDACTED(已更新)", result.Name);
            Assert.Equal("REDACTED", result.Phone);
            Assert.Equal("fucheng", result.Campus);
            Assert.Equal("official", result.Level);
            Assert.Equal("inactive", result.QualificationStatus);
            Assert.Equal(20.0, result.Lat);
            Assert.Equal(111.0, result.Lng);
            Assert.False(result.SkillCpr);
            Assert.Equal("女", result.Gender);

            var persisted = await store.FindByIdAsync("rescuer-001", CancellationToken.None);
            Assert.NotNull(persisted);
            Assert.Equal("REDACTED(已更新)", persisted.Name);
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
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task GetAllAsync_SortsById()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            var profiles = await store.GetAllAsync(CancellationToken.None);
            var ids = profiles.Select(p => p.Id).ToArray();

            Assert.Equal("rescuer-001", ids[0]);
            Assert.Equal("rescuer-031", ids[^1]);
            for (var i = 1; i < ids.Length; i++)
                Assert.True(string.CompareOrdinal(ids[i - 1], ids[i]) < 0);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task SeedComputesCampusCorrectly()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            var profiles = await store.GetAllAsync(CancellationToken.None);
            var guilinyang = profiles.Count(p => p.Campus == "guilinyang");
            var fucheng = profiles.Count(p => p.Campus == "fucheng");

            Assert.True(guilinyang > 0);
            Assert.True(fucheng > 0);
            Assert.Equal(31, guilinyang + fucheng);

            var fuchengProfile = profiles.First(p => p.Name == "REDACTED");
            Assert.Equal("fucheng", fuchengProfile.Campus);
            Assert.Contains("府城", fuchengProfile.TrainingSessionName);

            var guilinyangProfile = profiles.First(p => p.Name == "REDACTED");
            Assert.Equal("guilinyang", guilinyangProfile.Campus);
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
        var store = CreateStore(path);

        try
        {
            var updated = new ResponderProfileRecord
            {
                Id = "rescuer-001",
                Name = "REDACTED",
                AvatarUrl = "",
                Phone = "",
                Campus = "guilinyang",
                Level = "trainee",
                QualificationStatus = "active",
                Lat = 0,
                Lng = 0,
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
        var store = CreateStore(path);

        try
        {
            var updated = new ResponderProfileRecord
            {
                Id = "rescuer-001",
                Name = "REDACTED",
                AvatarUrl = "",
                Phone = "",
                Campus = "guilinyang",
                Level = "trainee",
                QualificationStatus = "active",
                Lat = 0,
                Lng = 0,
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

    private static JsonResponderStore CreateStore(string path)
    {
        return new JsonResponderStore(Options.Create(new WeChatOptions
        {
            RescuerProfilesFilePath = path
        }));
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
