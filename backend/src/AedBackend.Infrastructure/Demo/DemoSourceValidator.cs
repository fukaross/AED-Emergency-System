using System.Text.Json;
using AedBackend.Infrastructure.Site;

namespace AedBackend.Infrastructure.Demo;

/** 演示源数据在落库前的交叉校验结果，供初始化对账复用。 */
internal sealed class DemoSourceSnapshot
{
    public required int ExperienceUserCount { get; init; }
    public required int AdminUserCount { get; init; }
    public required int AedDeviceCount { get; init; }
    public required int CourseCount { get; init; }
    public required int QuickActionCount { get; init; }
    public required IReadOnlyDictionary<string, DemoSourceUser> UsersByOpenId { get; init; }
    public required IReadOnlyDictionary<string, DemoSourceResponder> RespondersById { get; init; }
    public required IReadOnlyDictionary<string, DemoSourceAdminProfile> AdminProfilesByOpenId { get; init; }
}

internal sealed record DemoSourceUser(string OpenId);

internal sealed record DemoSourceResponder(
    string Id,
    string? BoundOpenId,
    string QualificationStatus,
    string Campus);

internal sealed record DemoSourceAdminProfile(string OpenId, string AccountStatus);

/**
 * 校验 demo/ 生成的只读数据与站点配置、演示角色注册表是否自洽：
 * 校区 code 必须存在于 config/site.json（不静默接受孤立 code），
 * 角色与用户/档案的绑定必须支持真实授权判定，且不得预置管理员。
 */
