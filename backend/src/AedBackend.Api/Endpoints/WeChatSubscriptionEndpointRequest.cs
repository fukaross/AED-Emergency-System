using AedBackend.Core.DTOs.WeChat;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.WeChat;
using Microsoft.AspNetCore.Mvc;

namespace AedBackend.Api.Endpoints;

/** Minimal API binding for a subscription decision and its active-user dependencies. */
internal sealed class WeChatSubscriptionEndpointRequest
{
    public HttpRequest HttpRequest { get; init; } = default!;
    public CancellationToken CancellationToken { get; init; }

    [FromBody]
    public SaveSubscribeDecisionRequest Body { get; init; } = default!;

    [FromServices]
    public ISessionTokenService TokenService { get; init; } = default!;

    [FromServices]
    public IExperienceUserStore UserStore { get; init; } = default!;

    [FromServices]
    public IUserAccountStatusService AccountStatus { get; init; } = default!;

    public ActiveUserDependencies ActiveUserDependencies => new(
        TokenService,
        UserStore,
        AccountStatus);
}
