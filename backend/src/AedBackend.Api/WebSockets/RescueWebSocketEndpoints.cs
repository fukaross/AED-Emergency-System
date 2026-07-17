using System.Net.WebSockets;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Api.WebSockets;

/**
 * 急救会话 WS 通道（ADR 0001）：握手校验 token + 会话参与者身份；
 * 通道仅接收服务端扇出，客户端消息一律忽略，写操作只走 HTTP。
 */
public static class RescueWebSocketEndpoints
{
    private const int ReceiveBufferSize = 1024;

    public static IEndpointRouteBuilder MapRescueWebSocketEndpoints(this IEndpointRouteBuilder app)
    {
        app.Map("/ws/help/{id}/session", HandleAsync);
        return app;
    }

    private static async Task HandleAsync(
        HttpContext context,
        string id,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
        RescueSessionBroadcaster broadcaster)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var token = context.Request.Query["token"].ToString();
        var user = string.IsNullOrWhiteSpace(token)
            ? null
            : await userStore.FindByTokenHashAsync(tokenService.HashToken(token), context.RequestAborted);
        if (user is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (await accountStatus.IsDisabledAsync(user.OpenId, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var help = await helpStore.FindByIdOrRequestNoAsync(id, context.RequestAborted);
        if (help is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var session = await sessionStore.FindByHelpIdAsync(help.Id, context.RequestAborted);
        var isParticipant = session?.Participants.Any(p => p.OpenId == user.OpenId) == true;
        if (!isParticipant || HelpStatuses.IsClosed(help.Status))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid();
        broadcaster.Register(help.Id, connectionId, socket);
        try
        {
            var buffer = new byte[ReceiveBufferSize];
            while (!context.RequestAborted.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, context.RequestAborted);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 客户端断开连接
        }
        catch (WebSocketException)
        {
            // 连接异常断开，按注销处理
        }
        finally
        {
            broadcaster.Unregister(help.Id, connectionId);
        }
    }
}
