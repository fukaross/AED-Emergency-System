using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Tests.Admin;
using AedBackend.Tests.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Tests.Help;

public sealed class GuestHelpSecurityApiTests
{
    private const int GuestCredentialMinimumEntropyBytes = 16;
    private const int GuestCredentialMaxLength = 32;
    private const string JsonProvider = "Json";
    private const string SqliteProvider = "Sqlite";
    private const string GuestPublisherToken = "token-guest-publisher";
    private const string ResponderToken = "token-responder";
    private const string SubscribeTemplateId = "test-template";
    private const string NormalHelpId = "help-01";
    private const string NormalRequestNo = "HELP001";

    [Theory]
    [InlineData(JsonProvider)]
    [InlineData(SqliteProvider)]
    public async Task InvalidCredentials_CannotReadOrCloseHelpOrMutateSession(string provider)
    {
        await using var fixture = await GuestFixture.CreateAsync(provider);
        var guest = await fixture.PublishGuestAsync();
        await fixture.JoinAndLocateNormalHelpAsync();

        foreach (var payload in InvalidQueryPayloads(guest))
        {
            using var response = await fixture.PostAnonymousJsonAsync("/api/help/guest/query", payload);
            await AssertNotFoundAsync(response);
        }
        foreach (var payload in InvalidClosePayloads(guest))
        {
            using var response = await fixture.PostAnonymousJsonAsync("/api/help/guest/close", payload);
            await AssertNotFoundAsync(response);
        }

        await fixture.AssertHelpAndSessionUnchangedAsync(guest);
    }

    [Theory]
    [InlineData(JsonProvider)]
    [InlineData(SqliteProvider)]
    public async Task ValidCredentials_AreStrongAndWorkOnlyWithRequestNo(string provider)
    {
        await using var fixture = await GuestFixture.CreateAsync(provider);
        var guest = await fixture.PublishGuestAsync();
        AssertCredentialEntropy(guest.FollowupCode, "Q");
        AssertCredentialEntropy(guest.CloseToken, "CLOSE");

        using var query = await fixture.PostAnonymousJsonAsync(
            "/api/help/guest/query",
            $$"""{"requestNo":"{{guest.RequestNo}}","followupCode":"{{guest.FollowupCode}}"}""");
        Assert.Equal(HttpStatusCode.OK, query.StatusCode);

        using var close = await fixture.PostAnonymousJsonAsync(
            "/api/help/guest/close",
            $$"""{"requestNo":"{{guest.RequestNo}}","closeToken":"{{guest.CloseToken}}"}""");
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        await fixture.AssertGuestClosedAsync(guest);
    }

    private static IReadOnlyList<string> InvalidQueryPayloads(GuestPublication guest)
    {
        return
        [
            $$"""{"requestNo":"{{NormalRequestNo}}","followupCode":null}""",
            $$"""{"requestNo":"{{NormalRequestNo}}"}""",
            $$"""{"requestNo":"{{NormalRequestNo}}","followupCode":""}""",
            $$"""{"requestNo":"{{NormalRequestNo}}","followupCode":"{{guest.FollowupCode}}"}""",
            $$"""{"requestNo":"{{guest.HelpId}}","followupCode":"{{guest.FollowupCode}}"}""",
            $$"""{"requestNo":"{{guest.RequestNo}}","followupCode":"wrong"}""",
            """{"requestNo":null,"followupCode":"wrong"}""",
            """{"requestNo":"","followupCode":"wrong"}""",
            """{"requestNo":"HELP-NOT-FOUND","followupCode":null}""",
        ];
    }

    private static IReadOnlyList<string> InvalidClosePayloads(GuestPublication guest)
    {
        return
        [
            $$"""{"requestNo":"{{NormalRequestNo}}","closeToken":null}""",
            $$"""{"requestNo":"{{NormalRequestNo}}"}""",
            $$"""{"requestNo":"{{NormalRequestNo}}","closeToken":""}""",
            $$"""{"requestNo":"{{NormalRequestNo}}","closeToken":"{{guest.CloseToken}}"}""",
            $$"""{"requestNo":"{{guest.HelpId}}","closeToken":"{{guest.CloseToken}}"}""",
            $$"""{"requestNo":"{{guest.RequestNo}}","closeToken":"wrong"}""",
            """{"requestNo":null,"closeToken":"wrong"}""",
            """{"requestNo":"","closeToken":"wrong"}""",
            """{"requestNo":"HELP-NOT-FOUND","closeToken":null}""",
        ];
    }

