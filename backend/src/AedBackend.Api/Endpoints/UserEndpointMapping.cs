using AedBackend.Core.DTOs.User;
using AedBackend.Core.Models.Volunteer;
using AedBackend.Core.Models.WeChat;

namespace AedBackend.Api.Endpoints;

internal static partial class UserEndpoints
{
    private static UserProfileResponse ToProfile(ExperienceUserRecord user)
    {
        return new UserProfileResponse(
            user.OpenId,
            ResolveName(user),
            user.AvatarUrl ?? string.Empty,
            NormalizeGender(user.Gender),
            user.Phone ?? string.Empty,
            false,
            null,
            null,
            null);
    }

    private static VolunteerApplicationDetailResponse ToApplicationDetail(
        VolunteerApplicationRecord item)
    {
        return new VolunteerApplicationDetailResponse(
            item.Id.ToString(),
            item.Status,
            item.AttemptNumber,
            item.DerivedFromId,
            item.CreatedAt,
            item.UpdatedAt,
            item.Payload);
    }

    private static string ResolveName(ExperienceUserRecord user)
    {
        if (!string.IsNullOrWhiteSpace(user.Name)) return user.Name.Trim();
        var suffix = user.OpenId.Length <= 6 ? user.OpenId : user.OpenId[^6..];
        return $"微信用户 {suffix}";
    }

    private static string NormalizeGender(string? gender)
    {
        return gender is not null && Genders.Contains(gender) ? gender : "unknown";
    }
}
