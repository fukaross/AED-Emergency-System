namespace AedBackend.Infrastructure.Demo;

/** 演示模式配置/数据缺失或不满足隔离条件；消息只说明配置项，不含凭据。 */
public sealed class DemoConfigurationException : Exception
{
    public DemoConfigurationException(string message) : base(message)
    {
    }

    public DemoConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
