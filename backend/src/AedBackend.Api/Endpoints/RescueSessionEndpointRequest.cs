using AedBackend.Api.WebSockets;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Rescue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

/** Minimal API 参数包；集中框架绑定，业务处理器只接收一个上下文。 */
internal sealed class RescueSessionEndpointRequest
{
    public string? Id { get; init; }
    public HttpRequest HttpRequest { get; init; } = default!;
    public CancellationToken CancellationToken { get; init; }

    [FromServices]
    public IOptions<RescueOptions> RescueOptions { get; init; } = default!;

    [FromServices]
    public RescueSessionAuthorizer Authorizer { get; init; } = default!;

    [FromServices]
    public IHelpRequestStore HelpStore { get; init; } = default!;

    [FromServices]
    public IRescueSessionStore SessionStore { get; init; } = default!;

    [FromServices]
    public RescueSessionLifecycleCoordinator Lifecycle { get; init; } = default!;

    [FromServices]
    public RescueSessionBroadcaster Broadcaster { get; init; } = default!;

    public SessionEndpointServices ToServices()
    {
        return new SessionEndpointServices
        {
            Authorizer = Authorizer,
            HelpStore = HelpStore,
            SessionStore = SessionStore,
            Lifecycle = Lifecycle
        };
    }
}
