using System.Collections.Concurrent;

namespace AedBackend.Api.Endpoints;

/** 按求助 ID 串行化会话写入与关单，避免跨 Store 的关闭竞态窗口。 */
public sealed class RescueSessionLifecycleCoordinator
{
    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);

    /** 获取指定求助的生命周期租约；最后一个使用者释放后同步回收键。 */
    public async Task<IDisposable> EnterAsync(string helpId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helpId);
        while (true)
        {
            var entry = entries.GetOrAdd(helpId, static _ => new Entry());
            if (!entry.TryAddUser()) continue;
            try
            {
                await entry.Gate.WaitAsync(cancellationToken);
                return new Lease(this, helpId, entry);
            }
            catch
            {
                Release(helpId, entry, releaseGate: false);
                throw;
            }
        }
    }

    internal int GetUsageCount(string helpId)
    {
        return entries.TryGetValue(helpId, out var entry) ? entry.GetUsers() : 0;
    }

    private void Release(string helpId, Entry entry, bool releaseGate)
    {
        if (releaseGate) entry.Gate.Release();
        if (!entry.RemoveUser()) return;
        entries.TryRemove(new KeyValuePair<string, Entry>(helpId, entry));
        entry.Gate.Dispose();
    }

    private sealed class Entry
    {
        private readonly object sync = new();
        private int users;
        private bool removed;

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public bool TryAddUser()
        {
            lock (sync)
            {
                if (removed) return false;
                users++;
                return true;
            }
        }

        public bool RemoveUser()
        {
            lock (sync)
            {
                users--;
                if (users != 0) return false;
                removed = true;
                return true;
            }
        }

        public int GetUsers()
        {
            lock (sync) return users;
        }
    }

    private sealed class Lease(
        RescueSessionLifecycleCoordinator owner,
        string helpId,
        Entry entry) : IDisposable
    {
        private RescueSessionLifecycleCoordinator? currentOwner = owner;

        public void Dispose()
        {
            var target = Interlocked.Exchange(ref currentOwner, null);
            target?.Release(helpId, entry, releaseGate: true);
        }
    }
}
