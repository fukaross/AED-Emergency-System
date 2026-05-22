using AedBackend.Core.DTOs.Common;

namespace AedBackend.Core.DTOs.Admin;

public sealed record AdminBootstrapRequest(string Username, string Password);

public sealed record AdminLoginRequest(string Username, string Password);

public sealed record AdminLoginResponse(string AdminKey, string Username, string Role);

public sealed record AdminResetSuperAdminRequest(string Username, string Password);

public sealed record AdminMeResponse(string Id, string Username, string Role, string Status);

public sealed record AdminSessionListItem(
    string Id,
    bool Current,
    string MaskedIpAddress,
    string UserAgentSummary,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset LastSeenAt);

public sealed record AdminDashboardRecentLogItem(
    string Id,
    string EventType,
    string EventName,
    string? ActorName,
    string? Summary,
    DateTimeOffset CreatedAt);

public sealed record AdminDashboardResponse(
    int UsersTotal,
    int HelpRequestsTotal,
    int PendingReviewTotal,
    int TodayNewHelpRequests,
    IReadOnlyList<AdminDashboardRecentLogItem> RecentLogs);

public sealed record AdminHelpListItem(
    string Id,
    string RequestNo,
    string RequestChannel,
    string Content,
    string Type,
    string Status,
    string Address,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    string? CloseNote,
    int ReportersCount,
    int RespondersCount,
    string? LatestEta);

public sealed record AdminHelpListResponse(
    IReadOnlyList<AdminHelpListItem> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record AdminHelpDetailResponse(
    string Id,
    string RequestNo,
    string RequestChannel,
    string Content,
    string Type,
    string Status,
    string Address,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    string? CloseNote,
    int ReportersCount,
    int RespondersCount,
    string? LatestEta,
    IReadOnlyList<string> Images,
    IReadOnlyList<AdminHelpReportRecord> Reports,
    IReadOnlyList<AdminHelpReporter> Reporters,
    IReadOnlyList<AdminHelpTimelineEntry> Timeline,
    IReadOnlyList<AdminHelpResponder> Responders);

public sealed record AdminHelpReportRecord(
    string Id,
    string ReporterOpenId,
    string Content,
    IReadOnlyList<string> Images,
    DateTimeOffset CreatedAt);

public sealed record AdminHelpReporter(
    string OpenId,
    string DisplayName,
    int ReportsCount,
    DateTimeOffset LastReportedAt);

public sealed record AdminHelpTimelineEntry(
    string Type,
    string Description,
    DateTimeOffset CreatedAt);

public sealed record AdminHelpResponder(
    string OpenId,
    string Name,
    string? Eta,
    DateTimeOffset? ArrivedAt);

public sealed record AdminUserListItem(
    string OpenId,
    string Name,
    string? AvatarUrl,
    string? Phone,
    string PrimaryRole,
    string AccountStatus,
    string? ResponderStatus,
    bool Certified,
    string? Level,
    string? AdminRemark,
    DateTimeOffset? UpdatedAt);

public sealed record AdminUserDetail(
    string OpenId,
    string Name,
    string? AvatarUrl,
    string? Phone,
    string PrimaryRole,
    string AccountStatus,
    string? ResponderStatus,
    bool Certified,
    string? Level,
    string? AdminRemark,
    DateTimeOffset? UpdatedAt,
    SchoolProfileDto? SchoolProfile,
    RescuerSummaryDto? RescuerSummary,
    IReadOnlyDictionary<string, object?>? Subscriptions,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record SchoolProfileDto(
    string College,
    string? StudentId,
    string? StaffId,
    string BindingStatus);

public sealed record RescuerSummaryDto(
    string ArchiveProfileId,
    string ResponderProfileId,
    bool Certified,
    string? Level,
    string ResponderType,
    string ResponderStatus,
    DateTimeOffset? CertExpiry,
    string? CurrentCertificateRecordId,
    string? CurrentCertificateNo,
    string? WorkUnit);

public sealed record AdminUserUpdatePayload(
    OptionalValue<string> PrimaryRole,
    OptionalValue<string> AccountStatus,
    OptionalValue<string?> ResponderStatus,
    OptionalValue<bool> Certified,
    OptionalValue<string?> Level,
    OptionalValue<string?> AdminRemark);

public sealed record AdminAccountListItem(
    string Id,
    string Username,
    string Role,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record AdminCreatePayload(string Username, string Password);

public sealed record AdminAuditLogDto(
    string Id,
    string EventType,
    string EventName,
    string? TargetId,
    string? ActorType,
    string? ActorId,
    string? ActorName,
    string? ActorRole,
    string? Summary,
    IReadOnlyDictionary<string, object?>? Metadata,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAt);

public sealed record AdminAuditLogPage(
    IReadOnlyList<AdminAuditLogDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);
