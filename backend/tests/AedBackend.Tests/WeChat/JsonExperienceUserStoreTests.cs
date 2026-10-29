using System.Text.Json;
using AedBackend.Core.Models.WeChat;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Tests.WeChat;

public sealed class JsonExperienceUserStoreTests
{
    [Fact]
    public async Task FindByTokenHashAsync_ReturnsNull_WhenTokenExpired()
    {
        var path = CreateTempFilePath();
        await WriteUserFileAsync(path, "old-hash", DateTimeOffset.UtcNow.AddDays(-31));
        var store = CreateStore(path);

        try
        {
            var user = await store.FindByTokenHashAsync("old-hash", CancellationToken.None);

            Assert.Null(user);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task RefreshTokenAsync_ReplacesValidTokenHash()
    {
        var path = CreateTempFilePath();
        var store = CreateStore(path);

        try
        {
            await store.SaveLoginAsync(
                new WeChatLoginSession("openid-valid", null),
                "old-hash",
                CancellationToken.None);

            var result = await store.RefreshTokenAsync(
                "old-hash",
                "new-hash",
                CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("openid-valid", result.User.OpenId);
            Assert.Null(await store.FindByTokenHashAsync("old-hash", CancellationToken.None));
            Assert.NotNull(await store.FindByTokenHashAsync("new-hash", CancellationToken.None));
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNullAndKeepsHash_WhenTokenExpired()
    {
        var path = CreateTempFilePath();
        await WriteUserFileAsync(path, "expired-hash", DateTimeOffset.UtcNow.AddDays(-31));
        var store = CreateStore(path);

        try
        {
            var result = await store.RefreshTokenAsync(
                "expired-hash",
                "new-hash",
                CancellationToken.None);
            var users = await store.ListUsersAsync(CancellationToken.None);

            Assert.Null(result);
            Assert.Single(users);
            Assert.Equal("expired-hash", users[0].SessionTokenHash);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    private static JsonExperienceUserStore CreateStore(string path)
    {
        return new JsonExperienceUserStore(Options.Create(new WeChatOptions
        {
            ExperienceUsersFilePath = path
        }));
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"aed-users-{Guid.NewGuid():N}",
            "wechat-experience-users.json");
    }

    private static void DeleteTempDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private static async Task WriteUserFileAsync(
        string path,
        string tokenHash,
        DateTimeOffset lastLoginAt)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = new
        {
            users = new[]
            {
                new
                {
                    openId = "openid-expired",
                    unionId = (string?)null,
                    phone = (string?)null,
                    sessionTokenHash = tokenHash,
                    createdAt = lastLoginAt.AddDays(-1),
                    lastLoginAt,
                    subscriptions = new Dictionary<string, object>()
                }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await File.WriteAllTextAsync(path, json);
    }
}
