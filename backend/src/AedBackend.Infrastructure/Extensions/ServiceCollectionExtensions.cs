using AedBackend.Core.Models.Admin;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.WeChat;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Knowledge;
using AedBackend.Core.Services.Course;
using AedBackend.Infrastructure.Admin;
using AedBackend.Infrastructure.Help;
using AedBackend.Infrastructure.Knowledge;
using AedBackend.Infrastructure.Course;
using AedBackend.Infrastructure.Upload;
using AedBackend.Infrastructure.WeChat;
using Microsoft.AspNetCore.Identity;
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
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
        services.PostConfigure<AdminOptions>(options => options.ApplyEnvironmentAliases(configuration));
        services.AddSingleton<ISessionTokenService, SessionTokenService>();
        services.AddSingleton<IExperienceUserStore, JsonExperienceUserStore>();
        services.AddSingleton<IHelpRequestStore, JsonHelpRequestStore>();
        services.AddSingleton<IAedDeviceStore, JsonAedDeviceStore>();
        services.AddSingleton<IResponderStore, JsonResponderStore>();
        services.AddSingleton<IKnowledgeStore, JsonKnowledgeStore>();
        services.AddSingleton<ICourseStore, JsonCourseStore>();
        services.AddSingleton<IQuickActionStore, JsonQuickActionStore>();
        services.AddSingleton<IUploadImageStorage, LocalUploadImageStorage>();
        services.AddSingleton<IUploadVideoStorage, LocalUploadVideoStorage>();
        services.AddSingleton<IAdminUserStore, JsonAdminUserStore>();
        services.AddSingleton<IAdminUserProfileStore, JsonAdminUserProfileStore>();
        services.AddSingleton<IAdminAuditLogStore, JsonAdminAuditLogStore>();
        services.AddSingleton<IAdminAuthorizationService, AdminAuthorizationService>();
        services.AddSingleton<IAdminUserService, AdminUserService>();
        services.AddSingleton<IPasswordHasher<AdminUserRecord>, PasswordHasher<AdminUserRecord>>();
        services.AddHttpClient<IWeChatLoginClient, WeChatLoginClient>();
        services.AddHttpClient<IWeChatPhoneNumberClient, WeChatPhoneNumberClient>();
        services.AddHttpClient<WeChatAccessTokenClient>();
        services.AddHttpClient<IWeChatSubscribeMessageSender, WeChatSubscribeMessageSender>();

        return services;
    }
}
