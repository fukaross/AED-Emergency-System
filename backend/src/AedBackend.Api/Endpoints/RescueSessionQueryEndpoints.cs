using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Rescue;
using Microsoft.AspNetCore.Mvc;

namespace AedBackend.Api.Endpoints;

internal static class RescueSessionQueryEndpoints
{
    public static IEndpointRouteBuilder MapRescueSessionQueryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/help")
            .WithTags("Rescue")
            .MapGet("/{id}/rescue-view", GetRescueViewAsync)
            .WithName("GetRescueView");
        app.MapGroup("/api/rescue-sessions")
            .WithTags("Rescue")
            .MapGet("/mine", ListMineAsync)
            .WithName("ListMyRescueSessions");
        return app;
    }

    private static async Task<IResult> GetRescueViewAsync(
        [AsParameters] RescueSessionEndpointRequest endpoint)
    {
        var context = await endpoint.Authorizer.RequireRescueViewAsync(
            endpoint.HttpRequest,
            endpoint.Id!,
            endpoint.CancellationToken);
        if (context.Failure is not null) return context.Failure;

        var now = DateTimeOffset.UtcNow;
        return Results.Ok(ApiResponse.Ok(RescueSessionMapper.ToRescueView(
            new RescueViewMappingInput
            {
                Help = context.Help!,
                Session = context.Session,
                Context = new RescueMappingContext(endpoint.RescueOptions.Value, now),
            })));
    }

    private static async Task<IResult> ListMineAsync(
        [AsParameters] RescueSessionEndpointRequest endpoint)
    {
        var cancellationToken = endpoint.CancellationToken;
        var responder = await endpoint.Authorizer.RequireCertifiedResponderAsync(
            endpoint.HttpRequest,
            cancellationToken);
        if (responder.Failure is not null) return responder.Failure;

        var helps = await endpoint.HelpStore.ListOverviewsAsync(
            new HelpRequestOverviewQuery { Scope = HelpRequestOverviewScope.Active },
            cancellationToken);
        var sessions = (await endpoint.SessionStore.ListOverviewsAsync(
            new RescueSessionOverviewQuery
            {
                HelpRequestIds = helps.Items.Select(help => help.Id).ToArray(),
                ViewerOpenId = responder.User!.OpenId
            },
            cancellationToken))
            .ToDictionary(session => session.HelpRequestId);
        var items = helps.Items
            .Select(help => new
            {
                Item = ToMineItem(sessions.GetValueOrDefault(help.Id), help),
                help.CreatedAt,
            })
            .OrderByDescending(candidate => candidate.Item.UnreadCount > 0)
            .ThenByDescending(candidate => candidate.Item.LastActivityAt ?? candidate.CreatedAt)
            .Select(candidate => candidate.Item)
            .ToArray();
        return Results.Ok(ApiResponse.Ok(new RescueMineResponse(items)));
    }

    private static RescueMineItemDto ToMineItem(
        RescueSessionOverviewRecord? session,
        HelpRequestOverviewRecord help)
    {
        var viewer = session?.Viewer;
        return new RescueMineItemDto(
            help.Id,
            help.RequestNo,
            help.Status,
            help.Type,
            help.Address,
            viewer is not null,
            viewer?.JoinedAt,
            viewer?.UnreadCount ?? 0,
            session?.LastActivityAt);
    }
}
