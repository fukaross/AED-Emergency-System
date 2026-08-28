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
using Microsoft.AspNetCore.Mvc;
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
        [AsParameters] AdminRescueSessionListEndpointRequest endpoint)
    {
        var cancellationToken = endpoint.CancellationToken;
        var auth = await RequireAdminAsync(
            endpoint.HttpContext,
            endpoint.Authorization,
            cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var query = ValidateQuery(new AdminRescueListQueryInput
        {
            Status = endpoint.Status,
            Page = endpoint.Page,
            PageSize = endpoint.PageSize
        });
        if (query.Failure is not null) return query.Failure;
        var helps = await endpoint.HelpStore.ListOverviewsAsync(
            new HelpRequestOverviewQuery
            {
                Scope = query.Value!.History
                    ? HelpRequestOverviewScope.History
                    : HelpRequestOverviewScope.Active,
                Offset = query.Value.Offset,
                Limit = query.Value.PageSize
            },
            cancellationToken);
        var sessions = (await endpoint.SessionStore.ListOverviewsAsync(
            new RescueSessionOverviewQuery
            {
                HelpRequestIds = helps.Items.Select(help => help.Id).ToArray()
            },
            cancellationToken))
            .ToDictionary(session => session.HelpRequestId);
        var items = helps.Items
            .Select(h => ToListItem(h, sessions.GetValueOrDefault(h.Id)))
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();

        return Results.Ok(ApiResponse.Ok(new AdminRescueSessionListResponse(
            items,
            helps.Total,
            query.Value.Page,
            query.Value.PageSize,
            Math.Max(1, (int)Math.Ceiling(helps.Total / (double)query.Value.PageSize)))));
    }

    private static QueryValidationResult ValidateQuery(AdminRescueListQueryInput input)
    {
        var status = input.Status;
        if (status is not null
            && !string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "history", StringComparison.OrdinalIgnoreCase))
            return QueryValidationResult.Invalid("status 仅支持 active/history");
        if (input.Page is <= 0) return QueryValidationResult.Invalid("page 必须大于 0");
        if (input.PageSize is <= 0 or > 100)
            return QueryValidationResult.Invalid("pageSize 需为 1~100");

        var page = input.Page ?? DefaultPage;
        var pageSize = input.PageSize ?? DefaultPageSize;
        var offset = ((long)page - 1) * pageSize;
        if (offset > int.MaxValue) return QueryValidationResult.Invalid("page 超出支持范围");
        return QueryValidationResult.Success(new AdminRescueListQuery
        {
            History = string.Equals(status, "history", StringComparison.OrdinalIgnoreCase),
            Page = page,
            PageSize = pageSize,
            Offset = (int)offset
        });
    }

    private static async Task<IResult> GetRescueSessionAsync(
        [AsParameters] AdminRescueSessionDetailEndpointRequest endpoint)
    {
        var cancellationToken = endpoint.CancellationToken;
        var auth = await RequireAdminAsync(
            endpoint.HttpContext,
            endpoint.Authorization,
            cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var help = await endpoint.HelpStore.FindByIdOrRequestNoAsync(
            endpoint.HelpId,
            cancellationToken);
        if (help is null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "求助不存在", 404);
        }

        var session = await endpoint.SessionStore.FindByHelpIdAsync(help.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var detail = new AdminRescueSessionDetailResponse(
            help.Id,
            help.RequestNo,
            help.Status,
            new RescueSosDto(help.Lat, help.Lng, help.Address),
            await ResolveRequesterAsync(help, endpoint.UserStore, cancellationToken),
            help.ClosedAt,
            help.CloseNote,
            session is null
                ? []
                : await ResolveParticipantsAsync(
                    session,
                    endpoint.RescueOptions.Value,
                    now,
                    endpoint.UserStore,
                    endpoint.ResponderStore,
                    cancellationToken),
            session?.Messages.Select(RescueSessionMapper.ToMessage).ToArray() ?? [],
            now);
        await endpoint.AuditLogs.AppendAsync(
            CreateViewAudit(endpoint.HttpContext, auth.Principal, help),
            cancellationToken);
        return Results.Ok(ApiResponse.Ok(detail));
    }

    private static AdminRescueSessionListItem ToListItem(
        HelpRequestOverviewRecord help,
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

        var user = help.UserId is null
            ? null
            : await userStore.FindByOpenIdAsync(help.UserId, cancellationToken);
        return new AdminRescueRequesterDto(help.UserId, user?.Name ?? help.UserId ?? "未知用户");
    }

    private static async Task<IReadOnlyList<RescueParticipantDto>> ResolveParticipantsAsync(
        RescueSessionRecord session,
        RescueOptions options,
        DateTimeOffset now,
        IExperienceUserStore userStore,
        IResponderStore responderStore,
        CancellationToken cancellationToken)
    {
        var participants = RescueSessionMapper.ToParticipants(session, options, now);
        var enriched = new List<RescueParticipantDto>(participants.Count);
        foreach (var participant in participants)
        {
            if (!string.IsNullOrWhiteSpace(participant.AvatarUrl))
            {
                enriched.Add(participant);
                continue;
            }

            var user = await userStore.FindByOpenIdAsync(participant.OpenId, cancellationToken);
            var avatarUrl = user?.AvatarUrl;
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                var profile = await responderStore.FindByBoundOpenIdAsync(
                    participant.OpenId,
                    cancellationToken);
                avatarUrl = profile?.AvatarUrl;
            }
            enriched.Add(participant with { AvatarUrl = avatarUrl ?? string.Empty });
        }
        return enriched;
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

    private sealed record AdminRescueListQueryInput
    {
        public string? Status { get; init; }
        public int? Page { get; init; }
        public int? PageSize { get; init; }
    }

    private sealed record AdminRescueListQuery
    {
        public bool History { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int Offset { get; init; }
    }

    private sealed record QueryValidationResult
    {
        public AdminRescueListQuery? Value { get; init; }
        public IResult? Failure { get; init; }

        public static QueryValidationResult Success(AdminRescueListQuery value) => new()
        {
            Value = value
        };

        public static QueryValidationResult Invalid(string message) => new()
        {
            Failure = EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, message, 400)
        };
    }
}
