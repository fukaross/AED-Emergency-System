using System.Text.Json;
using AedBackend.Core.DTOs.Rescue;

namespace AedBackend.Api.WebSockets;

internal sealed record RescueBroadcasterTiming
{
    public required TimeSpan SendTimeout { get; init; }
    public required TimeSpan CloseGracePeriod { get; init; }
    public required TimeSpan ClosedHelpRetention { get; init; }
    public required TimeSpan ClosedHelpPruneInterval { get; init; }
}

internal sealed record RescueBroadcastCommand
{
    public required string HelpId { get; init; }
    public required string Type { get; init; }
    public required object Data { get; init; }
    public DateTimeOffset? ParticipantVersion { get; init; }
}

internal sealed record RescueAuthorizedSendCommand
{
    public required RescueSocketRegistration Target { get; init; }
    public required byte[] Payload { get; init; }
    public RescueBroadcastStamp? Stamp { get; init; }
    public required bool AllowClosed { get; init; }
}

internal static class RescueBroadcastPayload
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static byte[] Serialize(string type, object data) =>
        JsonSerializer.SerializeToUtf8Bytes(new RescueSocketEnvelope(type, data), JsonOptions);

    public static RescueBroadcastStamp? CreateStamp(
        object data,
        DateTimeOffset? participantVersion)
    {
        return data switch
        {
            RescueMessageDto message => new RescueBroadcastStamp(MessageSequence: message.Seq),
            RescueParticipantDto participant => new RescueBroadcastStamp(
                ParticipantOpenId: participant.OpenId,
                ParticipantVersion: participantVersion
                    ?? participant.Location?.ReportedAt
                    ?? participant.JoinedAt),
            _ => null,
        };
    }
}
