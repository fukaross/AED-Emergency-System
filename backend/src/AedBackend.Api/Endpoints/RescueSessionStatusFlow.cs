using AedBackend.Api.WebSockets;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Rescue;

namespace AedBackend.Api.Endpoints;

internal sealed record RescueSessionStatusInput
{
    public required HttpRequest HttpRequest { get; init; }
    public required string HelpId { get; init; }
    public required SendRescueStatusEventRequest Request { get; init; }
    public required RescueOptions Options { get; init; }
    public required SessionEndpointServices Services { get; init; }
    public required RescueSessionBroadcaster Broadcaster { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}

internal sealed record RescueSessionStatusOutput
{
    public required RescueSessionStatusAppendResult Result { get; init; }
    public required RescueParticipantState Updated { get; init; }
    public RescueMessageDto? EventMessage { get; init; }
}

/** 状态事件用例编排：参与者鉴权、原子去抖写入、投影与 WS 扇出。 */
internal static class RescueSessionStatusFlow
{
    public static async Task<IResult> ExecuteAsync(RescueSessionStatusInput input)
    {
        var context = await RescueSessionContext.RequireParticipantAsync(
            input.HttpRequest,
            input.HelpId,
            input.Services,
            input.CancellationToken);
        if (context.Failure is not null) return context.Failure;
        if (!IsValid(input.Request)) return InvalidStatusEvent();

        var write = await PersistAsync(input, context);
        if (write.Failure is not null) return write.Failure;

        var output = Project(write.Value!, context.User!.OpenId);
        await BroadcastAsync(input, context, output);
        return Results.Ok(ApiResponse.Ok(new RescueStatusEventResponse(
            output.Result.Appended,
            output.EventMessage,
            new RescueMyStateDto(
                context.User.OpenId,
                output.Updated.TakenAed,
                output.Updated.Arrived))));
    }

    private static bool IsValid(SendRescueStatusEventRequest request)
    {
        return request.EventType is RescueEventTypes.TakenAed or RescueEventTypes.Arrived
            && request.Action is RescueEventActions.Mark or RescueEventActions.Revoke;
    }

    private static IResult InvalidStatusEvent()
    {
        return EndpointFailures.JsonFailure(
            ErrorCodes.ParameterInvalid,
            "eventType 仅支持 taken_aed / arrived，action 仅支持 mark / revoke",
            400);
    }

    private static Task<RescueSessionWriteResult<RescueSessionStatusAppendResult>> PersistAsync(
        RescueSessionStatusInput input,
        ParticipantContextResult context)
    {
        return RescueSessionWriteGuard.ExecuteAsync(() =>
            input.Services.SessionStore.AppendStatusEventAsync(
                new RescueSessionStatusCommand
                {
                    HelpRequestId = context.Help!.Id,
                    SenderOpenId = context.User!.OpenId,
                    SenderName = context.Participant!.DisplayName,
                    EventType = input.Request.EventType!,
                    Action = input.Request.Action!,
                    Now = DateTimeOffset.UtcNow,
                },
                input.CancellationToken));
    }

    private static RescueSessionStatusOutput Project(
        RescueSessionStatusAppendResult result,
        string openId)
    {
        return new RescueSessionStatusOutput
        {
            Result = result,
            Updated = RescueSessionProjection.FoldParticipantStates(result.Session)[openId],
            EventMessage = result.Event is null ? null : RescueSessionMapper.ToMessage(result.Event),
        };
    }

    private static async Task BroadcastAsync(
        RescueSessionStatusInput input,
        ParticipantContextResult context,
        RescueSessionStatusOutput output)
    {
        if (!output.Result.Appended) return;

        await input.Broadcaster.BroadcastAsync(context.Help!.Id, "message", output.EventMessage!);
        var participant = RescueSessionMapper.ToParticipants(
            output.Result.Session,
            input.Options,
            DateTimeOffset.UtcNow).First(item => item.OpenId == context.User!.OpenId);
        await input.Broadcaster.BroadcastAsync(context.Help.Id, "participant", participant);
    }
}