internal static class DemoSourceValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<DemoSourceSnapshot> ValidateAsync(
        string demoDataRoot,
        SiteOptions site,
        DemoRolesDocument roles,
        CancellationToken ct)
    {
        EnsureRequiredFiles(demoDataRoot);

        var users = await ReadWrappedListAsync<UserDatabase, UserDto>(demoDataRoot, "wechat-experience-users.json", db => db.Users, ct);
        var adminUsers = await ReadWrappedListAsync<AdminUserDatabase, AdminUserDto>(demoDataRoot, "admin-users.json", db => db.Users, ct);
        var responders = await ReadListAsync<ResponderDto>(demoDataRoot, "rescuer-profiles.json", ct);
        var devices = await ReadListAsync<AedDeviceDto>(demoDataRoot, "aed-devices.json", ct);
        var courses = await ReadListAsync<JsonElement>(demoDataRoot, "courses.json", ct);
        var quickActions = await ReadListAsync<JsonElement>(demoDataRoot, "quick-actions.json", ct);
        var profiles = await ReadWrappedMapAsync(demoDataRoot, "admin-user-profiles.json", ct);

        var usersByOpenId = ValidateUsers(users);
        var respondersById = ValidateResponders(responders, site);
        ValidateDevices(devices, site);
        ValidateAdminUsers(adminUsers);
        ValidateAdminProfiles(profiles, usersByOpenId);
        ValidateRoles(roles, site, usersByOpenId, respondersById, profiles);
        ValidateNegativeSamples(roles, usersByOpenId, respondersById, profiles);

        return new DemoSourceSnapshot
        {
            ExperienceUserCount = usersByOpenId.Count,
            AdminUserCount = adminUsers.Count,
            AedDeviceCount = devices.Count,
            CourseCount = courses.Count,
            QuickActionCount = quickActions.Count,
            UsersByOpenId = usersByOpenId,
            RespondersById = respondersById,
            AdminProfilesByOpenId = profiles
        };
    }

    private static void EnsureRequiredFiles(string demoDataRoot)
    {
        var missing = DemoDataLayout.RequiredFiles
            .Where(file => !File.Exists(Path.Combine(demoDataRoot, file)))
            .ToArray();
        if (missing.Length == 0) return;
        throw new DemoInitializationException(
            $"演示数据目录缺少迁移器输入文件：{demoDataRoot} => {string.Join(", ", missing)}");
    }

    private static Dictionary<string, DemoSourceUser> ValidateUsers(IReadOnlyList<UserDto> users)
    {
        var result = new Dictionary<string, DemoSourceUser>(StringComparer.Ordinal);
        foreach (var user in users)
        {
            if (string.IsNullOrWhiteSpace(user.OpenId))
            {
                throw new DemoInitializationException("wechat-experience-users.json 存在空 openId");
            }

            if (!result.TryAdd(user.OpenId, new DemoSourceUser(user.OpenId)))
            {
                throw new DemoInitializationException($"演示用户 openId 重复：{user.OpenId}");
            }
        }

        return result;
    }

    private static Dictionary<string, DemoSourceResponder> ValidateResponders(
        IReadOnlyList<ResponderDto> responders,
        SiteOptions site)
    {
        var result = new Dictionary<string, DemoSourceResponder>(StringComparer.Ordinal);
        foreach (var responder in responders)
        {
            if (string.IsNullOrWhiteSpace(responder.Id))
            {
                throw new DemoInitializationException("rescuer-profiles.json 存在空 id");
            }

            if (site.FindCampus(responder.Campus) is null)
            {
                throw new DemoInitializationException(
                    $"急救员档案 {responder.Id} 的 campus={responder.Campus} 不在 config/site.json 的校区列表中；"
                    + "孤立 campusCode 不会被静默接受。");
            }

            if (!IsValidPoint(responder.Lat, responder.Lng))
            {
                throw new DemoInitializationException($"急救员档案 {responder.Id} 坐标不合法");
            }

            if (!result.TryAdd(responder.Id, new DemoSourceResponder(
                    responder.Id,
                    string.IsNullOrWhiteSpace(responder.BoundOpenId) ? null : responder.BoundOpenId,
                    responder.QualificationStatus,
                    responder.Campus)))
            {
                throw new DemoInitializationException($"急救员档案 id 重复：{responder.Id}");
            }
        }

        return result;
    }

    private static void ValidateDevices(IReadOnlyList<AedDeviceDto> devices, SiteOptions site)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices)
        {
            if (string.IsNullOrWhiteSpace(device.Id) || string.IsNullOrWhiteSpace(device.Name))
            {
                throw new DemoInitializationException("aed-devices.json 存在空 id/name");
            }

            if (!ids.Add(device.Id))
            {
                throw new DemoInitializationException($"AED 设备 id 重复：{device.Id}");
            }

            if (!names.Add(device.Name))
            {
                throw new DemoInitializationException($"AED 设备 name 重复：{device.Name}");
            }

            if (!IsValidPoint(device.Lat, device.Lng))
            {
                throw new DemoInitializationException($"AED 设备 {device.Id} 坐标不合法");
            }
        }

        // 设备归属由最近校区中心推导（模型无 campus 字段），这里只确认站点配置可用。
        if (site.Campuses.Count == 0)
        {
            throw new DemoInitializationException("站点配置没有校区，无法推导 AED 设备归属");
        }
    }

    private static void ValidateAdminUsers(IReadOnlyList<AdminUserDto> adminUsers)
    {
        if (adminUsers.Count == 0) return;
        throw new DemoInitializationException(
            "演示数据不得预置管理员（admin-users.json 必须为空）；管理员请通过 POST /api/admin/auth/bootstrap 创建。");
    }

    private static void ValidateAdminProfiles(
        IReadOnlyDictionary<string, DemoSourceAdminProfile> profiles,
        IReadOnlyDictionary<string, DemoSourceUser> users)
    {
        foreach (var profile in profiles.Values)
        {
            if (!users.ContainsKey(profile.OpenId))
            {
                throw new DemoInitializationException(
                    $"admin-user-profiles.json 中的 openId={profile.OpenId} 不在演示用户列表中");
            }

            if (string.IsNullOrWhiteSpace(profile.AccountStatus))
            {
                throw new DemoInitializationException($"admin-user-profiles.json 中 {profile.OpenId} 缺少 accountStatus");
            }
        }
    }

    private static void ValidateRoles(
        DemoRolesDocument document,
        SiteOptions site,
        IReadOnlyDictionary<string, DemoSourceUser> users,
        IReadOnlyDictionary<string, DemoSourceResponder> responders,
        IReadOnlyDictionary<string, DemoSourceAdminProfile> profiles)
    {
        foreach (var role in document.DemoRoles)
        {
            if (site.FindCampus(role.Campus) is null)
            {
                throw new DemoInitializationException(
                    $"演示角色 {role.Role} 的 campus={role.Campus} 不在 config/site.json 的校区列表中");
            }

            if (!users.ContainsKey(role.OpenId))
            {
                throw new DemoInitializationException(
                    $"演示角色 {role.Role} 的 openId={role.OpenId} 不在 wechat-experience-users.json 中");
            }

            if (profiles.TryGetValue(role.OpenId, out var profile))
            {
                if (!string.Equals(profile.AccountStatus, role.AccountStatus, StringComparison.OrdinalIgnoreCase))
                {
                    throw new DemoInitializationException(
                        $"演示角色 {role.Role} 的 accountStatus={role.AccountStatus} 与 admin-user-profiles.json 的 {profile.AccountStatus} 不一致");
                }
            }

            var certified = ResolveCertified(role, responders);
            if (certified != role.ExpectedCertifiedResponder)
            {
                throw new DemoInitializationException(
                    $"演示角色 {role.Role} 的 expectedCertifiedResponder={role.ExpectedCertifiedResponder} 与实际授权判定 {certified} 不一致");
            }
        }
    }

    private static void ValidateNegativeSamples(
        DemoRolesDocument document,
        IReadOnlyDictionary<string, DemoSourceUser> users,
        IReadOnlyDictionary<string, DemoSourceResponder> responders,
        IReadOnlyDictionary<string, DemoSourceAdminProfile> profiles)
    {
        foreach (var sample in document.NegativeSamples)
        {
            if (!users.ContainsKey(sample.OpenId))
            {
                throw new DemoInitializationException(
                    $"演示负向样本 {sample.Id} 的 openId={sample.OpenId} 不在演示用户列表中");
            }

            if (!profiles.TryGetValue(sample.OpenId, out var profile)
                || !string.Equals(profile.AccountStatus, sample.AccountStatus, StringComparison.OrdinalIgnoreCase))
            {
                throw new DemoInitializationException(
                    $"演示负向样本 {sample.Id} 的 accountStatus={sample.AccountStatus} 与 admin-user-profiles.json 不一致");
            }

            if (sample.ResponderProfileId is { } responderId && !responders.ContainsKey(responderId))
            {
                throw new DemoInitializationException(
                    $"演示负向样本 {sample.Id} 引用了不存在的急救员档案 {responderId}");
            }
        }
    }

    private static bool ResolveCertified(
        DemoRoleDefinition role,
        IReadOnlyDictionary<string, DemoSourceResponder> responders)
    {
        if (role.ResponderProfileId is not { } profileId) return false;
        if (!responders.TryGetValue(profileId, out var responder))
        {
            throw new DemoInitializationException(
                $"演示角色 {role.Role} 引用了不存在的急救员档案 {profileId}");
        }

        if (!string.Equals(responder.BoundOpenId, role.OpenId, StringComparison.Ordinal))
        {
            throw new DemoInitializationException(
                $"急救员档案 {profileId} 的 boundOpenId={responder.BoundOpenId} 与演示角色 {role.Role} 的 openId={role.OpenId} 不一致");
        }

        if (role.QualificationStatus is { } expectedStatus
            && !string.Equals(responder.QualificationStatus, expectedStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new DemoInitializationException(
                $"急救员档案 {profileId} 的 qualificationStatus={responder.QualificationStatus} 与角色期望 {expectedStatus} 不一致");
        }

        if (!string.Equals(responder.Campus, role.Campus, StringComparison.OrdinalIgnoreCase))
        {
            throw new DemoInitializationException(
                $"急救员档案 {profileId} 的 campus={responder.Campus} 与演示角色 {role.Role} 的 campus={role.Campus} 不一致");
        }

        return string.Equals(responder.QualificationStatus, "active", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidPoint(double lat, double lng)
    {
        return IsFinite(lat) && lat is >= -90 and <= 90
            && IsFinite(lng) && lng is >= -180 and <= 180;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static async Task<IReadOnlyList<TItem>> ReadListAsync<TItem>(
        string demoDataRoot,
        string fileName,
        CancellationToken ct)
    {
        var path = Path.Combine(demoDataRoot, fileName);
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<TItem>>(stream, JsonOptions, ct) ?? [];
        }
        catch (JsonException exception)
        {
            throw new DemoInitializationException($"演示数据不是合法 JSON：{path}", exception);
        }
    }

    private static async Task<IReadOnlyList<TItem>> ReadWrappedListAsync<TDb, TItem>(
        string demoDataRoot,
        string fileName,
        Func<TDb, IReadOnlyList<TItem>> selector,
        CancellationToken ct)
        where TDb : new()
    {
        var path = Path.Combine(demoDataRoot, fileName);
        try
        {
            await using var stream = File.OpenRead(path);
            var database = await JsonSerializer.DeserializeAsync<TDb>(stream, JsonOptions, ct) ?? new TDb();
            return selector(database);
        }
        catch (JsonException exception)
        {
            throw new DemoInitializationException($"演示数据不是合法 JSON：{path}", exception);
        }
    }

    private static async Task<IReadOnlyDictionary<string, DemoSourceAdminProfile>> ReadWrappedMapAsync(
        string demoDataRoot,
        string fileName,
        CancellationToken ct)
    {
        var path = Path.Combine(demoDataRoot, fileName);
        try
        {
            await using var stream = File.OpenRead(path);
            var database = await JsonSerializer.DeserializeAsync<AdminProfileDatabase>(stream, JsonOptions, ct)
                ?? new AdminProfileDatabase();
            return database.Profiles.ToDictionary(
                pair => pair.Key,
                pair => new DemoSourceAdminProfile(
                    string.IsNullOrWhiteSpace(pair.Value.OpenId) ? pair.Key : pair.Value.OpenId,
                    pair.Value.AccountStatus ?? string.Empty),
                StringComparer.Ordinal);
        }
        catch (JsonException exception)
        {
            throw new DemoInitializationException($"演示数据不是合法 JSON：{path}", exception);
        }
    }

    private sealed class UserDatabase
    {
        public List<UserDto> Users { get; init; } = [];
    }

    private sealed class UserDto
    {
        public string OpenId { get; init; } = string.Empty;
    }

    private sealed class AdminUserDatabase
    {
        public List<AdminUserDto> Users { get; init; } = [];
    }

    private sealed class AdminUserDto
    {
        public string Id { get; init; } = string.Empty;
    }

    private sealed class ResponderDto
    {
        public string Id { get; init; } = string.Empty;
        public string? BoundOpenId { get; init; }
        public string QualificationStatus { get; init; } = string.Empty;
        public string Campus { get; init; } = string.Empty;
        public double Lat { get; init; }
        public double Lng { get; init; }
    }

    private sealed class AedDeviceDto
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public double Lat { get; init; }
        public double Lng { get; init; }
    }

    private sealed class AdminProfileDatabase
    {
        public Dictionary<string, AdminProfileDto> Profiles { get; init; } = [];
    }

    private sealed class AdminProfileDto
    {
        public string? OpenId { get; init; }
        public string? AccountStatus { get; init; }
    }
}
