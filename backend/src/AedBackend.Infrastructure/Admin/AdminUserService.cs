using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Infrastructure.Admin;

public sealed class AdminUserService : IAdminUserService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;

    private readonly IExperienceUserStore experienceUsers;
    private readonly IAdminUserProfileStore profiles;

    public AdminUserService(
        IExperienceUserStore experienceUsers,
        IAdminUserProfileStore profiles)
    {
        this.experienceUsers = experienceUsers;
        this.profiles = profiles;
    }

    public async Task<PaginatedData<AdminUserListItem>> ListUsersAsync(
        AdminUserQuery query,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);

        var users = await experienceUsers.ListUsersAsync(cancellationToken);
        var profileMap = await profiles.ListAsync(cancellationToken);
        var items = users.Select(user => ToListItem(user, GetProfile(user.OpenId, profileMap)));
        var filtered = ApplyFilters(items, query).ToArray();
        var page = Math.Max(DefaultPage, query.Page ?? DefaultPage);
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, 100);
        var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();

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
        return ToDetail(user, profile);
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

        return ToDetail(user, updated);
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
        ValidateOptional(payload.ResponderStatus, AdminUserFields.ResponderStatuses, "responderStatus 不支持");
        ValidateOptional(payload.Level, AdminUserFields.Levels, "level 不支持");
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

    private static IEnumerable<AdminUserListItem> ApplyFilters(
        IEnumerable<AdminUserListItem> items,
        AdminUserQuery query)
    {
        var filtered = items;
        if (!string.IsNullOrWhiteSpace(query.PrimaryRole))
            filtered = filtered.Where(item => item.PrimaryRole == query.PrimaryRole);
        if (!string.IsNullOrWhiteSpace(query.AccountStatus))
            filtered = filtered.Where(item => item.AccountStatus == query.AccountStatus);
        if (!string.IsNullOrWhiteSpace(query.ResponderStatus))
            filtered = filtered.Where(item => item.ResponderStatus == query.ResponderStatus);
        if (!string.IsNullOrWhiteSpace(query.Level))
            filtered = filtered.Where(item => item.Level == query.Level);

        return string.IsNullOrWhiteSpace(query.Keyword)
            ? filtered
            : filtered.Where(item => MatchesKeyword(item, query.Keyword.Trim()));
    }

    private static bool MatchesKeyword(AdminUserListItem item, string keyword)
    {
        return Contains(item.OpenId, keyword)
            || Contains(item.Name, keyword)
            || Contains(item.Phone, keyword)
            || Contains(item.AdminRemark, keyword);
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
            ResponderStatus = payload.ResponderStatus.HasValue
                ? payload.ResponderStatus.Value
                : current.ResponderStatus,
            Certified = payload.Certified.HasValue
                ? payload.Certified.Value
                : current.Certified,
            Level = payload.Level.HasValue ? payload.Level.Value : current.Level,
            AdminRemark = payload.AdminRemark.HasValue
                ? payload.AdminRemark.Value
                : current.AdminRemark,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static AdminUserListItem ToListItem(
        ExperienceUserRecord user,
        AdminUserProfileRecord profile)
    {
        return new AdminUserListItem(
            user.OpenId,
            CreateDisplayName(user),
            string.IsNullOrWhiteSpace(user.AvatarUrl) ? null : user.AvatarUrl,
            MaskPhone(user.Phone),
            profile.PrimaryRole,
            profile.AccountStatus,
            profile.ResponderStatus,
            profile.Certified,
            profile.Level,
            profile.AdminRemark,
            profile.UpdatedAt);
    }

    private static AdminUserDetail ToDetail(
        ExperienceUserRecord user,
        AdminUserProfileRecord profile)
    {
        var item = ToListItem(user, profile);
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
            null,
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
}
