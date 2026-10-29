namespace AedBackend.Core.Services.Help;

/** 表示求助已经进入终态；仅用于将明确的领域冲突映射为 1014。 */
public sealed class HelpAlreadyClosedException : InvalidOperationException
{
    public HelpAlreadyClosedException()
        : base("求助已结束")
    {
    }
}
