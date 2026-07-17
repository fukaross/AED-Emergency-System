using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Rescue;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

internal static class AdminRescueSessionEndpoints
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;

    public static RouteGroupBuilder MapAdminRescueSessionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/rescue-sessions", ListRescueSessionsAsync);
        group.MapGet("/rescue-sessions/{helpId}", GetRescueSessionAsync);
        return group;
    }

    private static async Task<IResult> ListRescueSessionsAsync(
        HttpContext context,
        string? status,
        int? page,
        int? pageSize,
        IAdminAuthorizationService authorization,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var history = string.Equals(status, "history", StringComparison.OrdinalIgnoreCase);
        var helps = await helpStore.ListAllAsync(cancellationToken);
        var sessions = (await sessionStore.ListOverviewsAsync(null, cancellationToken))
            .ToDictionary(session => session.HelpRequestId);
        var items = helps
            .Where(h => history ? HelpStatuses.IsClosed(h.Status) : HelpStatuses.Active.Contains(h.Status))
            .Select(h => ToListItem(h, sessions.GetValueOrDefault(h.Id)))
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();

        var safePage = Math.Max(DefaultPage, page ?? DefaultPage);
        var safePageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, 100);
        var paged = items.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToArray();
        return Results.Ok(ApiResponse.Ok(new AdminRescueSessionListResponse(
            paged,
            items.Length,
            safePage,
            safePageSize,
            Math.Max(1, (int)Math.Ceiling(items.Length / (double)safePageSize)))));
    }

    private static async Task<IResult> GetRescueSessionAsync(
        HttpContext context,
        string helpId,
        IAdminAuthorizationService authorization,
        IOptions<RescueOptions> rescueOptions,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
        IExperienceUserStore userStore,
        IAdminAuditLogStore auditLogs,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var help = await helpStore.FindByIdOrRequestNoAsync(helpId, cancellationToken);
        if (help is null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "求助不存在", 404);
        }

        var session = await sessionStore.FindByHelpIdAsync(help.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var detail = new AdminRescueSessionDetailResponse(
            help.Id,
            help.RequestNo,
            help.Status,
            new RescueSosDto(help.Lat, help.Lng, help.Address),
            await ResolveRequesterAsync(help, userStore, cancellationToken),
            help.ClosedAt,
            help.CloseNote,
            session is null ? [] : RescueSessionMapper.ToParticipants(session, rescueOptions.Value, now),
            session?.Messages.Select(RescueSessionMapper.ToMessage).ToArray() ?? [],
            now);
        await auditLogs.AppendAsync(CreateViewAudit(context, auth.Principal, help), cancellationToken);
        return Results.Ok(ApiResponse.Ok(detail));
    }

    private static AdminRescueSessionListItem ToListItem(
        HelpRequestRecord help,
        RescueSessionOverviewRecord? session)
    {
        return new AdminRescueSessionListItem(
            help.Id,
            help.RequestNo,
            help.Status,
            help.Type,
            help.Address,
            help.CreatedAt,
            session?.ParticipantCount ?? 0,
            session?.MessageCount ?? 0,
            session?.LastActivityAt);
    }

    private static async Task<AdminRescueRequesterDto> ResolveRequesterAsync(
        HelpRequestRecord help,
        IExperienceUserStore userStore,
        CancellationToken cancellationToken)
    {
        if (help.RequestChannel == "guest")
        {
            return new AdminRescueRequesterDto(null, help.GuestSession?.GuestName ?? "游客");
        }

        var users = await userStore.ListUsersAsync(cancellationToken);
        var user = users.FirstOrDefault(u => u.OpenId == help.UserId);
        return new AdminRescueRequesterDto(help.UserId, user?.Name ?? help.UserId ?? "未知用户");
    }

    private static Task<AdminAuthorizationResult> RequireAdminAsync(
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

    private static Core.Models.Admin.AdminAuditLogRecord CreateViewAudit(
        HttpContext context,
        Core.Models.Admin.AdminPrincipal? actor,
        HelpRequestRecord help)
    {
        return AdminEndpointHelpers.CreateAudit(
            "rescue_session",
            "rescue_session.viewed",
            actor,
            help.Id,
            $"查看急救会话 {help.RequestNo}",
            context,
            new Dictionary<string, object?> { ["helpStatus"] = help.Status });
    }
}
