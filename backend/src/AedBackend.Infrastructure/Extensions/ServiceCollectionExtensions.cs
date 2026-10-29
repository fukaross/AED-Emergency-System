using AedBackend.Core.Models.Admin;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.WeChat;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Knowledge;
using AedBackend.Core.Services.Course;
using AedBackend.Core.Services.Volunteer;
using AedBackend.Core.Services.User;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Admin;
using AedBackend.Infrastructure.Help;
using AedBackend.Infrastructure.Knowledge;
using AedBackend.Infrastructure.Course;
using AedBackend.Infrastructure.Persistence;
using AedBackend.Infrastructure.Persistence.Sqlite;
using AedBackend.Infrastructure.Rescue;
using AedBackend.Infrastructure.Upload;
using AedBackend.Infrastructure.Volunteer;
using AedBackend.Infrastructure.WeChat;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        services.Configure<DataOptions>(configuration.GetSection(DataOptions.SectionName));
        services.PostConfigure<DataOptions>(options => options.ApplyEnvironmentAliases(configuration));
        services.Configure<UploadOptions>(configuration.GetSection(UploadOptions.SectionName));
        services.PostConfigure<UploadOptions>(options => options.ApplyEnvironmentAliases(configuration));
        services.AddOptions<RescueOptions>()
            .Bind(configuration.GetSection(RescueOptions.SectionName))
            .Validate(
                options => options.SignalLostAfterSeconds > 0
                    && options.LocationGoneAfterSeconds > options.SignalLostAfterSeconds
                    && options.LocationReportIntervalSeconds > 0,
                "Rescue timing must satisfy 0 < signalLost < locationGone and reportInterval > 0")
            .ValidateOnStart();
        services.AddSingleton<ISessionTokenService, SessionTokenService>();
        AddRuntimeDataStores(services);
        AddUploadStorage(services);
        services.AddSingleton<IAdminAuthorizationService, AdminAuthorizationService>();
        services.AddSingleton<AdminUserService>();
        services.AddSingleton<IAdminUserService>(provider => provider.GetRequiredService<AdminUserService>());
        services.AddSingleton<IUserAccountStatusService>(provider => provider.GetRequiredService<AdminUserService>());
        services.AddSingleton<IPasswordHasher<AdminUserRecord>, PasswordHasher<AdminUserRecord>>();
        services.AddHttpClient<IWeChatLoginClient, WeChatLoginClient>();
        services.AddHttpClient<IWeChatPhoneNumberClient, WeChatPhoneNumberClient>();
        services.AddHttpClient<WeChatAccessTokenClient>();
        services.AddHttpClient<IWeChatSubscribeMessageSender, WeChatSubscribeMessageSender>();

        return services;
    }

    private static void AddUploadStorage(IServiceCollection services)
    {
        services.AddSingleton<LocalUploadImageStorage>();
        services.AddSingleton<LocalUploadVideoStorage>();
        services.AddSingleton<IOssObjectStore, AliyunOssObjectStore>();
        services.AddSingleton<OssUploadSupport>();
        services.AddSingleton<OssUploadImageStorage>();
        services.AddSingleton<OssUploadVideoStorage>();
        services.AddSingleton<IUploadImageStorage>(provider => PickUpload<IUploadImageStorage,
            LocalUploadImageStorage, OssUploadImageStorage>(provider));
        services.AddSingleton<IUploadVideoStorage>(provider => PickUpload<IUploadVideoStorage,
            LocalUploadVideoStorage, OssUploadVideoStorage>(provider));
    }

    private static void AddRuntimeDataStores(IServiceCollection services)
    {
        AddSqliteFactory(services);
        AddJsonStoreImplementations(services);
        AddSqliteStoreImplementations(services);
        AddStoreSelectors(services);
    }

    private static void AddJsonStoreImplementations(IServiceCollection services)
    {
        services.AddSingleton<JsonExperienceUserStore>();
        services.AddSingleton<JsonHelpRequestStore>();
        services.AddSingleton<JsonAedDeviceStore>();
        services.AddSingleton<JsonResponderStore>();
        services.AddSingleton<JsonKnowledgeStore>();
        services.AddSingleton<JsonCourseStore>();
        services.AddSingleton<JsonQuickActionStore>();
        services.AddSingleton<JsonAdminUserStore>();
        services.AddSingleton<JsonAdminUserProfileStore>();
        services.AddSingleton<JsonAdminAuditLogStore>();
        services.AddSingleton<JsonVolunteerApplicationStore>();
        services.AddSingleton<JsonSchoolPersonProfileStore>();
        services.AddSingleton<JsonSchoolBindingApplicationStore>();
        services.AddSingleton<JsonResponderClaimApplicationStore>();
        services.AddSingleton<JsonRescueSessionStore>();
    }

    private static void AddSqliteStoreImplementations(IServiceCollection services)
    {
        services.AddSingleton<SqliteExperienceUserStore>();
        services.AddSingleton<SqliteHelpRequestStore>();
        services.AddSingleton<SqliteAedDeviceStore>();
        services.AddSingleton<SqliteResponderStore>();
        services.AddSingleton<SqliteKnowledgeStore>();
        services.AddSingleton<SqliteCourseStore>();
        services.AddSingleton<SqliteQuickActionStore>();
        services.AddSingleton<SqliteAdminUserStore>();
        services.AddSingleton<SqliteAdminUserProfileStore>();
        services.AddSingleton<SqliteAdminAuditLogStore>();
        services.AddSingleton<SqliteVolunteerApplicationStore>();
        services.AddSingleton<SqliteSchoolPersonProfileStore>();
        services.AddSingleton<SqliteSchoolBindingApplicationStore>();
        services.AddSingleton<SqliteResponderClaimApplicationStore>();
        services.AddSingleton<SqliteRescueSessionStore>();
        services.AddSingleton<SqliteUserIdentityReviewService>();
        services.AddSingleton(provider => new SqliteVolunteerApplicationReviewOptions
        {
            ForcedResponderId = provider.GetRequiredService<IConfiguration>()["Test:ForcedResponderId"]
        });
        services.AddSingleton<SqliteVolunteerApplicationReviewService>();
    }

    private static void AddStoreSelectors(IServiceCollection services)
    {
        services.AddSingleton<IExperienceUserStore>(Pick<IExperienceUserStore, JsonExperienceUserStore, SqliteExperienceUserStore>);
        services.AddSingleton<IHelpRequestStore>(Pick<IHelpRequestStore, JsonHelpRequestStore, SqliteHelpRequestStore>);
        services.AddSingleton<IAedDeviceStore>(Pick<IAedDeviceStore, JsonAedDeviceStore, SqliteAedDeviceStore>);
        services.AddSingleton<IResponderStore>(Pick<IResponderStore, JsonResponderStore, SqliteResponderStore>);
        services.AddSingleton<IKnowledgeStore>(Pick<IKnowledgeStore, JsonKnowledgeStore, SqliteKnowledgeStore>);
        services.AddSingleton<ICourseStore>(Pick<ICourseStore, JsonCourseStore, SqliteCourseStore>);
        services.AddSingleton<IQuickActionStore>(Pick<IQuickActionStore, JsonQuickActionStore, SqliteQuickActionStore>);
        services.AddSingleton<IAdminUserStore>(Pick<IAdminUserStore, JsonAdminUserStore, SqliteAdminUserStore>);
        services.AddSingleton<IAdminUserProfileStore>(Pick<IAdminUserProfileStore, JsonAdminUserProfileStore, SqliteAdminUserProfileStore>);
        services.AddSingleton<IAdminAuditLogStore>(Pick<IAdminAuditLogStore, JsonAdminAuditLogStore, SqliteAdminAuditLogStore>);
        services.AddSingleton<IVolunteerApplicationStore>(Pick<IVolunteerApplicationStore, JsonVolunteerApplicationStore, SqliteVolunteerApplicationStore>);
        services.AddSingleton<ISchoolPersonProfileStore>(Pick<ISchoolPersonProfileStore, JsonSchoolPersonProfileStore, SqliteSchoolPersonProfileStore>);
        services.AddSingleton<ISchoolBindingApplicationStore>(Pick<ISchoolBindingApplicationStore, JsonSchoolBindingApplicationStore, SqliteSchoolBindingApplicationStore>);
        services.AddSingleton<IResponderClaimApplicationStore>(Pick<IResponderClaimApplicationStore, JsonResponderClaimApplicationStore, SqliteResponderClaimApplicationStore>);
        services.AddSingleton<IRescueSessionStore>(Pick<IRescueSessionStore, JsonRescueSessionStore, SqliteRescueSessionStore>);
        services.AddSingleton<IVolunteerApplicationReviewService>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<DataOptions>>().Value;
            return options.UseSqlite()
                ? provider.GetRequiredService<SqliteVolunteerApplicationReviewService>()
                : new JsonVolunteerApplicationReviewService();
        });
        services.AddSingleton<IUserIdentityReviewService>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<DataOptions>>().Value;
            return options.UseSqlite()
                ? provider.GetRequiredService<SqliteUserIdentityReviewService>()
                : new JsonUserIdentityReviewService();
        });
    }

    private static void AddSqliteFactory(IServiceCollection services)
    {
        services.AddDbContextFactory<AedDbContext>((provider, options) =>
        {
            var dataOptions = provider.GetRequiredService<IOptions<DataOptions>>().Value;
            var fullPath = Path.GetFullPath(dataOptions.SqlitePath);
            options.UseSqlite(SqliteStoreHelpers.BuildConnectionString(fullPath));
        });
    }

    private static TService Pick<TService, TJson, TSqlite>(IServiceProvider provider)
        where TService : notnull
        where TJson : TService
        where TSqlite : TService
    {
        var options = provider.GetRequiredService<IOptions<DataOptions>>().Value;
        return options.UseSqlite()
            ? provider.GetRequiredService<TSqlite>()
            : provider.GetRequiredService<TJson>();
    }

    private static TService PickUpload<TService, TLocal, TOss>(IServiceProvider provider)
        where TService : notnull
        where TLocal : TService
        where TOss : TService
    {
        var options = provider.GetRequiredService<IOptions<UploadOptions>>().Value;
        return options.UseOss()
            ? provider.GetRequiredService<TOss>()
            : provider.GetRequiredService<TLocal>();
    }
}
