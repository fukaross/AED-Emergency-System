using AedBackend.Core.DTOs.Rescue;

namespace AedBackend.Core.DTOs.Admin;

public sealed record AdminRescueSessionListItem(
    string HelpId,
    string RequestNo,
    string HelpStatus,
    string Type,
    string Address,
    DateTimeOffset CreatedAt,
    int ParticipantsCount,
    int MessagesCount,
    DateTimeOffset? LastActivityAt);

public sealed record AdminRescueSessionListResponse(
    IReadOnlyList<AdminRescueSessionListItem> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record AdminRescueRequesterDto(string? OpenId, string DisplayName);

public sealed record AdminRescueSessionDetailResponse(
    string HelpId,
    string RequestNo,
    string HelpStatus,
    RescueSosDto Sos,
    AdminRescueRequesterDto Requester,
    DateTimeOffset? ClosedAt,
    string? CloseNote,
    IReadOnlyList<RescueParticipantDto> Participants,
    IReadOnlyList<RescueMessageDto> Messages,
    DateTimeOffset ServerTime);
