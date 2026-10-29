using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Knowledge;
using AedBackend.Core.Services.Knowledge;

namespace AedBackend.Api.Endpoints;

public static class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/knowledge")
            .WithTags("Knowledge");

        group.MapGet("/summary", GetSummaryAsync)
            .WithName("GetKnowledgeSummary");

        return app;
    }

    private static async Task<IResult> GetSummaryAsync(
        IKnowledgeStore store,
        CancellationToken ct)
    {
        var summary = await store.GetSummaryAsync(ct);
        var response = new KnowledgeSummaryResponse(
            summary.Items.Select(i => new KnowledgeStatItemResponse(
                i.Key, i.Title, i.Value, i.SortOrder)).ToArray(),
            summary.UpdatedAt);

        return Results.Ok(ApiResponse.Ok(response));
    }
}
