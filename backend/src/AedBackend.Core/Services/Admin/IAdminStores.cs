using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Admin;

namespace AedBackend.Core.Services.Admin;

public interface IAdminUserStore
{
    Task<AdminUserRecord?> FindByNormalizedUsernameAsync(
        string normalizedUsername,
        CancellationToken cancellationToken);

    Task<AdminUserRecord?> FindByIdAsync(
        string id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminUserRecord>> ListAsync(CancellationToken cancellationToken);

    Task<bool> HasSuperAdminAsync(CancellationToken cancellationToken);

    Task<AdminUserRecord> AddAsync(AdminUserRecord user, CancellationToken cancellationToken);

    Task<AdminUserRecord> UpdateAsync(
        string id,
        Func<AdminUserRecord, AdminUserRecord> update,
        CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}

public interface IAdminUserProfileStore
{
    Task<AdminUserProfileRecord?> FindAsync(
        string openId,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, AdminUserProfileRecord>> ListAsync(
        CancellationToken cancellationToken);

    Task<AdminUserProfileRecord> UpsertAsync(
        string openId,
        Func<AdminUserProfileRecord?, AdminUserProfileRecord> update,
        CancellationToken cancellationToken);
}

public interface IAdminAuditLogStore
{
    Task AppendAsync(AdminAuditLogRecord record, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminAuditLogRecord>> ListAsync(CancellationToken cancellationToken);
}

public interface IAdminAuthorizationService
{
    Task<AdminAuthorizationResult> RequireAdminSessionAsync(
        string? adminKey,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);

    Task<AdminAuthorizationResult> RequireLegacyOrAdminAsync(
        string? adminKey,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);

    bool IsLegacyAdminKey(string? adminKey);
}

public interface IAdminUserService
{
    Task<PaginatedData<AdminUserListItem>> ListUsersAsync(
        AdminUserQuery query,
        CancellationToken cancellationToken);

    Task<AdminUserDetail?> GetUserAsync(
        string openId,
        CancellationToken cancellationToken);

    Task<AdminUserDetail> UpdateUserAsync(
        string openId,
        AdminUserUpdatePayload payload,
        CancellationToken cancellationToken);
}

public sealed record AdminAuthorizationResult(
    bool Succeeded,
    AdminPrincipal? Principal,
    int StatusCode,
    int ErrorCode,
    string ErrorMessage)
{
    public static AdminAuthorizationResult Success(AdminPrincipal principal)
    {
        return new AdminAuthorizationResult(true, principal, 200, 0, "success");
    }

    public static AdminAuthorizationResult Failure(
        int statusCode,
        int errorCode,
        string errorMessage)
    {
        return new AdminAuthorizationResult(false, null, statusCode, errorCode, errorMessage);
    }
}

public sealed record AdminUserQuery(
    int? Page,
    int? PageSize,
    string? Keyword,
    string? PrimaryRole,
    string? AccountStatus,
    string? ResponderStatus,
    string? Level);
