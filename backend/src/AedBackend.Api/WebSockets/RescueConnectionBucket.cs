namespace AedBackend.Api.WebSockets;

/** 单个 helpId 的连接桶；退休桶不会再接受连接，避免空桶删除与并发注册竞态。 */
internal sealed class ConnectionBucket
{
    private readonly Dictionary<Guid, RescueSocketRegistration> entries = [];
    private readonly object gate = new();
    private bool retired;

    public bool IsRetired
    {
        get { lock (gate) return retired; }
    }

    public bool TryAdd(RescueSocketRegistration registration)
    {
        lock (gate)
        {
            if (retired) return false;
            entries[registration.ConnectionId] = registration;
            return true;
        }
    }

    public RescueSocketRegistration[] Snapshot()
    {
        lock (gate) return entries.Values.ToArray();
    }

    public bool TryRemove(Guid connectionId)
    {
        lock (gate)
        {
            if (!entries.Remove(connectionId) || entries.Count != 0) return false;
            retired = true;
            return true;
        }
    }
}
