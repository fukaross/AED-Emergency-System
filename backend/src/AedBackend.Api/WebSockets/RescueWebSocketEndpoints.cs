using AedBackend.Api.Endpoints;
using Microsoft.AspNetCore.Mvc;

namespace AedBackend.Api.WebSockets;

/** ADR 0001 的只读 WS 入口；HTTP 是唯一写真源。 */
public static class RescueWebSocketEndpoints
{
    public static IEndpointRouteBuilder MapRescueWebSocketEndpoints(this IEndpointRouteBuilder app)
    {
        app.Map("/ws/help/{id}/session", HandleAsync);
        return app;
    }

    /** 握手只负责编排；连接生命周期由 registration + receive pump 收口。 */
    private static async Task HandleAsync(
        [AsParameters] RescueWebSocketEndpointRequest endpoint)
    {
        var context = endpoint.HttpContext;
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var access = await endpoint.Authorizer.RequireParticipantAsync(
            context.Request,
            endpoint.Id,
            context.RequestAborted);
        if (access.Failure is not null)
        {
            context.Response.StatusCode = access.FailureStatusCode;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var identity = new RescueSocketIdentity(access.User!.OpenId, access.TokenHash!);
        var registration = endpoint.Broadcaster.Register(access.Help!.Id, identity, socket);
        if (registration is null) return;
        try
        {
            await endpoint.ReceivePump.RunAsync(registration, context.RequestAborted);
        }
        finally
        {
            endpoint.Broadcaster.Unregister(registration);
        }
    }
}
