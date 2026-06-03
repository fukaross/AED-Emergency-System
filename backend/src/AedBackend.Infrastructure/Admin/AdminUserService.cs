using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Infrastructure.Admin;

public sealed class AdminUserService : IAdminUserService, IUserAccountStatusService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const string ActiveQualificationStatus = "active";
    private const string DefaultResponderType = "rescuer";

    private readonly IExperienceUserStore experienceUsers;
    private readonly IAdminUserProfileStore profiles;
    private readonly IResponderStore responders;

    public AdminUserService(
        IExperienceUserStore experienceUsers,
        IAdminUserProfileStore profiles,
        IResponderStore responders)
    {
        this.experienceUsers = experienceUsers;
        this.profiles = profiles;
        this.responders = responders;
    }

    public async Task<PaginatedData<AdminUserListItem>> ListUsersAsync(
        AdminUserQuery query,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);

        var users = await experienceUsers.ListUsersAsync(cancellationToken);
        var profileMap = await profiles.ListAsync(cancellationToken);
        var responderMap = await ListBoundRespondersAsync(cancellationToken);
        var items = users.Select(user =>
            ToSearchItem(user, GetProfile(user.OpenId, profileMap), GetResponder(user.OpenId, responderMap)));
        var filtered = ApplyFilters(items, query).ToArray();
        var page = Math.Max(DefaultPage, query.Page ?? DefaultPage);
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, 100);
        var paged = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => item.Display)
            .ToArray();

        return new PaginatedData<AdminUserListItem>(
            paged,
            filtered.Length,
            page,
            pageSize,
            Math.Max(1, (int)Math.Ceiling(filtered.Length / (double)pageSize)));
    }

    public async Task<AdminUserDetail?> GetUserAsync(
        string openId,
        CancellationToken cancellationToken)
    {
        var users = await experienceUsers.ListUsersAsync(cancellationToken);
        var user = users.FirstOrDefault(item => item.OpenId == openId);
        if (user is null) return null;

        var profile = await profiles.FindAsync(openId, cancellationToken) ?? DefaultProfile(openId);
        var responder = await FindBoundResponderAsync(openId, cancellationToken);
        return ToDetail(user, profile, responder);
    }

    public async Task<AdminUserDetail> UpdateUserAsync(
        string openId,
        AdminUserUpdatePayload payload,
        CancellationToken cancellationToken)
    {
        ValidatePayload(payload);
        var users = await experienceUsers.ListUsersAsync(cancellationToken);
        var user = users.FirstOrDefault(item => item.OpenId == openId);
        if (user is null) throw new KeyNotFoundException("用户不存在");

        var updated = await profiles.UpsertAsync(
            openId,
            current => ApplyUpdate(current ?? DefaultProfile(openId), payload),
            cancellationToken);

        var responder = await FindBoundResponderAsync(openId, cancellationToken);
        return ToDetail(user, updated, responder);
    }

    public async Task<bool> IsDisabledAsync(
        string openId,
        CancellationToken cancellationToken)
    {
        var profile = await profiles.FindAsync(openId, cancellationToken);
        return profile?.AccountStatus == AdminStatuses.Disabled;
    }

    public async Task<string> GetPrimaryRoleAsync(
        string openId,
        CancellationToken cancellationToken)
    {
        var profile = await profiles.FindAsync(openId, cancellationToken);
        return profile?.PrimaryRole ?? DefaultProfile(openId).PrimaryRole;
    }

    private static void ValidateQuery(AdminUserQuery query)
    {
        ValidateNullable(query.PrimaryRole, AdminUserFields.PrimaryRoles, "primaryRole 不支持");
        ValidateNullable(query.AccountStatus, AdminUserFields.AccountStatuses, "accountStatus 不支持");
        ValidateNullable(query.ResponderStatus, AdminUserFields.ResponderStatuses, "responderStatus 不支持");
        ValidateNullable(query.Level, AdminUserFields.Levels, "level 不支持");
    }

    private static void ValidatePayload(AdminUserUpdatePayload payload)
    {
        ValidateOptional(payload.PrimaryRole, AdminUserFields.PrimaryRoles, "primaryRole 不支持");
        ValidateOptional(payload.AccountStatus, AdminUserFields.AccountStatuses, "accountStatus 不支持");
        if (payload.ResponderStatus.HasValue || payload.Certified.HasValue || payload.Level.HasValue)
            throw new ArgumentException("急救资格请在急救员管理中维护");
    }

    private static void ValidateNullable(
        string? value,
        IReadOnlySet<string> allowed,
        string message)
    {
        if (!string.IsNullOrWhiteSpace(value) && !allowed.Contains(value))
        {
            throw new ArgumentException(message);
        }
    }

    private static void ValidateOptional<T>(
        OptionalValue<T> value,
        IReadOnlySet<string> allowed,
        string message)
    {
        if (value is { HasValue: true, Value: string text }
            && !string.IsNullOrWhiteSpace(text)
            && !allowed.Contains(text))
        {
            throw new ArgumentException(message);
        }
    }

    private static IEnumerable<AdminUserSearchItem> ApplyFilters(
        IEnumerable<AdminUserSearchItem> items,
        AdminUserQuery query)
    {
        var filtered = items;
        if (!string.IsNullOrWhiteSpace(query.PrimaryRole))
            filtered = filtered.Where(item => item.Display.PrimaryRole == query.PrimaryRole);
        if (!string.IsNullOrWhiteSpace(query.AccountStatus))
            filtered = filtered.Where(item => item.Display.AccountStatus == query.AccountStatus);
        if (!string.IsNullOrWhiteSpace(query.ResponderStatus))
            filtered = filtered.Where(item => item.Display.ResponderStatus == query.ResponderStatus);
        if (!string.IsNullOrWhiteSpace(query.Level))
            filtered = filtered.Where(item => item.Display.Level == query.Level);

        return string.IsNullOrWhiteSpace(query.Keyword)
            ? filtered
            : filtered.Where(item => MatchesKeyword(item, query.Keyword.Trim()));
    }

    private static bool MatchesKeyword(AdminUserSearchItem item, string keyword)
    {
        return Contains(item.Display.OpenId, keyword)
            || Contains(item.Display.Name, keyword)
            || Contains(item.RawPhone, keyword)
            || Contains(item.Display.Phone, keyword)
            || Contains(item.Display.AdminRemark, keyword);
    }

    private static bool Contains(string? value, string keyword)
    {
        return value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static AdminUserProfileRecord GetProfile(
        string openId,
        IReadOnlyDictionary<string, AdminUserProfileRecord> profiles)
    {
        return profiles.TryGetValue(openId, out var profile) ? profile : DefaultProfile(openId);
    }

    private static AdminUserProfileRecord DefaultProfile(string openId)
    {
        return new AdminUserProfileRecord { OpenId = openId };
    }

    private static AdminUserProfileRecord ApplyUpdate(
        AdminUserProfileRecord current,
        AdminUserUpdatePayload payload)
    {
        return current with
        {
            PrimaryRole = payload.PrimaryRole.HasValue
                ? payload.PrimaryRole.Value ?? current.PrimaryRole
                : current.PrimaryRole,
            AccountStatus = payload.AccountStatus.HasValue
                ? payload.AccountStatus.Value ?? current.AccountStatus
                : current.AccountStatus,
            AdminRemark = payload.AdminRemark.HasValue
                ? payload.AdminRemark.Value
                : current.AdminRemark,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static AdminUserSearchItem ToSearchItem(
        ExperienceUserRecord user,
        AdminUserProfileRecord profile,
        ResponderProfileRecord? responder)
    {
        return new AdminUserSearchItem(user.Phone, ToListItem(user, profile, responder));
    }

    private static AdminUserListItem ToListItem(
        ExperienceUserRecord user,
        AdminUserProfileRecord profile,
        ResponderProfileRecord? responder)
    {
        var summary = ToRescuerSummary(responder);
        return new AdminUserListItem(
            user.OpenId,
            CreateDisplayName(user),
            string.IsNullOrWhiteSpace(user.AvatarUrl) ? null : user.AvatarUrl,
            MaskPhone(user.Phone),
            profile.PrimaryRole,
            profile.AccountStatus,
            summary?.ResponderStatus,
            summary?.Certified ?? false,
            summary?.Level,
            profile.AdminRemark,
            profile.UpdatedAt);
    }

    private static AdminUserDetail ToDetail(
        ExperienceUserRecord user,
        AdminUserProfileRecord profile,
        ResponderProfileRecord? responder)
    {
        var item = ToListItem(user, profile, responder);
        var subscriptions = user.Subscriptions.ToDictionary(
            pair => pair.Key,
            pair => (object?)pair.Value.Status);

        return new AdminUserDetail(
            item.OpenId,
            item.Name,
            item.AvatarUrl,
            item.Phone,
            item.PrimaryRole,
            item.AccountStatus,
            item.ResponderStatus,
            item.Certified,
            item.Level,
            item.AdminRemark,
            item.UpdatedAt,
            null,
            ToRescuerSummary(responder),
            subscriptions,
            user.CreatedAt,
            user.LastLoginAt);
    }

    private static string CreateDisplayName(ExperienceUserRecord user)
    {
        if (!string.IsNullOrWhiteSpace(user.Name)) return user.Name;
        return string.IsNullOrWhiteSpace(user.Phone) ? user.OpenId : $"用户{user.Phone[^4..]}";
    }

    private static string? MaskPhone(string? phone)
    {
        return string.IsNullOrWhiteSpace(phone) || phone.Length < 7
            ? phone
            : $"{phone[..3]}****{phone[^4..]}";
    }

    private async Task<IReadOnlyDictionary<string, ResponderProfileRecord>> ListBoundRespondersAsync(
        CancellationToken cancellationToken)
    {
        var items = await responders.GetAllAsync(cancellationToken);
        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.BoundOpenId))
            .GroupBy(item => item.BoundOpenId!)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private async Task<ResponderProfileRecord?> FindBoundResponderAsync(
        string openId,
        CancellationToken cancellationToken)
    {
        var items = await responders.GetAllAsync(cancellationToken);
        return items.FirstOrDefault(item => item.BoundOpenId == openId);
    }

    private static ResponderProfileRecord? GetResponder(
        string openId,
        IReadOnlyDictionary<string, ResponderProfileRecord> responders)
    {
        return responders.TryGetValue(openId, out var responder) ? responder : null;
    }

    private static RescuerSummaryDto? ToRescuerSummary(ResponderProfileRecord? profile)
    {
        if (profile is null) return null;
        return new RescuerSummaryDto(
            profile.Id,
            profile.Id,
            profile.QualificationStatus == ActiveQualificationStatus,
            profile.Level,
            DefaultResponderType,
            profile.QualificationStatus,
            null,
            null,
            profile.CertificateNoMasked,
            profile.WorkUnit);
    }

    private sealed record AdminUserSearchItem(
        string? RawPhone,
        AdminUserListItem Display);
}
