namespace AedBackend.Core.DTOs.Rescuer;

public sealed record RescuerSkillFlags(
    bool Cpr,
    bool Aed,
    bool Stb);

public sealed record RescuerListItem(
    string Id,
    string Name,
    string AvatarUrl,
    string Campus,
    string CampusName,
    string? Level,
    string AvailabilityStatus,
    string QualificationStatus,
    int Distance,
    string PhoneMasked,
    RescuerSkillFlags Skills);

public sealed record RescuerListResponse(
    string Campus,
    string CampusName,
    int ViewerCampusDistance,
    int AutoOfflineRadius,
    IReadOnlyList<RescuerListItem> Items);

public sealed record RescuerDetailResponse(
    string Id,
    string Name,
    string AvatarUrl,
    string Campus,
    string CampusName,
    string? Level,
    string AvailabilityStatus,
    string QualificationStatus,
    string Phone,
    string PhoneMasked,
    string? CertificateNoMasked,
    string? WorkUnit,
    RescuerSkillFlags Skills);
