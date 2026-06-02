using AedBackend.Infrastructure.Persistence.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Persistence;

public static class RuntimeDataInitializer
{
    public static async Task EnsureReadyAsync(IServiceProvider services, CancellationToken ct)
    {
        var options = services.GetRequiredService<IOptions<DataOptions>>().Value;
        if (!options.UseSqlite()) return;
        if (!File.Exists(options.SqlitePath))
            throw new InvalidOperationException("SQLite 数据库不存在，请先执行 migrate-json-to-sqlite 显式迁移命令。");
        var factory = services.GetRequiredService<IDbContextFactory<AedDbContext>>();
        await SqliteStoreHelpers.EnsureReadyAsync(factory, ct);
    }
}
