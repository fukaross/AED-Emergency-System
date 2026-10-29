using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Services.Admin;
using Microsoft.AspNetCore.Identity;

namespace AedBackend.Api.Endpoints;

internal static class AdminManagementEndpoints
{
    public static RouteGroupBuilder MapAdminManagementEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/admins", ListAdminsAsync);
        group.MapGet("/admins/{id}", GetAdminAsync);
        group.MapPost("/admins", CreateAdminAsync);
        group.MapPost("/admins/{id}/password-reset", ResetAdminPasswordAsync);
        group.MapDelete("/admins/{id}", DeleteAdminAsync);
        return group;
    }

    private static async Task<IResult> ListAdminsAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        IAdminUserStore users,
        CancellationToken cancellationToken)
    {
        var auth = await RequireSuperAdminAsync(context, authorization, cancellationToken);
        if (auth.failure is not null) return auth.failure;

        var items = (await users.ListAsync(cancellationToken))
            .Select(user => new AdminAccountListItem(user.Id, user.Username, user.Role, user.Status, user.CreatedAt))
            .ToArray();
        return Results.Ok(ApiResponse.Ok(items));
    }

    private static async Task<IResult> GetAdminAsync(
        HttpContext context,
        string id,
        IAdminAuthorizationService authorization,
        IAdminUserStore users,
        CancellationToken cancellationToken)
    {
        var auth = await RequireSuperAdminAsync(context, authorization, cancellationToken);
        if (auth.failure is not null) return auth.failure;

        var user = await users.FindByIdAsync(id, cancellationToken);
        if (user is null)
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "管理员不存在", 404);

        return Results.Ok(ApiResponse.Ok(ToDetail(user, auth.principal!.SessionId)));
    }

    private static async Task<IResult> CreateAdminAsync(
        HttpContext context,
        AdminCreatePayload payload,
        IAdminAuthorizationService authorization,
        IAdminUserStore users,
        IAdminAuditLogStore auditLogs,
        IPasswordHasher<AdminUserRecord> hasher,
        CancellationToken cancellationToken)
    {
        var auth = await RequireSuperAdminAsync(context, authorization, cancellationToken);
        if (auth.failure is not null) return auth.failure;

        var validationError = ValidatePayload(payload);
        if (validationError is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validationError, 400);

        var now = DateTimeOffset.UtcNow;
        var user = NewAdmin(payload.Username, now);
        user = user with { PasswordHash = hasher.HashPassword(user, payload.Password) };

        try
        {
            var created = await users.AddAsync(user, cancellationToken);
            await auditLogs.AppendAsync(CreateAudit(context, auth.principal, "admin.admin_created", created.Id), cancellationToken);
            return Results.Ok(ApiResponse.Ok());
        }
        catch (InvalidOperationException ex)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, ex.Message, 400);
        }
    }

    private static async Task<IResult> ResetAdminPasswordAsync(
        HttpContext context,
        string id,
        AdminPasswordResetPayload payload,
        IAdminAuthorizationService authorization,
        IAdminUserStore users,
        IAdminAuditLogStore auditLogs,
        IPasswordHasher<AdminUserRecord> hasher,
        CancellationToken cancellationToken)
    {
        var auth = await RequireSuperAdminAsync(context, authorization, cancellationToken);
        if (auth.failure is not null) return auth.failure;
        if (auth.principal!.Id == id)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "不能重置自己的密码", 400);

        var validationError = ValidatePassword(payload.Password);
        if (validationError is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validationError, 400);

        var user = await users.FindByIdAsync(id, cancellationToken);
        if (user is null)
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "管理员不存在", 404);
        if (user.Role != AdminRoles.Admin)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "只能重置普通管理员密码", 400);

        var now = DateTimeOffset.UtcNow;
        await users.UpdateAsync(
            user.Id,
            current => ResetPassword(current, payload.Password, hasher, now),
            cancellationToken);
        await auditLogs.AppendAsync(CreateAudit(context, auth.principal, "admin.password_reset", user.Id), cancellationToken);
        return Results.Ok(ApiResponse.Ok(new AdminPasswordResetResponse(user.Id, user.Username)));
    }

    private static async Task<IResult> DeleteAdminAsync(
        HttpContext context,
        string id,
        IAdminAuthorizationService authorization,
        IAdminUserStore users,
        IAdminAuditLogStore auditLogs,
        CancellationToken cancellationToken)
    {
        var auth = await RequireSuperAdminAsync(context, authorization, cancellationToken);
        if (auth.failure is not null) return auth.failure;

        if (auth.principal!.Id == id)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "不能删除自己", 400);

        try
        {
            await users.DeleteAsync(id, cancellationToken);
            await auditLogs.AppendAsync(CreateAudit(context, auth.principal, "admin.admin_deleted", id), cancellationToken);
            return Results.Ok(ApiResponse.Ok());
        }
        catch (KeyNotFoundException)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "管理员不存在", 404);
        }
    }

    private static async Task<(AdminPrincipal? principal, IResult? failure)> RequireSuperAdminAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var result = await authorization.RequireAdminSessionAsync(
            AdminEndpointHelpers.ReadAdminKey(context.Request),
            AdminEndpointHelpers.GetClientIp(context),
            AdminEndpointHelpers.GetUserAgent(context.Request),
            cancellationToken);

        if (!result.Succeeded) return (null, AdminEndpointHelpers.AuthorizationFailure(result));
        if (result.Principal!.Role != AdminRoles.SuperAdmin) return (null, AdminEndpointHelpers.Forbidden());
        return (result.Principal, null);
    }

    private static AdminUserRecord NewAdmin(string username, DateTimeOffset now)
    {
        var trimmed = username.Trim();
        return new AdminUserRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = trimmed,
            NormalizedUsername = trimmed.ToUpperInvariant(),
            Role = AdminRoles.Admin,
            Status = AdminStatuses.Active,
            CreatedAt = now
        };
    }

    private static AdminAccountDetail ToDetail(
        AdminUserRecord user,
        string currentSessionId)
    {
        var activeSessions = AdminAuthSupport.ActiveSessions(user, DateTimeOffset.UtcNow)
            .Select(session => AdminAuthSupport.ToSessionItem(session, currentSessionId, includeFullIp: true))
            .ToArray();
        return new AdminAccountDetail(
            user.Id,
            user.Username,
            user.Role,
            user.Status,
            user.CreatedAt,
            user.UpdatedAt,
            user.SessionsRevokedAt,
            user.LockedUntil,
            activeSessions);
    }

    private static AdminUserRecord ResetPassword(
        AdminUserRecord current,
        string password,
        IPasswordHasher<AdminUserRecord> hasher,
        DateTimeOffset now)
    {
        return current with
        {
            PasswordHash = hasher.HashPassword(current, password),
            Sessions = [],
            SessionsRevokedAt = now,
            FailedLoginCount = 0,
            LockedUntil = null,
            UpdatedAt = now
        };
    }

    private static string? ValidatePayload(AdminCreatePayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Username)) return "username 不能为空";
        return ValidatePassword(payload.Password);
    }

    private static string? ValidatePassword(string password)
    {
        return string.IsNullOrWhiteSpace(password) || password.Length < 8 ? "password 长度不能少于 8 位" : null;
    }

    private static AdminAuditLogRecord CreateAudit(
        HttpContext context,
        AdminPrincipal? actor,
        string eventName,
        string targetId)
    {
        return AdminEndpointHelpers.CreateAudit(
            "admin",
            eventName,
            actor,
            targetId,
            eventName,
            context);
    }
}
