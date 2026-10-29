using System.Globalization;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Rescuer;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Site;

namespace AedBackend.Api.Endpoints;

public static class RescuerEndpoints
{
    private const string ActiveStatus = "active";
    private const string OnlineStatus = "online";
    private const string OfflineStatus = "offline";

    public static IEndpointRouteBuilder MapRescuerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rescuers").WithTags("Rescuers");

        group.MapGet("/nearby", ListNearbyAsync).WithName("ListNearbyRescuers");
        group.MapGet("/{id}", GetRescuerAsync).WithName("GetRescuerDetail");

        return app;
    }

    private static async Task<IResult> ListNearbyAsync(
        HttpRequest httpRequest,
        string? lat,
        string? lng,
        IResponderStore store,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        SiteOptions site,
        CancellationToken ct)
    {
        var currentUser = await EndpointUsers.RequireActiveUserAsync(
            httpRequest,
            new ActiveUserDependencies(tokenService, userStore, accountStatus),
            ct);
        if (currentUser.Failure is not null) return currentUser.Failure;

        var viewerPoint = ParseViewerPoint(lat, lng);
        if (viewerPoint.Error is not null)
            return EndpointFailures.JsonFailure(
                ErrorCodes.ParameterInvalid, viewerPoint.Error, 400);

        var (viewerCampus, viewerCampusDistance) = site.ResolveNearestCampus(viewerPoint.Point);
        var serviceRadiusMeters = site.ServiceRadiusMeters;
        var profiles = await store.GetAllAsync(ct);
        var items = viewerCampusDistance > serviceRadiusMeters
            ? []
            : profiles
                .Where(p => IsActiveInCampus(p, viewerCampus.Code))
                .Select(p => ToProfileDistance(p, viewerPoint.Point, site))
                .OrderBy(p => p.AvailabilityStatus == OnlineStatus ? 0 : 1)
                .ThenBy(p => p.Distance)
                .Select(item => ToPublicListItem(item, site))
                .ToArray();

        var response = new RescuerListResponse(
            viewerCampus.Code,
            viewerCampus.Name,
            viewerCampusDistance,
            serviceRadiusMeters,
            items);

        return Results.Ok(ApiResponse.Ok(response));
    }

    private static bool IsActiveInCampus(ResponderProfileRecord profile, string campus)
    {
        return profile.QualificationStatus == ActiveStatus
            && profile.Campus.Equals(campus, StringComparison.OrdinalIgnoreCase);
    }

    private static (SiteGeoPoint Point, string? Error) ParseViewerPoint(
        string? lat,
        string? lng)
    {
        if (!TryParseCoordinate(lat, out var parsedLat)) return (default, "lat 不合法");
        if (!TryParseCoordinate(lng, out var parsedLng)) return (default, "lng 不合法");

        if (parsedLat is < -90 or > 90) return (default, "lat 不合法");
        return parsedLng is < -180 or > 180
            ? (default, "lng 不合法")
            : (new SiteGeoPoint(parsedLat, parsedLng), null);
    }

    private static bool TryParseCoordinate(string? value, out double result)
    {
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static ProfileDistance ToProfileDistance(
        ResponderProfileRecord profile,
        SiteGeoPoint viewer,
        SiteOptions site)
    {
        return new ProfileDistance(
            profile,
            SiteOptions.DistanceMeters(viewer, new SiteGeoPoint(profile.Lat, profile.Lng)),
            ResolveAvailabilityStatus(profile, site));
    }

    /**
     * 在线判定沿用既有语义：资格 active 且到所属校区中心不超过服务半径才算在线
     * （等于半径不判离线）。半径来自站点配置 serviceRadiusKm。
     * 站点配置已不包含该校区 code 的历史档案没有可比较的中心点，按离线保守处理且不回落到默认校区。
     */
    private static string ResolveAvailabilityStatus(ResponderProfileRecord profile, SiteOptions site)
    {
        if (profile.QualificationStatus != ActiveStatus) return OfflineStatus;

        var campus = site.FindCampus(profile.Campus);
        if (campus is null) return OfflineStatus;
        var profilePoint = new SiteGeoPoint(profile.Lat, profile.Lng);
        return SiteOptions.DistanceMeters(profilePoint, campus.Center) > site.ServiceRadiusMeters
            ? OfflineStatus
            : OnlineStatus;
    }

    private static RescuerListItem ToPublicListItem(ProfileDistance item, SiteOptions site)
    {
        var profile = item.Profile;
        return new RescuerListItem(
            profile.Id,
            profile.Name,
            profile.AvatarUrl,
            profile.Campus,
            site.ResolveCampusName(profile.Campus),
            profile.Level,
            item.AvailabilityStatus,
            profile.QualificationStatus,
            item.Distance,
            MaskPhone(profile.Phone),
            ToSkills(profile));
    }

    private static RescuerSkillFlags ToSkills(ResponderProfileRecord profile)
    {
        return new RescuerSkillFlags(
            profile.SkillCpr,
            profile.SkillAed,
            profile.SkillStb);
    }

    private static async Task<IResult> GetRescuerAsync(
        HttpRequest httpRequest,
        string id,
        IResponderStore store,
        ISessionTokenService tokenService,
        IExperienceUserStore userStore,
        IUserAccountStatusService accountStatus,
        SiteOptions site,
        CancellationToken ct)
    {
        var currentUser = await EndpointUsers.RequireActiveUserAsync(
            httpRequest,
            new ActiveUserDependencies(tokenService, userStore, accountStatus),
            ct);
        if (currentUser.Failure is not null) return currentUser.Failure;

        var profile = await store.FindByIdAsync(id, ct);
        if (profile is null) return Results.Json(
            ApiResponse.Fail(2006, "急救员不存在"), statusCode: 404);

        return Results.Ok(ApiResponse.Ok(ToPublicDetail(profile, site)));
    }

    private static RescuerDetailResponse ToPublicDetail(ResponderProfileRecord p, SiteOptions site)
    {
        return new RescuerDetailResponse(
            p.Id,
            p.Name,
            p.AvatarUrl,
            p.Campus,
            site.ResolveCampusName(p.Campus),
            p.Level,
            ResolveAvailabilityStatus(p, site),
            p.QualificationStatus,
            p.Phone,
            MaskPhone(p.Phone),
            p.CertificateNoMasked,
            p.WorkUnit,
            ToSkills(p));
    }

    private static string MaskPhone(string phone)
    {
        return string.IsNullOrWhiteSpace(phone) || phone.Length < 7
            ? phone
            : $"{phone[..3]}****{phone[^4..]}";
    }

    private sealed record ProfileDistance(
        ResponderProfileRecord Profile,
        int Distance,
        string AvailabilityStatus);
}
