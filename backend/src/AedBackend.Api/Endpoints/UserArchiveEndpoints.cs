using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.User;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

internal static class UserArchiveEndpoints
{
    public static RouteGroupBuilder MapUserArchiveEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/archives", ListArchivesAsync).WithName("ListUserArchives");
        group.MapGet("/archives/{id}", GetArchiveAsync).WithName("GetUserArchive");
        group.MapGet("/archives/{id}/phone", GetArchivePhoneAsync).WithName("GetUserArchivePhone");
        return group;
    }

    private static async Task<IResult> ListArchivesAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IUserAccountStatusService accountStatus,
        IResponderStore responders,
        IOptions<DataOptions> dataOptions,
        [AsParameters] ArchiveQuery query,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await EndpointUsers.RequireActiveUserAsync(
            request,
            new ActiveUserDependencies(tokenService, users, accountStatus),
            ct);
        if (current.Failure is not null) return current.Failure;

        var validationError = query.Validate();
        if (validationError is not null) return ParameterInvalid(validationError);

        var profiles = await responders.GetAllAsync(ct);
        return Results.Ok(ApiResponse.Ok(UserArchiveMapping.ToListResponse(
            profiles, current.User!.OpenId, query)));
    }

    private static async Task<IResult> GetArchiveAsync(
        HttpRequest request,
        string id,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IUserAccountStatusService accountStatus,
        IResponderStore responders,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await EndpointUsers.RequireActiveUserAsync(
            request,
            new ActiveUserDependencies(tokenService, users, accountStatus),
            ct);
        if (current.Failure is not null) return current.Failure;

        var profile = await responders.FindByIdAsync(id, ct);
        if (profile is null) return RescuerNotFound();
        return Results.Ok(ApiResponse.Ok(UserArchiveMapping.ToDetail(profile)));
    }

    private static async Task<IResult> GetArchivePhoneAsync(
        HttpContext context,
        string id,
        string? purpose,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IUserAccountStatusService accountStatus,
        IResponderStore responders,
        IAdminAuditLogStore auditLogs,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await EndpointUsers.RequireActiveUserAsync(
            context.Request,
            new ActiveUserDependencies(tokenService, users, accountStatus),
            ct);
        if (current.Failure is not null) return current.Failure;
        if (string.IsNullOrWhiteSpace(purpose)) return ParameterInvalid("purpose 不能为空");

        var profile = await responders.FindByIdAsync(id, ct);
        if (profile is null) return RescuerNotFound();

        await auditLogs.AppendAsync(CreatePhoneAudit(context, current.User!.OpenId, id, purpose.Trim()), ct);
        return Results.Ok(ApiResponse.Ok(new ArchivePhoneResponse(profile.Phone)));
    }

    private static AdminAuditLogRecord CreatePhoneAudit(
        HttpContext context,
        string openId,
        string archiveId,
        string purpose)
    {
        return new AdminAuditLogRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            EventType = "archive",
            EventName = "archive.view_phone",
            TargetId = archiveId,
            ActorType = "user",
            ActorId = openId,
            ActorName = openId,
            ActorRole = "user",
            Summary = "查看急救档案完整手机号",
            Metadata = new Dictionary<string, object?> { ["purpose"] = purpose },
            IpAddress = AdminEndpointHelpers.GetClientIp(context),
            UserAgent = AdminEndpointHelpers.GetUserAgent(context.Request),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static IResult ParameterInvalid(string message)
    {
        return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, message, 400);
    }

    private static IResult RescuerNotFound()
    {
        return EndpointFailures.JsonFailure(ErrorCodes.RescuerNotFound, "急救员不存在", 404);
    }

    private static IResult FeatureRequiresSqlite()
    {
        return EndpointFailures.JsonFailure(
            ErrorCodes.FeatureRequiresSqlite,
            "Phase 7 用户与志愿者接口仅支持 SQLite 数据源",
            501);
    }

    internal sealed class ArchiveQuery
    {
        private const int MaxPageSize = 100;
        public int? Page { get; init; }
        public int? PageSize { get; init; }
        public string? Keyword { get; init; }
        public string? Level { get; init; }

        public int SafePage => Math.Max(1, Page ?? 1);
        public int SafePageSize => Math.Clamp(PageSize ?? 20, 1, MaxPageSize);

        public string? Validate()
        {
            if (Page is < 1) return "page 必须大于 0";
            return PageSize is < 1 or > MaxPageSize ? "pageSize 必须在 1 到 100 之间" : null;
        }
    }
}
