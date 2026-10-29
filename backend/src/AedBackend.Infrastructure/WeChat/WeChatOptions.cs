using Microsoft.Extensions.Configuration;

namespace AedBackend.Infrastructure.WeChat;

public sealed class WeChatOptions
{
    public const string SectionName = "WeChat";

    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string SubscribeTemplateId { get; set; } = string.Empty;
    public string AdminKey { get; set; } = string.Empty;
    public string DefaultMiniProgramState { get; set; } = "trial";
    public string ExperienceUsersFilePath { get; set; } = "data/wechat-experience-users.json";
    public string HelpRequestsFilePath { get; set; } = "data/help-requests.json";
    public string AedDevicesFilePath { get; set; } = "data/aed-devices.json";
    public string UploadsDirectory { get; set; } = "data/uploads";
    public string RescuerProfilesFilePath { get; set; } = "data/rescuer-profiles.json";
    public string KnowledgeSummaryFilePath { get; set; } = "data/knowledge-summary.json";
    public string CoursesFilePath { get; set; } = "data/courses.json";
    public string QuickActionsFilePath { get; set; } = "data/quick-actions.json";
    public string RescueSessionsFilePath { get; set; } = "data/rescue-sessions.json";
    public string PublicBaseUrl { get; set; } = "REDACTED";

    public void ApplyEnvironmentAliases(IConfiguration configuration)
    {
        AppId = FirstNonEmpty(configuration["WECHAT_APP_ID"], AppId);
        AppSecret = FirstNonEmpty(configuration["WECHAT_APP_SECRET"], AppSecret);
        SubscribeTemplateId = FirstNonEmpty(
            configuration["WECHAT_SUBSCRIBE_TEMPLATE_ID"],
            SubscribeTemplateId);
        AdminKey = FirstNonEmpty(configuration["AED_PUSH_ADMIN_KEY"], AdminKey);
        PublicBaseUrl = FirstNonEmpty(configuration["AED_API_PUBLIC_BASE_URL"], PublicBaseUrl);
    }

    private static string FirstNonEmpty(string? first, string second)
    {
        return string.IsNullOrWhiteSpace(first) ? second : first;
    }
}
