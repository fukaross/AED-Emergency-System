using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Services.Admin;
using AedBackend.Infrastructure.Admin;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

internal static class AdminAuthEndpoints
{
    private const int MaxFailedLogins = 5;
    private const int MaxActiveSessions = 3;
    private const string SessionLimitCode = "SESSION_LIMIT_REACHED";
    private const string SessionLimitMessage = "已达到最大登录设备数，请先登出其他设备";
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    public static RouteGroupBuilder MapAdminAuthEndpoints(this RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/auth");
        auth.MapPost("/bootstrap", BootstrapAsync);
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/reset-super-admin", ResetSuperAdminAsync);
        auth.MapGet("/me", GetMeAsync);
        auth.MapGet("/sessions", GetSessionsAsync);
        auth.MapDelete("/sessions/{id}", DeleteSessionAsync);
        auth.MapPost("/logout", LogoutAsync);
        return group;
    }

    private static async Task<IResult> BootstrapAsync(
        HttpContext context,
        AdminBootstrapRequest request,
        IAdminAuthorizationService authorization,
        IAdminUserStore users,
        IAdminAuditLogStore auditLogs,
        IPasswordHasher<AdminUserRecord> hasher,
        CancellationToken cancellationToken)
    {
        var legacyFailure = RequireLegacyKey(context, authorization);
        if (legacyFailure is not null) return legacyFailure;

        var validationError = AdminAuthSupport.ValidateCredentials(request.Username, request.Password);
        if (validationError is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validationError, 400);

        if (await users.HasSuperAdminAsync(cancellationToken))
            return EndpointFailures.JsonFailure(ErrorCodes.AdminAlreadyInitialized, "超级管理员已初始化", 409);

        var now = DateTimeOffset.UtcNow;
        var user = AdminAuthSupport.NewAdmin(request.Username, AdminRoles.SuperAdmin, now);
        user = user with { PasswordHash = hasher.HashPassword(user, request.Password) };
        var created = await users.AddAsync(user, cancellationToken);
        await AdminAuthSupport.AppendAuditAsync(
            auditLogs,
            context,
            "admin.bootstrap_created",
            created.Id,
            cancellationToken);
        return Results.Ok(ApiResponse.Ok());
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        AdminLoginRequest request,
        IOptions<AdminOptions> options,
        IAdminUserStore users,
        IPasswordHasher<AdminUserRecord> hasher,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SessionHashSecret))
            return EndpointFailures.JsonFailure(ErrorCodes.InternalServerError, "缺少 Admin:SessionHashSecret 配置", 500);
        var deviceError = AdminAuthSupport.ValidateDeviceId(request.DeviceId);
        if (deviceError is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, deviceError, 400);

        if (!await users.HasSuperAdminAsync(cancellationToken))
            return Results.Json(
                new { code = "SYSTEM_NOT_INITIALIZED", message = "系统尚未初始化" },
                statusCode: 409);

        var normalized = AdminAuthSupport.NormalizeUsername(request.Username);
        var user = await users.FindByNormalizedUsernameAsync(normalized, cancellationToken);
        if (user is null) return InvalidLogin();

        var now = DateTimeOffset.UtcNow;
        if (user.LockedUntil > now) return LockedFailure();
        if (user.Status != AdminStatuses.Active) return InvalidLogin();

        var verified = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verified == PasswordVerificationResult.Failed)
        {
            await RecordFailedLoginAsync(users, user, now, cancellationToken);
            return InvalidLogin();
        }

        var activeSessions = AdminAuthSupport.ActiveSessions(user, now).ToArray();
        var deviceHash = AdminAuthSupport.HashDeviceId(request.DeviceId, options.Value.SessionHashSecret);
        if (AdminAuthSupport.IsNewDeviceOverLimit(activeSessions, deviceHash, MaxActiveSessions))
            return SessionLimitFailure();

        var sessionKey = AdminSessionCrypto.GenerateSessionKey();
        var session = AdminAuthSupport.NewSession(
            sessionKey,
            options.Value.SessionHashSecret,
            deviceHash,
            context,
            now);
        await users.UpdateAsync(
            user.Id,
            current => current with
            {
                FailedLoginCount = 0,
                LockedUntil = null,
                Sessions = AdminAuthSupport.ReplaceDeviceSession(
                    AdminAuthSupport.ActiveSessions(current, now),
                    deviceHash,
                    session),
                UpdatedAt = now
            },
            cancellationToken);

        return Results.Ok(ApiResponse.Ok(
            new AdminLoginResponse(sessionKey, user.Username, user.Role)));
    }

    private static async Task<IResult> ResetSuperAdminAsync(
        HttpContext context,
        AdminResetSuperAdminRequest request,
        IAdminAuthorizationService authorization,
        IAdminUserStore users,
        IAdminAuditLogStore auditLogs,
        IPasswordHasher<AdminUserRecord> hasher,
        CancellationToken cancellationToken)
    {
        var legacyFailure = RequireLegacyKey(context, authorization);
        if (legacyFailure is not null) return legacyFailure;

        var validationError = AdminAuthSupport.ValidateCredentials(request.Username, request.Password);
        if (validationError is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validationError, 400);

        var normalized = AdminAuthSupport.NormalizeUsername(request.Username);
        var user = await users.FindByNormalizedUsernameAsync(normalized, cancellationToken);
        if (user?.Role != AdminRoles.SuperAdmin)
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "超级管理员不存在", 404);

        var now = DateTimeOffset.UtcNow;
        await users.UpdateAsync(
            user.Id,
            current => current with
            {
                PasswordHash = hasher.HashPassword(current, request.Password),
                Sessions = [],
                SessionsRevokedAt = now,
                FailedLoginCount = 0,
                LockedUntil = null,
                UpdatedAt = now
            },
            cancellationToken);
        await AdminAuthSupport.AppendAuditAsync(
            auditLogs,
            context,
            "admin.super_admin_reset",
            user.Id,
            cancellationToken);
        return Results.Ok(ApiResponse.Ok());
    }

    private static async Task<IResult> GetMeAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminSessionAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var principal = auth.Principal!;
        return Results.Ok(ApiResponse.Ok(
            new AdminMeResponse(principal.Id, principal.Username, principal.Role, principal.Status)));
    }

    private static async Task<IResult> GetSessionsAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        IAdminUserStore users,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminSessionAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var user = await users.FindByIdAsync(auth.Principal!.Id, cancellationToken);
        var items = AdminAuthSupport.ActiveSessions(user!, DateTimeOffset.UtcNow)
            .Select(item => AdminAuthSupport.ToSessionItem(item, auth.Principal.SessionId, includeFullIp: false))
            .ToArray();
        return Results.Ok(ApiResponse.Ok(items));
    }

    private static async Task<IResult> DeleteSessionAsync(
        HttpContext context,
        string id,
        IAdminAuthorizationService authorization,
        IAdminUserStore users,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminSessionAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        await RemoveSessionAsync(users, auth.Principal!.Id, id, cancellationToken);
        return Results.Ok(ApiResponse.Ok());
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        IAdminUserStore users,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminSessionAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        await RemoveSessionAsync(users, auth.Principal!.Id, auth.Principal.SessionId, cancellationToken);
        return Results.Ok(ApiResponse.Ok());
    }

    private static IResult? RequireLegacyKey(
        HttpContext context,
        IAdminAuthorizationService authorization)
    {
        return authorization.IsLegacyAdminKey(AdminEndpointHelpers.ReadAdminKey(context.Request))
            ? null
            : EndpointFailures.JsonFailure(ErrorCodes.Unauthorized, "缺少有效的旧管理密钥", 401);
    }

    private static Task<AdminAuthorizationResult> RequireAdminSessionAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        return authorization.RequireAdminSessionAsync(
            AdminEndpointHelpers.ReadAdminKey(context.Request),
            AdminEndpointHelpers.GetClientIp(context),
            AdminEndpointHelpers.GetUserAgent(context.Request),
            cancellationToken);
    }

    private static async Task RecordFailedLoginAsync(
        IAdminUserStore users,
        AdminUserRecord user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await users.UpdateAsync(
            user.Id,
            current =>
            {
                var nextCount = current.FailedLoginCount + 1;
                return current with
                {
                    FailedLoginCount = nextCount,
                    LockedUntil = nextCount >= MaxFailedLogins ? now.Add(LockDuration) : current.LockedUntil,
                    UpdatedAt = now
                };
            },
            cancellationToken);
    }

    private static async Task RemoveSessionAsync(
        IAdminUserStore users,
        string userId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        await users.UpdateAsync(
            userId,
            current => current with
            {
                Sessions = current.Sessions.Where(session => session.Id != sessionId).ToArray(),
                UpdatedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);
    }

    private static IResult InvalidLogin()
    {
        return EndpointFailures.JsonFailure(ErrorCodes.Unauthorized, "用户名或密码错误", 401);
    }

    private static IResult LockedFailure()
    {
        return EndpointFailures.JsonFailure(ErrorCodes.OperationRateLimited, "登录失败次数过多，请稍后再试", 429);
    }

    private static IResult SessionLimitFailure()
    {
        return Results.Json(new { code = SessionLimitCode, message = SessionLimitMessage }, statusCode: 409);
    }
}
