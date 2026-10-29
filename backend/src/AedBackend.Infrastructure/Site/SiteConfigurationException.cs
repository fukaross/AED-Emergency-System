namespace AedBackend.Infrastructure.Site;

/** 站点配置缺失或不合法；信息只含字段/路径，不含任何凭据。 */
public sealed class SiteConfigurationException : Exception
{
    public SiteConfigurationException(string message) : base(message)
    {
    }

    public SiteConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
