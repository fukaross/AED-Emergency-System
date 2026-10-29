using System.Globalization;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Rescuer;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Api.Endpoints;

public static class RescuerEndpoints
{
    private const int AutoOfflineRadiusMeters = 30000;
    private const double EarthRadiusMeters = 6371000;
    private const string ActiveStatus = "active";
    private const string OnlineStatus = "online";
    private const string OfflineStatus = "offline";

    private static readonly CampusInfo[] Campuses =
    [
        new("guilinyang", "REDACTED校区", new GeoPoint(REDACTED, REDACTED)),
        new("fucheng", "府城校区", new GeoPoint(REDACTED, REDACTED))
    ];

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

        var viewerCampus = ResolveViewerCampus(viewerPoint.Point);
        var profiles = await store.GetAllAsync(ct);
        var items = viewerCampus.Distance > AutoOfflineRadiusMeters
            ? []
            : profiles
                .Where(p => IsActiveInCampus(p, viewerCampus.Campus.Code))
                .Select(p => ToProfileDistance(p, viewerPoint.Point))
                .OrderBy(p => p.AvailabilityStatus == OnlineStatus ? 0 : 1)
                .ThenBy(p => p.Distance)
                .Select(ToPublicListItem)
                .ToArray();

        var response = new RescuerListResponse(
            viewerCampus.Campus.Code,
            viewerCampus.Campus.Name,
            viewerCampus.Distance,
            AutoOfflineRadiusMeters,
            items);

        return Results.Ok(ApiResponse.Ok(response));
    }

    private static bool IsActiveInCampus(ResponderProfileRecord profile, string campus)
    {
        return profile.QualificationStatus == ActiveStatus
            && profile.Campus.Equals(campus, StringComparison.OrdinalIgnoreCase);
    }

    private static (GeoPoint Point, string? Error) ParseViewerPoint(
        string? lat,
        string? lng)
    {
        if (!TryParseCoordinate(lat, out var parsedLat)) return (default, "lat 不合法");
        if (!TryParseCoordinate(lng, out var parsedLng)) return (default, "lng 不合法");

        if (parsedLat is < -90 or > 90) return (default, "lat 不合法");
        return parsedLng is < -180 or > 180
            ? (default, "lng 不合法")
            : (new GeoPoint(parsedLat, parsedLng), null);
    }

    private static bool TryParseCoordinate(string? value, out double result)
    {
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static (CampusInfo Campus, int Distance) ResolveViewerCampus(GeoPoint viewer)
    {
        return Campuses
            .Select(campus => (Campus: campus, Distance: DistanceMeters(viewer, campus.Point)))
            .OrderBy(item => item.Distance)
            .First();
    }

    private static ProfileDistance ToProfileDistance(
        ResponderProfileRecord profile,
        GeoPoint viewer)
    {
        return new ProfileDistance(
            profile,
            DistanceMeters(viewer, new GeoPoint(profile.Lat, profile.Lng)),
            ResolveAvailabilityStatus(profile));
    }

    private static string ResolveAvailabilityStatus(ResponderProfileRecord profile)
    {
        if (profile.QualificationStatus != ActiveStatus) return OfflineStatus;

        var campus = Campuses.First(item => item.Code == profile.Campus);
        var profilePoint = new GeoPoint(profile.Lat, profile.Lng);
        return DistanceMeters(profilePoint, campus.Point) > AutoOfflineRadiusMeters
            ? OfflineStatus
            : OnlineStatus;
    }

    private static int DistanceMeters(GeoPoint left, GeoPoint right)
    {
        var lat1 = ToRadians(left.Lat);
        var lat2 = ToRadians(right.Lat);
        var latDelta = ToRadians(right.Lat - left.Lat);
        var lngDelta = ToRadians(right.Lng - left.Lng);

        var a = Math.Sin(latDelta / 2) * Math.Sin(latDelta / 2)
            + Math.Cos(lat1) * Math.Cos(lat2)
            * Math.Sin(lngDelta / 2) * Math.Sin(lngDelta / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return (int)Math.Round(EarthRadiusMeters * c);
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    private static RescuerListItem ToPublicListItem(ProfileDistance item)
    {
        var profile = item.Profile;
        return new RescuerListItem(
            profile.Id,
            profile.Name,
            profile.AvatarUrl,
            profile.Campus,
            Campuses.First(campus => campus.Code == profile.Campus).Name,
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

        return Results.Ok(ApiResponse.Ok(ToPublicDetail(profile)));
    }

    private static RescuerDetailResponse ToPublicDetail(ResponderProfileRecord p)
    {
        return new RescuerDetailResponse(
            p.Id,
            p.Name,
            p.AvatarUrl,
            p.Campus,
            Campuses.First(campus => campus.Code == p.Campus).Name,
            p.Level,
            ResolveAvailabilityStatus(p),
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

    private sealed record CampusInfo(string Code, string Name, GeoPoint Point);

    private readonly record struct GeoPoint(double Lat, double Lng);

    private sealed record ProfileDistance(
        ResponderProfileRecord Profile,
        int Distance,
        string AvailabilityStatus);
}
