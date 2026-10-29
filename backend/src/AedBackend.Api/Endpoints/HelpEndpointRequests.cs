using AedBackend.Api.WebSockets;
using AedBackend.Core.DTOs.Help;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Demo;
using AedBackend.Infrastructure.Upload;
using AedBackend.Infrastructure.WeChat;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

internal interface IUserHelpEndpointRequest
{
    HttpRequest HttpRequest { get; }
    ISessionTokenService TokenService { get; }
    IExperienceUserStore UserStore { get; }
    IUserAccountStatusService AccountStatus { get; }
    CancellationToken CancellationToken { get; }
}

internal sealed class PublishHelpEndpointRequest
{
    public HttpRequest HttpRequest { get; init; } = default!;
    public CancellationToken CancellationToken { get; init; }

    [FromBody]
    public PublishHelpRequest Body { get; init; } = default!;

    [FromServices]
    public IOptions<WeChatOptions> Options { get; init; } = default!;

    [FromServices]
    public ISessionTokenService TokenService { get; init; } = default!;

    [FromServices]
    public IExperienceUserStore UserStore { get; init; } = default!;

    [FromServices]
    public IUserAccountStatusService AccountStatus { get; init; } = default!;

    [FromServices]
    public IHelpRequestStore HelpStore { get; init; } = default!;

    [FromServices]
    public IRescueSessionStore SessionStore { get; init; } = default!;

    [FromServices]
    public RescueSessionLifecycleCoordinator Lifecycle { get; init; } = default!;

    [FromServices]
    public IResponderStore ResponderStore { get; init; } = default!;

    [FromServices]
    public IWeChatSubscribeMessageSender Sender { get; init; } = default!;

    [FromServices]
    public IOptions<DemoOptions> DemoOptions { get; init; } = default!;
}

internal sealed class ListHelpEndpointRequest : IUserHelpEndpointRequest
{
    public HttpRequest HttpRequest { get; init; } = default!;
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public string? Status { get; init; }
    public CancellationToken CancellationToken { get; init; }

    [FromServices]
    public ISessionTokenService TokenService { get; init; } = default!;

    [FromServices]
    public IExperienceUserStore UserStore { get; init; } = default!;

    [FromServices]
    public IUserAccountStatusService AccountStatus { get; init; } = default!;

    [FromServices]
    public IHelpRequestStore HelpStore { get; init; } = default!;
}

internal sealed class GetHelpEndpointRequest : IUserHelpEndpointRequest
{
    public HttpRequest HttpRequest { get; init; } = default!;
    public string Id { get; init; } = string.Empty;
    public CancellationToken CancellationToken { get; init; }

    [FromServices]
    public ISessionTokenService TokenService { get; init; } = default!;

    [FromServices]
    public IExperienceUserStore UserStore { get; init; } = default!;

    [FromServices]
    public IUserAccountStatusService AccountStatus { get; init; } = default!;

    [FromServices]
    public IResponderStore ResponderStore { get; init; } = default!;

    [FromServices]
    public IHelpRequestStore HelpStore { get; init; } = default!;
}

internal sealed class SubmitHelpReportEndpointRequest
{
    public HttpRequest HttpRequest { get; init; } = default!;
    public string Id { get; init; } = string.Empty;
    public CancellationToken CancellationToken { get; init; }

    [FromBody]
    public HelpReportRequest Body { get; init; } = default!;

    [FromServices]
    public IOptions<WeChatOptions> Options { get; init; } = default!;

    [FromServices]
    public IOptions<UploadOptions> UploadOptions { get; init; } = default!;

    [FromServices]
    public ISessionTokenService TokenService { get; init; } = default!;

    [FromServices]
    public IExperienceUserStore UserStore { get; init; } = default!;

    [FromServices]
    public IUserAccountStatusService AccountStatus { get; init; } = default!;

    [FromServices]
    public IResponderStore ResponderStore { get; init; } = default!;

    [FromServices]
    public IHelpRequestStore HelpStore { get; init; } = default!;

    [FromServices]
    public RescueSessionLifecycleCoordinator Lifecycle { get; init; } = default!;

    [FromServices]
    public RescueSessionBroadcaster Broadcaster { get; init; } = default!;

    [FromServices]
    public HelpClosureApplication Closure { get; init; } = default!;
}

internal sealed class CloseHelpEndpointRequest : IUserHelpEndpointRequest
{
    public HttpRequest HttpRequest { get; init; } = default!;
    public string Id { get; init; } = string.Empty;
    public CancellationToken CancellationToken { get; init; }

    [FromBody]
    public CloseHelpRequest Body { get; init; } = default!;

    [FromServices]
    public ISessionTokenService TokenService { get; init; } = default!;

    [FromServices]
    public IExperienceUserStore UserStore { get; init; } = default!;

    [FromServices]
    public IUserAccountStatusService AccountStatus { get; init; } = default!;

    [FromServices]
    public IHelpRequestStore HelpStore { get; init; } = default!;

    [FromServices]
    public HelpClosureApplication Closure { get; init; } = default!;
}

internal sealed class GuestCloseHelpEndpointRequest
{
    public CancellationToken CancellationToken { get; init; }

    [FromBody]
    public GuestCloseHelpRequest Body { get; init; } = default!;

    [FromServices]
    public IHelpRequestStore HelpStore { get; init; } = default!;

    [FromServices]
    public HelpClosureApplication Closure { get; init; } = default!;
}