    private static async Task AssertNotFoundAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(ErrorCodes.ResourceNotFound, body.Code);
    }

    private static void AssertCredentialEntropy(string credential, string prefix)
    {
        Assert.StartsWith(prefix, credential, StringComparison.Ordinal);
        var randomBytes = WebEncoders.Base64UrlDecode(credential[prefix.Length..]);
        Assert.True(
            randomBytes.Length >= GuestCredentialMinimumEntropyBytes,
            "Guest credential must contain at least 128 random bits.");
        Assert.True(
            credential.Length <= GuestCredentialMaxLength,
            $"Guest credential must fit the {GuestCredentialMaxLength}-character storage contract.");
    }

    private sealed record GuestPublication(
        string HelpId,
        string RequestNo,
        string FollowupCode,
        string CloseToken);

    private sealed class GuestFixture : IAsyncDisposable
    {
        private readonly ProviderTempData temp;
        private readonly AdminTestApplication application;

        private GuestFixture(
            ProviderTempData temp,
            AdminTestApplication application,
            HttpClient client)
        {
            this.temp = temp;
            this.application = application;
            Client = client;
        }

        private HttpClient Client { get; }
        private IServiceProvider Services => application.Services;

        public static async Task<GuestFixture> CreateAsync(string provider)
        {
            var temp = new ProviderTempData();
            await temp.WriteValidDataAsync();
            await temp.WriteUsersJsonAsync(
                ("openid-1", "token-requester"),
                ("openid-2", ResponderToken),
                ("openid-3", GuestPublisherToken));
            await temp.WriteBoundResponderJsonAsync("openid-2");
            var configuration = await CreateProviderConfigurationAsync(provider, temp);
            var application = new AdminTestApplication(temp.Root, configuration);
            return new GuestFixture(temp, application, application.CreateClient());
        }

        public async Task<GuestPublication> PublishGuestAsync()
        {
            UseToken(GuestPublisherToken);
            using var response = await Client.PostAsJsonAsync("/api/help/request", new
            {
                lat = 19.974,
                lng = 110.517,
                address = "教学楼A一层大厅",
                content = "有人晕倒需要协助",
                type = "help_hand",
                requestChannel = "guest",
                guestName = "路人甲",
                callbackAllowed = false,
            });
            Assert.True(
                response.IsSuccessStatusCode,
                await response.Content.ReadAsStringAsync());
            var body = await ReadAsync(response);
            return new GuestPublication(
                body.Data.GetProperty("id").GetString()!,
                body.Data.GetProperty("requestNo").GetString()!,
                body.Data.GetProperty("followupCode").GetString()!,
                body.Data.GetProperty("closeToken").GetString()!);
        }

        public async Task JoinAndLocateNormalHelpAsync()
        {
            UseToken(ResponderToken);
            using var join = await Client.PostAsync($"/api/help/{NormalHelpId}/session/join", null);
            Assert.Equal(HttpStatusCode.OK, join.StatusCode);
            using var location = await Client.PutAsJsonAsync(
                $"/api/help/{NormalHelpId}/session/location",
                new { lat = 19.974, lng = 110.517 });
            Assert.Equal(HttpStatusCode.OK, location.StatusCode);
            Client.DefaultRequestHeaders.Authorization = null;
        }

        public async Task<HttpResponseMessage> PostAnonymousJsonAsync(string path, string json)
        {
            Client.DefaultRequestHeaders.Authorization = null;
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await Client.PostAsync(path, content);
        }

        public async Task AssertHelpAndSessionUnchangedAsync(GuestPublication guest)
        {
            var helpStore = Services.GetRequiredService<IHelpRequestStore>();
            var sessionStore = Services.GetRequiredService<IRescueSessionStore>();
            var normal = await helpStore.FindByIdOrRequestNoAsync(NormalHelpId, CancellationToken.None);
            var guestHelp = await helpStore.FindByIdOrRequestNoAsync(guest.HelpId, CancellationToken.None);
            Assert.Equal(HelpStatuses.Responding, Assert.IsType<HelpRequestRecord>(normal).Status);
            Assert.Equal(HelpStatuses.Published, Assert.IsType<HelpRequestRecord>(guestHelp).Status);

            var normalSession = await sessionStore.FindByHelpIdAsync(NormalHelpId, CancellationToken.None);
            var participant = Assert.Single(Assert.IsType<RescueSessionRecord>(normalSession).Participants);
            Assert.Null(normalSession.ClosedAt);
            Assert.Equal(19.974, participant.LastLat);
            Assert.Equal(110.517, participant.LastLng);

            var guestSession = await sessionStore.FindByHelpIdAsync(guest.HelpId, CancellationToken.None);
            var storedGuestSession = Assert.IsType<RescueSessionRecord>(guestSession);
            Assert.Null(storedGuestSession.ClosedAt);
            Assert.Empty(storedGuestSession.Participants);
        }

        public async Task AssertGuestClosedAsync(GuestPublication guest)
        {
            var helpStore = Services.GetRequiredService<IHelpRequestStore>();
            var sessionStore = Services.GetRequiredService<IRescueSessionStore>();
            var help = await helpStore.FindByIdOrRequestNoAsync(guest.HelpId, CancellationToken.None);
            var session = await sessionStore.FindByHelpIdAsync(guest.HelpId, CancellationToken.None);
            Assert.Equal(HelpStatuses.FalseAlarm, Assert.IsType<HelpRequestRecord>(help).Status);
            Assert.NotNull(Assert.IsType<RescueSessionRecord>(session).ClosedAt);
        }

        public ValueTask DisposeAsync()
        {
            Client.Dispose();
            application.Dispose();
            temp.Dispose();
            return ValueTask.CompletedTask;
        }

        private void UseToken(string token)
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private static async Task<IReadOnlyDictionary<string, string?>> CreateProviderConfigurationAsync(
            string provider,
            ProviderTempData temp)
        {
            var configuration = new Dictionary<string, string?>
            {
                ["WeChat:SubscribeTemplateId"] = SubscribeTemplateId,
            };
            if (provider == JsonProvider) return configuration;
            if (provider != SqliteProvider) throw new ArgumentOutOfRangeException(nameof(provider));
            await new JsonToSqliteMigrator().RunAsync(temp.Root, temp.DbPath, CancellationToken.None);
            configuration["Data:Provider"] = SqliteProvider;
            configuration["Data:SqlitePath"] = temp.DbPath;
            return configuration;
        }

        private static async Task<ApiResponse<JsonElement>> ReadAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
            return body ?? throw new InvalidOperationException("Response body was empty.");
        }
    }
}
