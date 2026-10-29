using AedBackend.Api.WebSockets;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Rescue;
using AedBackend.Infrastructure.Rescue;

namespace AedBackend.Api.Endpoints;

internal sealed record RescueSessionJoinInput
{
    public required HttpRequest HttpRequest { get; init; }
    public required string HelpId { get; init; }
    public required RescueOptions Options { get; init; }
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
    public required bool HelpStatusChanged { get; init; }
}

internal sealed record RescueSessionHelpTransitionInput(
    HelpRequestRecord Help,
    RescueSessionJoinResult Join,
    DateTimeOffset Now);

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

        var persisted = await PersistAsync(input, responder, help);
        if (persisted.Failure is not null) return persisted.Failure;

        var state = persisted.Value!;
        await BroadcastJoinedAsync(input, state);
        await BroadcastHelpStatusAsync(input, state);
        return ToResult(input, state);
    }

    private static Task<CertifiedResponderResult> RequireResponderAsync(RescueSessionJoinInput input)
    {
        return input.Services.Authorizer.RequireCertifiedResponderAsync(
            input.HttpRequest,
            input.CancellationToken);
    }

    private static async Task<RescueSessionWriteResult<RescueSessionJoinState>> PersistAsync(
        RescueSessionJoinInput input,
        CertifiedResponderResult responder,
        HelpRequestRecord help)
    {
        using var lease = await input.Services.Lifecycle.EnterAsync(
            help.Id,
            input.CancellationToken);
        var currentHelp = await input.Services.HelpStore.FindByIdOrRequestNoAsync(
            help.Id,
            input.CancellationToken);
        if (currentHelp is null)
            return new RescueSessionWriteResult<RescueSessionJoinState>(null, RescueSessionContext.NotFound());
        if (HelpStatuses.IsClosed(currentHelp.Status))
            return new RescueSessionWriteResult<RescueSessionJoinState>(null, RescueSessionContext.SessionClosed());

        var now = DateTimeOffset.UtcNow;
        var displayName = EndpointRescuers.ResolveDisplayName(responder.Profile!, responder.User!);
        var write = await RescueSessionWriteGuard.ExecuteAsync(() =>
            input.Services.SessionStore.JoinAsync(
                new RescueSessionJoinCommand
                {
                    HelpRequestId = help.Id,
                    OpenId = responder.User!.OpenId,
                    DisplayName = displayName,
                    AvatarUrl = ResolveAvatarUrl(responder),
                    Now = now
                },
                input.CancellationToken));
        if (write.Failure is not null)
            return new RescueSessionWriteResult<RescueSessionJoinState>(null, write.Failure);

        var transition = await EnsureRespondingAsync(
            input,
            new RescueSessionHelpTransitionInput(currentHelp, write.Value!, now));
        return new RescueSessionWriteResult<RescueSessionJoinState>(new RescueSessionJoinState
        {
            Responder = responder,
            Help = transition.Help,
            Join = write.Value!,
            Now = now,
            HelpStatusChanged = transition.Changed,
        }, null);
    }

    private static string ResolveAvatarUrl(CertifiedResponderResult responder)
    {
        if (!string.IsNullOrWhiteSpace(responder.User?.AvatarUrl))
            return responder.User.AvatarUrl;
        return responder.Profile?.AvatarUrl ?? string.Empty;
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
        await input.Broadcaster.BroadcastAsync(new RescueBroadcastCommand
        {
            HelpId = state.Help.Id,
            Type = "participant",
            Data = participant,
            ParticipantVersion = state.Join.JoinedEvent.CreatedAt
        });
    }

    private static async Task<(HelpRequestRecord Help, bool Changed)> EnsureRespondingAsync(
        RescueSessionJoinInput input,
        RescueSessionHelpTransitionInput transition)
    {
        if (transition.Join.Session.Participants.Count == 0
            || transition.Help.Status != HelpStatuses.Published)
        {
            return (transition.Help, false);
        }

        var help = await input.Services.HelpStore.TransitionToRespondingAsync(
            transition.Help.Id,
            transition.Now,
            input.CancellationToken);
        return (help, help.Status != transition.Help.Status);
    }

    private static Task BroadcastHelpStatusAsync(
        RescueSessionJoinInput input,
        RescueSessionJoinState state)
    {
        if (!state.HelpStatusChanged) return Task.CompletedTask;

        return input.Broadcaster.BroadcastAsync(
            state.Help.Id,
            "help_status",
            new RescueHelpStatusDto(state.Help.Status));
    }

    private static IResult ToResult(
        RescueSessionJoinInput input,
        RescueSessionJoinState state)
    {
        return Results.Ok(ApiResponse.Ok(RescueSessionMapper.ToSnapshot(
            new RescueSnapshotMappingInput
            {
                Session = state.Join.Session,
                Help = state.Help,
                CurrentOpenId = state.Responder.User!.OpenId,
                AfterSeq = null,
                Context = new RescueMappingContext(input.Options, state.Now),
            })));
    }
}
