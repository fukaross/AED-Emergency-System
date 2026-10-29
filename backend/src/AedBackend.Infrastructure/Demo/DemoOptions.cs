namespace AedBackend.Infrastructure.Demo;

/**
 * 受限本地演示开关（CONTRACT-003）。
 * 只有在 Development + Demo:Enabled=true + 仅回环监听同时成立时才会启用，
 * 具体判定见 DemoModeGuard，业务代码只读取 Enabled。
 */
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    public bool Enabled { get; set; }
}
