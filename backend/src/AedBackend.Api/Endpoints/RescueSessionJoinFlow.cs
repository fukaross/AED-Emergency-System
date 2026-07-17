using AedBackend.Api.WebSockets;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Rescue;

namespace AedBackend.Api.Endpoints;

internal sealed record RescueSessionJoinInput
{
    public required HttpRequest HttpRequest { get; init; }
    public required string HelpId { get; init; }
    public required RescueOptions Options { get; init; }
    public required IResponderStore ResponderStore { get; init; }
    public required SessionEndpointServices Services { get; init; }
    public required RescueSessionBroadcaster Broadcaster { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}

internal sealed record RescueSessionJoinState
{
    public required CertifiedResponderResult Responder { get; init; }
    public required HelpRequestRecord Help { get; init; }
    public required RescueSessionJoinResult Join { get; init; }
    public required DateTimeOffset Now { get; init; }
}

/** Join 用例编排：认证、原子写入、joined 扇出与 published -> responding 派生。 */
internal static class RescueSessionJoinFlow
{
    public static async Task<IResult> ExecuteAsync(RescueSessionJoinInput input)
    {
        var responder = await RequireResponderAsync(input);
        if (responder.Failure is not null) return responder.Failure;

        var help = await input.Services.HelpStore.FindByIdOrRequestNoAsync(
            input.HelpId,
            input.CancellationToken);
        if (help is null) return RescueSessionContext.NotFound();
        if (HelpStatuses.IsClosed(help.Status)) return RescueSessionContext.SessionClosed();

        var persisted = await PersistAsync(input, responder, help);
        if (persisted.Failure is not null) return persisted.Failure;

        var state = persisted.Value!;
        await BroadcastJoinedAsync(input, state);
        var currentHelp = await TransitionHelpAsync(input, state);
        return ToResult(input, state, currentHelp);
    }

    private static Task<CertifiedResponderResult> RequireResponderAsync(RescueSessionJoinInput input)
    {
        return EndpointRescuers.RequireCertifiedResponderAsync(
            input.HttpRequest,
            input.Services.Tokens,
            input.Services.Users,
            input.Services.AccountStatus,
            input.ResponderStore,
            input.CancellationToken);
    }

    private static async Task<RescueSessionWriteResult<RescueSessionJoinState>> PersistAsync(
        RescueSessionJoinInput input,
        CertifiedResponderResult responder,
        HelpRequestRecord help)
    {
        var now = DateTimeOffset.UtcNow;
        var displayName = EndpointRescuers.ResolveDisplayName(responder.Profile!, responder.User!);
        var write = await RescueSessionWriteGuard.ExecuteActiveAsync(
            new RescueSessionActiveWrite<RescueSessionJoinResult>
            {
                HelpId = help.Id,
                Services = input.Services,
                Write = () => input.Services.SessionStore.JoinAsync(
                    new RescueSessionJoinCommand(help.Id, responder.User!.OpenId, displayName, now),
                    input.CancellationToken),
                CancellationToken = input.CancellationToken,
            });
        return write.Failure is not null
            ? new RescueSessionWriteResult<RescueSessionJoinState>(null, write.Failure)
            : new RescueSessionWriteResult<RescueSessionJoinState>(new RescueSessionJoinState
            {
                Responder = responder,
                Help = help,
                Join = write.Value!,
                Now = now,
            }, null);
    }

    private static async Task BroadcastJoinedAsync(RescueSessionJoinInput input, RescueSessionJoinState state)
    {
        if (!state.Join.IsNewParticipant || state.Join.JoinedEvent is null) return;

        var openId = state.Responder.User!.OpenId;
        await input.Broadcaster.BroadcastAsync(
            state.Help.Id,
            "message",
            RescueSessionMapper.ToMessage(state.Join.JoinedEvent));

        var participant = RescueSessionMapper.ToParticipants(state.Join.Session, input.Options, state.Now)
            .First(item => item.OpenId == openId);
        await input.Broadcaster.BroadcastAsync(state.Help.Id, "participant", participant);
    }

    private static async Task<HelpRequestRecord> TransitionHelpAsync(
        RescueSessionJoinInput input,
        RescueSessionJoinState state)
    {
        if (!state.Join.IsNewParticipant
            || state.Join.Session.Participants.Count != 1
            || state.Help.Status != HelpStatuses.Published)
        {
            return state.Help;
        }

        var help = await input.Services.HelpStore.TransitionToRespondingAsync(
            state.Help.Id,
            state.Now,
            input.CancellationToken);
        await input.Broadcaster.BroadcastAsync(
            help.Id,
            "help_status",
            new RescueHelpStatusDto(help.Status));
        return help;
    }

    private static IResult ToResult(
        RescueSessionJoinInput input,
        RescueSessionJoinState state,
        HelpRequestRecord help)
    {
        return Results.Ok(ApiResponse.Ok(RescueSessionMapper.ToSnapshot(
            state.Join.Session,
            help,
            state.Responder.User!.OpenId,
            null,
            input.Options,
            state.Now)));
    }
}
