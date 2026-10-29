using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AedBackend.Api.Endpoints;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Help;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Help;
using AedBackend.Infrastructure.Help;
using AedBackend.Infrastructure.WeChat;
using AedBackend.Tests.Admin;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Tests.Rescue;

public sealed class HelpPublishConcurrencyApiTests : AdminApiTestBase
{
    private const string SubscribeTemplateId = "test-template";
    private const string RequesterToken = "token-requester";

    protected override Action<IServiceCollection>? ConfigureServices => services =>
    {
        services.Configure<WeChatOptions>(options => options.SubscribeTemplateId = SubscribeTemplateId);
        services.AddSingleton<IHelpRequestStore>(provider =>
            new PublishBarrierHelpStore(provider.GetRequiredService<JsonHelpRequestStore>()));
    };

    [Fact]
    public async Task Publish_ConcurrentDuplicate_AllowsOnlyOneActiveRequest()
    {
        SeedLoggedInExperienceUsers(
            SubscribeTemplateId,
            ("openid-1", RequesterToken));

        using var firstRequest = CreatePublishRequest();
        using var secondRequest = CreatePublishRequest();
        var responses = await Task.WhenAll(
            Client.SendAsync(firstRequest),
            Client.SendAsync(secondRequest));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var conflict = Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Conflict);
        var failure = await conflict.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.Equal(ErrorCodes.DuplicateHelpRequest, Assert.IsType<ApiResponse>(failure).Code);
        var store = Services.GetRequiredService<IHelpRequestStore>();
        Assert.Single(await store.ListAllAsync(CancellationToken.None));
    }

    [Fact]
    public void CreateRecord_BurstWithinSameMillisecond_UsesUniqueRequestNumbers()
    {
        var request = CreatePublishBody();
        var user = new ExperienceUserRecord(
            "openid-1",
            null,
            "13800138000",
            null,
            null,
            null,
            "token-hash",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            new Dictionary<string, SubscriptionRecord>());

        var requestNumbers = Enumerable.Range(0, 1_000)
            .Select(_ => HelpPublishFlow.CreateRecord(request, user).RequestNo)
            .ToArray();

        Assert.Equal(requestNumbers.Length, requestNumbers.Distinct().Count());
    }

    private static HttpRequestMessage CreatePublishRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/help/request")
        {
            Content = JsonContent.Create(CreatePublishBody())
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", RequesterToken);
        return request;
    }

    private static PublishHelpRequest CreatePublishBody() => new(
        19.974,
        110.517,
        "教学楼A一层大厅",
        "有人晕倒需要协助",
        "help_hand",
        null,
        "user",
        null,
        null,
        null);

    private sealed class PublishBarrierHelpStore(JsonHelpRequestStore inner) : IHelpRequestStore
    {
        private readonly TaskCompletionSource bothChecksReached =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int activeChecks;

        public async Task<bool> TrySaveIfNoActiveAsync(
            HelpRequestRecord record,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref activeChecks) == 2) bothChecksReached.TrySetResult();
            await bothChecksReached.Task.WaitAsync(cancellationToken);
            return await inner.TrySaveIfNoActiveAsync(record, cancellationToken);
        }

        public Task<HelpRequestRecord?> FindByIdOrRequestNoAsync(
            string idOrRequestNo,
            CancellationToken cancellationToken) =>
            inner.FindByIdOrRequestNoAsync(idOrRequestNo, cancellationToken);

        public Task<IReadOnlyList<HelpRequestRecord>> ListByUserAsync(
            string userId,
            CancellationToken cancellationToken) =>
            inner.ListByUserAsync(userId, cancellationToken);

        public Task<IReadOnlyList<HelpRequestRecord>> ListAllAsync(
            CancellationToken cancellationToken) =>
            inner.ListAllAsync(cancellationToken);

        public Task<HelpRequestOverviewPage> ListOverviewsAsync(
            HelpRequestOverviewQuery query,
            CancellationToken cancellationToken) =>
            inner.ListOverviewsAsync(query, cancellationToken);

        public Task<HelpRequestRecord> AddReportAsync(
            HelpReportUpdate update,
            CancellationToken cancellationToken) =>
            inner.AddReportAsync(update, cancellationToken);

        public Task<HelpRequestRecord> CloseAsync(
            HelpClosureUpdate update,
            CancellationToken cancellationToken) =>
            inner.CloseAsync(update, cancellationToken);

        public Task<HelpRequestRecord> TransitionToRespondingAsync(
            string id,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            inner.TransitionToRespondingAsync(id, now, cancellationToken);
    }
}
