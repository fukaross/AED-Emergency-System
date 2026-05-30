using System.Text.Json;
using AedBackend.Core.Models.Course;
using AedBackend.Core.Services.Course;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Course;

public sealed class JsonQuickActionStore : IQuickActionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public JsonQuickActionStore(IOptions<WeChatOptions> options)
    {
        filePath = Path.GetFullPath(options.Value.QuickActionsFilePath);
    }

    public async Task<IReadOnlyList<QuickActionRecord>> GetAllAsync(CancellationToken ct)
    {
        var actions = await LoadActionsAsync(ct);
        return actions
            .Where(a => a.IsEnabled)
            .OrderBy(a => a.SortOrder)
            .ToArray();
    }

    public async Task<IReadOnlyList<QuickActionRecord>> GetAllAdminAsync(CancellationToken ct)
    {
        var actions = await LoadActionsAsync(ct);
        return actions
            .OrderBy(a => a.SortOrder)
            .ToArray();
    }

    public async Task<QuickActionRecord?> FindByIdAsync(string id, CancellationToken ct)
    {
        var actions = await LoadActionsAsync(ct);
        return actions.FirstOrDefault(a =>
            a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<QuickActionRecord> AddAsync(QuickActionRecord action, CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            var actions = await ReadUnsafeAsync(ct);
            action.Id = NextId(actions);
            actions.Add(action);
            await WriteUnsafeAsync(actions, ct);
            return action;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<QuickActionRecord?> UpdateAsync(string id, QuickActionRecord updated, CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            var actions = await ReadUnsafeAsync(ct);
            var index = actions.FindIndex(a =>
                a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return null;

            var existing = actions[index];
            ApplyUpdate(existing, updated);
            await WriteUnsafeAsync(actions, ct);
            return existing;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            var actions = await ReadUnsafeAsync(ct);
            var removed = actions.RemoveAll(a =>
                a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return false;
            await WriteUnsafeAsync(actions, ct);
            return true;
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<List<QuickActionRecord>> LoadActionsAsync(CancellationToken ct)
    {
        await fileLock.WaitAsync(ct);
        try
        {
            return await ReadUnsafeAsync(ct);
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<List<QuickActionRecord>> ReadUnsafeAsync(CancellationToken ct)
    {
        if (!File.Exists(filePath))
        {
            var seed = DefaultActions();
            await WriteUnsafeAsync(seed, ct);
            return seed;
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<QuickActionRecord>>(stream, JsonOptions, ct)
               ?? DefaultActions();
    }

    private async Task WriteUnsafeAsync(List<QuickActionRecord> actions, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, actions, JsonOptions, ct);
    }

    private static string NextId(List<QuickActionRecord> actions)
    {
        var max = 0;
        foreach (var a in actions)
        {
            if (a.Id.StartsWith("qa-") && int.TryParse(a.Id[3..], out var num))
                max = Math.Max(max, num);
        }

        return $"qa-{max + 1:D2}";
    }

    private static void ApplyUpdate(QuickActionRecord target, QuickActionRecord source)
    {
        target.Title = source.Title;
        target.Icon = source.Icon;
        target.Summary = source.Summary;
        target.SortOrder = source.SortOrder;
        target.IsEnabled = source.IsEnabled;
        target.Steps = source.Steps;
    }

    internal static List<QuickActionRecord> DefaultActions()
    {
        return new List<QuickActionRecord>
        {
            new()
            {
                Id = "qa-01",
                Title = "心肺复苏",
                Icon = "/static/images/icon-cpr.png",
                Summary = "心肺复苏关键步骤与节奏提示",
                SortOrder = 1,
                IsEnabled = true,
                Steps = new List<QuickActionStepRecord>
                {
                    new()
                    {
                        Order = 1,
                        Title = "判断意识",
                        Description = "轻拍双肩，大声呼唤，确认患者有无反应。",
                        Image = "/static/images/course-thumb-cpr.jpg",
                        KeyPoints = new List<string> { "轻拍重唤", "观察胸部起伏" }
                    },
                    new()
                    {
                        Order = 2,
                        Title = "呼叫帮助",
                        Description = "指定周围人员拨打120并取来AED。",
                        Image = "/static/images/course-thumb-aed.jpg",
                        KeyPoints = new List<string> { "指定具体人拨打120", "尽快取得AED" }
                    },
                    new()
                    {
                        Order = 3,
                        Title = "胸外按压",
                        Description = "双手交叠，掌根置于胸骨下半部，垂直向下按压。",
                        Image = "/static/images/course-thumb-cpr.jpg",
                        KeyPoints = new List<string> { "频率 100-120 次/分钟", "深度 5-6 cm", "充分回弹" }
                    }
                }
            },
            new()
            {
                Id = "qa-02",
                Title = "海姆立克急救法",
                Icon = "/static/images/icon-heimlich.jpg",
                Summary = "气道异物梗阻的现场急救步骤",
                SortOrder = 2,
                IsEnabled = true,
                Steps = new List<QuickActionStepRecord>
                {
                    new()
                    {
                        Order = 1,
                        Title = "确认梗阻",
                        Description = "询问患者是否噎住，观察是否无法说话、呼吸困难、面色发紫。",
                        Image = "/static/images/course-thumb-heimlich.jpg",
                        KeyPoints = new List<string> { "患者双手掐喉是典型信号", "确认是气道完全梗阻" }
                    },
                    new()
                    {
                        Order = 2,
                        Title = "站位与手势",
                        Description = "站在患者背后，双臂从腋下环抱，一手握拳置于肚脐上方。",
                        Image = "/static/images/course-thumb-heimlich.jpg",
                        KeyPoints = new List<string> { "拳眼向内", "位置在肚脐上两横指" }
                    },
                    new()
                    {
                        Order = 3,
                        Title = "向内上方冲击",
                        Description = "另一手握住拳头，快速向内上方冲击腹部。",
                        Image = "/static/images/course-thumb-heimlich.jpg",
                        KeyPoints = new List<string> { "快速有力", "重复至异物排出或患者失去意识" }
                    }
                }
            }
        };
    }
}
