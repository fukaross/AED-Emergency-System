using AedBackend.Core.Models.Volunteer;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Volunteer;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Admin;
using AedBackend.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Tests.User;

public sealed class UserStoreTests
{
    [Fact]
    public async Task UpdateProfileAsync_OnlyUpdatesPresentNonNullFields()
    {
        using var fixture = await SqliteStoreFixture.CreateAsync();
        var users = fixture.GetRequiredService<IExperienceUserStore>();

        await users.UpdateProfileAsync(
            "openid-store",
            new UpdateExperienceUserProfilePatch(
                PatchValue<string?>.Present("张三"),
                PatchValue<string?>.Present("/uploads/avatar/a.jpg"),
                PatchValue<string?>.Present("female"),
                PatchValue<string?>.Present("REDACTED")),
            CancellationToken.None);
        await users.UpdateProfileAsync(
            "openid-store",
            new UpdateExperienceUserProfilePatch(
                PatchValue<string?>.Missing,
                PatchValue<string?>.Present(null),
                PatchValue<string?>.Missing,
                PatchValue<string?>.Missing),
            CancellationToken.None);

        var current = (await users.ListUsersAsync(CancellationToken.None)).Single();
        Assert.Equal("张三", current.Name);
        Assert.Null(current.AvatarUrl);
        Assert.Equal("female", current.Gender);
        Assert.Equal("REDACTED", current.Phone);
    }

    [Fact]
    public async Task VolunteerApplicationStore_SubmitsAndListsByUserDescending()
    {
        using var fixture = await SqliteStoreFixture.CreateAsync();
        var store = fixture.GetRequiredService<IVolunteerApplicationStore>();

        var first = await store.SubmitAsync("openid-store", CreatePayload("first"), null, CancellationToken.None);
        await Task.Delay(10);
        var second = await store.SubmitAsync("openid-store", CreatePayload("second"), first.Id, CancellationToken.None);
        await store.SubmitAsync("another-user", CreatePayload("hidden"), null, CancellationToken.None);

        var items = await store.ListByUserAsync(
            "openid-store",
            new VolunteerApplicationQuery(),
            CancellationToken.None);
        var found = await store.GetByIdAsync(first.Id, CancellationToken.None);

        Assert.Equal(2, items.Total);
        Assert.Equal(1, items.Page);
        Assert.Equal(20, items.PageSize);
        Assert.Equal(1, items.TotalPages);
        Assert.Equal(second.Id, items.Items[0].Id);
        Assert.Equal(2, items.Items[0].AttemptNumber);
        Assert.Equal(1, items.Items[1].AttemptNumber);
        Assert.Equal("first", found?.Payload.JoinReason);
    }

    [Fact]
    public async Task VolunteerApplicationStore_PaginatesByUser()
    {
        using var fixture = await SqliteStoreFixture.CreateAsync();
        var store = fixture.GetRequiredService<IVolunteerApplicationStore>();

        await store.SubmitAsync("openid-store", CreatePayload("old"), null, CancellationToken.None);
        await Task.Delay(10);
        var latest = await store.SubmitAsync("openid-store", CreatePayload("latest"), null, CancellationToken.None);

        var firstPage = await store.ListByUserAsync(
            "openid-store",
            new VolunteerApplicationQuery(PageSize: 1),
            CancellationToken.None);

        Assert.Equal(2, firstPage.Total);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Single(firstPage.Items);
        Assert.Equal(latest.Id, firstPage.Items[0].Id);
    }

    private static VolunteerApplicationPayloadRecord CreatePayload(string reason)
    {
        var image = new VolunteerImageAttachmentRecord("/uploads/volunteer/a.jpg", null, "a.jpg");
        return new VolunteerApplicationPayloadRecord
        {
            IdPhoto = image,
            Name = "王小明",
            Gender = "male",
            BirthDate = "2000-01-01",
            Phone = "REDACTED",
            Email = "user@example.com",
            PoliticalStatus = "群众",
            Education = "本科",
            Occupation = "学生",
            ProfessionalTitle = "无",
            EmergencyContactName = "李老师",
            EmergencyContactPhone = "REDACTED",
            QualificationImages = [image],
            VolunteerCertificateImages = [image],
            IdNumber = "REDACTED",
            IdCardFront = image,
            IdCardBack = image,
            HomeAddress = "海南省海口市",
            PersonalResume = "参加过校园急救培训",
            JoinReason = reason,
            CommitmentAccepted = true
        };
    }

    private sealed class SqliteStoreFixture : IDisposable
    {
        private readonly ProviderTempData temp;
        private readonly AdminTestApplication app;

        private SqliteStoreFixture(ProviderTempData temp, AdminTestApplication app)
        {
            this.temp = temp;
            this.app = app;
            Client = app.CreateClient();
        }

        private HttpClient Client { get; }

        public static async Task<SqliteStoreFixture> CreateAsync()
        {
            var temp = new ProviderTempData();
            await temp.WriteMinimalJsonAsync();
            await temp.WriteUserJsonAsync("openid-store", "REDACTED", "store-token");
            await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
            var app = new AdminTestApplication(temp.Root, new Dictionary<string, string?>
            {
                ["Data:Provider"] = "Sqlite",
                ["Data:SqlitePath"] = temp.DbPath
            });
            return new SqliteStoreFixture(temp, app);
        }

        public T GetRequiredService<T>() where T : notnull
        {
            return app.Services.GetRequiredService<T>();
        }

        public void Dispose()
        {
            Client.Dispose();
            app.Dispose();
            temp.Dispose();
        }
    }
}
