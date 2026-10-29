using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Rescue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Persistence;

public static class RuntimeDataInitializer
{
    public static async Task EnsureReadyAsync(IServiceProvider services, CancellationToken ct)
    {
        var options = services.GetRequiredService<IOptions<DataOptions>>().Value;
        if (options.UseSqlite())
        {
            if (!File.Exists(options.SqlitePath))
                throw new InvalidOperationException("SQLite 数据库不存在，请先执行 migrate-json-to-sqlite 显式迁移命令。");
            var factory = services.GetRequiredService<IDbContextFactory<AedDbContext>>();
            await SqliteStoreHelpers.EnsureReadyAsync(factory, ct);
            return;
        }

        var helpStore = services.GetRequiredService<IHelpRequestStore>();
        var sessionStore = services.GetRequiredService<IRescueSessionStore>();
        var closedHelpIds = (await helpStore.ListAllAsync(ct))
            .Where(help => HelpStatuses.IsClosed(help.Status))
            .Select(help => help.Id);
        if (sessionStore is not JsonRescueSessionStore jsonSessionStore)
            throw new InvalidOperationException("JSON provider 未解析到 JSON 急救会话存储");
        await jsonSessionStore.BackfillClosedSessionsAsync(closedHelpIds.ToArray(), ct);
    }
}
