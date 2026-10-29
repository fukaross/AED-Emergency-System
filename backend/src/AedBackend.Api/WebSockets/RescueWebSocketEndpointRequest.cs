using AedBackend.Api.Endpoints;
using Microsoft.AspNetCore.Mvc;

namespace AedBackend.Api.WebSockets;

/** WebSocket 握手的框架绑定参数。 */
internal sealed class RescueWebSocketEndpointRequest
{
    public HttpContext HttpContext { get; init; } = default!;
    public string Id { get; init; } = string.Empty;

    [FromServices]
    public RescueSessionAuthorizer Authorizer { get; init; } = default!;

    [FromServices]
    public RescueSessionBroadcaster Broadcaster { get; init; } = default!;

    [FromServices]
    public RescueWebSocketReceivePump ReceivePump { get; init; } = default!;
}
