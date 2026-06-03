using AedBackend.Core.DTOs.User;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Models.User;
using AedBackend.Core.Models.Volunteer;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.User;

namespace AedBackend.Api.Endpoints;

internal static partial class UserEndpoints
{
    private static async Task<UserProfileResponse> ToProfileAsync(
        ExperienceUserRecord user,
        ISchoolBindingApplicationStore schoolBindings,
        ISchoolPersonProfileStore schoolProfiles,
        IResponderStore responders,
        CancellationToken ct)
    {
        var schoolProfile = await ResolveSchoolProfileAsync(user.OpenId, schoolBindings, schoolProfiles, ct);
        var rescuerSummary = await ResolveRescuerSummaryAsync(user.OpenId, responders, ct);
        return new UserProfileResponse(
            user.OpenId,
            ResolveName(user),
            user.AvatarUrl ?? string.Empty,
            NormalizeGender(user.Gender),
            user.Phone ?? string.Empty,
            rescuerSummary?.Certified ?? false,
            rescuerSummary?.Level,
            schoolProfile,
            rescuerSummary);
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

    private static SchoolBindingDetailResponse ToSchoolBindingDetail(
        SchoolBindingApplicationRecord item)
    {
        return new SchoolBindingDetailResponse(
            item.Id.ToString(),
            item.Status,
            item.SchoolProfileId,
            item.CreatedAt,
            item.UpdatedAt,
            item.ReviewNote,
            item.ReviewedAt,
            ToSchoolPayload(item.Payload));
    }

    private static ResponderClaimDetailResponse ToResponderClaimDetail(
        ResponderClaimApplicationRecord item)
    {
        return new ResponderClaimDetailResponse(
            item.Id.ToString(),
            item.Status,
            item.ResponderProfileId,
            item.CreatedAt,
            item.UpdatedAt,
            item.ReviewNote,
            item.ReviewedAt,
            ToClaimPayload(item.Payload));
    }

    private static async Task<UserSchoolProfileResponse?> ResolveSchoolProfileAsync(
        string openId,
        ISchoolBindingApplicationStore bindings,
        ISchoolPersonProfileStore profiles,
        CancellationToken ct)
    {
        var page = await bindings.ListByUserAsync(openId, new Core.Services.User.UserIdentityQuery(1, 100), ct);
        var approved = page.Items.OrderByDescending(item => item.ReviewedAt)
            .FirstOrDefault(item => item.Status == SchoolBindingStatuses.Approved && item.SchoolProfileId is not null);
        if (approved?.SchoolProfileId is not int profileId) return null;
        var profile = await profiles.GetByIdAsync(profileId, ct);
        return profile is null ? null : ToUserSchoolProfile(profile, approved);
    }

    private static async Task<UserRescuerSummaryResponse?> ResolveRescuerSummaryAsync(
        string openId,
        IResponderStore responders,
        CancellationToken ct)
    {
        var profiles = await responders.GetAllAsync(ct);
        var profile = profiles.FirstOrDefault(item => item.BoundOpenId == openId);
        return profile is null ? null : ToUserRescuerSummary(profile);
    }

    private static UserSchoolProfileResponse ToUserSchoolProfile(
        SchoolPersonProfileRecord profile,
        SchoolBindingApplicationRecord binding)
    {
        return new UserSchoolProfileResponse(
            profile.Id,
            profile.PersonType,
            profile.Name,
            profile.College,
            profile.ClassName,
            profile.StudentId,
            profile.StaffId,
            profile.Phone,
            "active",
            binding.ReviewedAt ?? binding.UpdatedAt);
    }

    private static UserRescuerSummaryResponse ToUserRescuerSummary(ResponderProfileRecord profile)
    {
        return new UserRescuerSummaryResponse(
            profile.Id,
            profile.Name,
            profile.Campus,
            profile.QualificationStatus == "active",
            profile.Level,
            "rescuer",
            profile.QualificationStatus,
            profile.CertificateNoMasked,
            profile.WorkUnit);
    }

    private static SchoolBindingPayloadRecordDto ToSchoolPayload(SchoolBindingPayloadRecord payload)
    {
        return new SchoolBindingPayloadRecordDto(
            payload.PersonType,
            payload.Name,
            payload.College,
            payload.ClassName,
            payload.StudentId,
            payload.StaffId,
            payload.Phone,
            payload.Note);
    }

    private static ResponderClaimPayloadRecordDto ToClaimPayload(ResponderClaimPayloadRecord payload)
    {
        return new ResponderClaimPayloadRecordDto(
            payload.Name,
            payload.IdNumberLast6,
            payload.Phone,
            payload.CertificateNo,
            payload.Note);
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
