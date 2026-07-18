namespace AedBackend.Api.WebSockets;

/** 反向代理空闲超时契约：服务端每 30 秒保持连接活跃。 */
internal static class RescueWebSocketRuntimeOptions
{
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(30);

    public static WebSocketOptions Create()
    {
        return new WebSocketOptions
        {
            KeepAliveInterval = KeepAliveInterval,
        };
    }
}
