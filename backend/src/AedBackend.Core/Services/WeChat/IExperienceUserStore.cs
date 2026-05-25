using AedBackend.Core.DTOs.WeChat;
using AedBackend.Core.Models.WeChat;

namespace AedBackend.Core.Services.WeChat;

public interface IExperienceUserStore
{
    Task<SaveLoginResult> SaveLoginAsync(
        WeChatLoginSession session,
        string tokenHash,
        CancellationToken cancellationToken);

    Task<ExperienceUserRecord?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    Task<SaveLoginResult?> RefreshTokenAsync(
        string oldTokenHash,
        string newTokenHash,
        CancellationToken cancellationToken);

    Task SaveSubscriptionAsync(
        string tokenHash,
        SaveSubscribeDecisionRequest request,
        CancellationToken cancellationToken);

    Task<ExperienceUserRecord> SavePhoneAsync(
        string tokenHash,
        string phone,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ExperienceUserRecord>> ListUsersAsync(CancellationToken cancellationToken);
}
