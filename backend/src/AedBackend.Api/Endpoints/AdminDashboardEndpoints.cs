using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Api.Endpoints;

internal static class AdminDashboardEndpoints
{
    public static RouteGroupBuilder MapAdminDashboardEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/dashboard", GetDashboardAsync);
        return group;
    }

    private static async Task<IResult> GetDashboardAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        IExperienceUserStore users,
        IResponderStore responders,
        IHelpRequestStore helpRequests,
        IAdminAuditLogStore auditLogs,
        CancellationToken cancellationToken)
    {
        var auth = await authorization.RequireAdminSessionAsync(
            AdminEndpointHelpers.ReadAdminKey(context.Request),
            AdminEndpointHelpers.GetClientIp(context),
            AdminEndpointHelpers.GetUserAgent(context.Request),
            cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var userList = await users.ListUsersAsync(cancellationToken);
        var responderList = await responders.GetAllAsync(cancellationToken);
        var helps = await helpRequests.ListAllAsync(cancellationToken);
        var logs = await auditLogs.ListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var today = now.UtcDateTime.Date;
        var response = new AdminDashboardResponse(
            userList.Count,
            helps.Count,
            helps.Count(item => item.Status == HelpStatuses.PendingReview),
            helps.Count(item => item.CreatedAt.UtcDateTime.Date == today),
            logs.Take(10).Select(AdminEndpointHelpers.ToRecentLog).ToArray(),
            AdminDashboardChartAggregator.Build(userList, responderList, helps, now));

        return Results.Ok(ApiResponse.Ok(response));
    }
}
