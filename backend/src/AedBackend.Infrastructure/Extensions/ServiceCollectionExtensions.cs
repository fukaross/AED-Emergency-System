using AedBackend.Core.Services.WeChat;
using AedBackend.Core.Services.Help;
using AedBackend.Infrastructure.Help;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWeChatInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<WeChatOptions>(configuration.GetSection(WeChatOptions.SectionName));
        services.PostConfigure<WeChatOptions>(options => options.ApplyEnvironmentAliases(configuration));
        services.AddSingleton<ISessionTokenService, SessionTokenService>();
        services.AddSingleton<IExperienceUserStore, JsonExperienceUserStore>();
        services.AddSingleton<IHelpRequestStore, JsonHelpRequestStore>();
        services.AddSingleton<IAedDeviceStore, JsonAedDeviceStore>();
        services.AddHttpClient<IWeChatLoginClient, WeChatLoginClient>();
        services.AddHttpClient<IWeChatPhoneNumberClient, WeChatPhoneNumberClient>();
        services.AddHttpClient<WeChatAccessTokenClient>();
        services.AddHttpClient<IWeChatSubscribeMessageSender, WeChatSubscribeMessageSender>();

        return services;
    }
}
