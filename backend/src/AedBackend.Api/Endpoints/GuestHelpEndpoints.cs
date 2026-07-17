using AedBackend.Api.WebSockets;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Help;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;

namespace AedBackend.Api.Endpoints;

/** 游客求助查询与关单端点，保持无登录态的 followupCode / closeToken 边界。 */
internal static class GuestHelpEndpoints
{
    internal static async Task<IResult> QueryAsync(
        GuestHelpLookupRequest request,
        IHelpRequestStore helpStore,
        CancellationToken cancellationToken)
    {
        var record = await helpStore.FindByIdOrRequestNoAsync(request.RequestNo, cancellationToken);
        if (record?.GuestSession?.FollowupCode != request.FollowupCode)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "求助不存在", 404);
        }

        return Results.Ok(ApiResponse.Ok(HelpResponseMapper.ToGuestLookup(record)));
    }

    internal static async Task<IResult> CloseAsync(
        GuestCloseHelpRequest request,
        IHelpRequestStore helpStore,
        IRescueSessionStore sessionStore,
        RescueSessionBroadcaster broadcaster,
        CancellationToken cancellationToken)
    {
        var record = await helpStore.FindByIdOrRequestNoAsync(request.RequestNo, cancellationToken);
        if (record?.GuestSession?.CloseToken != request.CloseToken)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "求助不存在", 404);
        }

        return await HelpEndpoints.UpdateHelpAsync(
            async () =>
            {
                var closed = await helpStore.CloseAsync(
                    record.Id,
                    HelpStatuses.FalseAlarm,
                    request.Note,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
                await sessionStore.CloseSessionAsync(closed.Id, cancellationToken);
                await broadcaster.BroadcastAsync(closed.Id, "help_status", new RescueHelpStatusDto(closed.Status));
                await broadcaster.CloseAllAsync(closed.Id, "help closed");
                return closed;
            },
            updated => Results.Ok(ApiResponse.Ok(new GuestCloseHelpResponse(
                updated.RequestNo,
                HelpStatuses.FalseAlarm,
                updated.ClosedAt!.Value))));
    }
}
