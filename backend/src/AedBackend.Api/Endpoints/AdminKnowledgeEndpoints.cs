using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Knowledge;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Knowledge;

namespace AedBackend.Api.Endpoints;

internal static class AdminKnowledgeEndpoints
{
    public static RouteGroupBuilder MapAdminKnowledgeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/knowledge-summary", GetSummaryAsync).WithName("AdminGetKnowledgeSummary");
        group.MapPut("/knowledge-summary", UpdateSummaryAsync).WithName("AdminUpdateKnowledgeSummary");
        return group;
    }

    private static Task<AdminAuthorizationResult> RequireAdminAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        CancellationToken ct)
    {
        return authorization.RequireAdminSessionAsync(
            AdminEndpointHelpers.ReadAdminKey(context.Request),
            AdminEndpointHelpers.GetClientIp(context),
            AdminEndpointHelpers.GetUserAgent(context.Request),
            ct);
    }

    private static async Task<IResult> GetSummaryAsync(
        HttpContext context,
        IKnowledgeStore store,
        IAdminAuthorizationService authorization,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var summary = await store.GetSummaryAsync(ct);
        return Results.Ok(ApiResponse.Ok(summary));
    }

    private static async Task<IResult> UpdateSummaryAsync(
        HttpContext context,
        UpdateKnowledgeSummaryRequest body,
        IKnowledgeStore store,
        IAdminAuthorizationService authorization,
        IAdminAuditLogStore auditLogStore,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var summary = new KnowledgeSummary
        {
            Items = body.Items.Select(i => new KnowledgeStatItem
            {
                Key = i.Key,
                Title = i.Title,
                Value = i.Value,
                SortOrder = i.SortOrder
            }).ToList()
        };

        await store.SaveSummaryAsync(summary, ct);

        await auditLogStore.AppendAsync(AdminEndpointHelpers.CreateAudit(
            "KnowledgeSummary", "knowledge-summary.update", auth.Principal, "summary",
            "更新课堂统计横幅数据", context), ct);

        return Results.Ok(ApiResponse.Ok());
    }
}
