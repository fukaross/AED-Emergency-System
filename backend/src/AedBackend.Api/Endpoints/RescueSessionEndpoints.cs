using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Rescue;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Api.WebSockets;
using AedBackend.Infrastructure.Rescue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

public static class RescueSessionEndpoints
{
    private const int MessageTextMaxLength = 500;

    public static IEndpointRouteBuilder MapRescueSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var helpGroup = app.MapGroup("/api/help").WithTags("Rescue");
        helpGroup.MapPost("/{id}/session/join", JoinSessionAsync).WithName("JoinRescueSession");
        helpGroup.MapGet("/{id}/session", GetSessionAsync).WithName("GetRescueSession");
        helpGroup.MapPost("/{id}/session/messages", PostMessageAsync).WithName("PostRescueMessage");
        helpGroup.MapPost("/{id}/session/status-events", PostStatusEventAsync).WithName("PostRescueStatusEvent");
        helpGroup.MapPut("/{id}/session/location", ReportLocationAsync).WithName("ReportRescueLocation");
        app.MapRescueSessionQueryEndpoints();
        return app;
    }

    private static async Task<IResult> JoinSessionAsync(
        [AsParameters] RescueSessionEndpointRequest endpoint)
    {
        return await RescueSessionJoinFlow.ExecuteAsync(new RescueSessionJoinInput
        {
            HttpRequest = endpoint.HttpRequest,
            HelpId = endpoint.Id!,
            Options = endpoint.RescueOptions.Value,
            Services = endpoint.ToServices(),
            Broadcaster = endpoint.Broadcaster,
            CancellationToken = endpoint.CancellationToken,
        });
    }

    private static async Task<IResult> GetSessionAsync(
        [AsParameters] RescueSessionEndpointRequest endpoint,
        long? afterSeq)
    {
        var services = endpoint.ToServices();
        var cancellationToken = endpoint.CancellationToken;
        var context = await services.Authorizer.RequireParticipantAsync(
            endpoint.HttpRequest,
            endpoint.Id!,
            cancellationToken);
        if (context.Failure is not null) return context.Failure;

        var now = DateTimeOffset.UtcNow;
        var snapshot = RescueSessionMapper.ToSnapshot(new RescueSnapshotMappingInput
        {
            Session = context.Session!,
            Help = context.Help!,
            CurrentOpenId = context.User!.OpenId,
            AfterSeq = afterSeq,
            Context = new RescueMappingContext(endpoint.RescueOptions.Value, now),
        });
        // 隐式已读：lastReadSeq 推进到当前 latestSeq，响应中的 me 同步反映新值（BackEnd.md §4.5A）
        var write = await RescueSessionWriteGuard.ExecuteActiveAsync(
            new RescueSessionActiveWrite<long>
            {
                HelpId = context.Help!.Id,
                Services = services,
                Write = () => services.SessionStore.MarkReadAsync(
                    new RescueSessionReadCommand
                    {
                        HelpRequestId = context.Help.Id,
                        OpenId = context.User.OpenId,
                        LastReadSeq = snapshot.LatestSeq,
                    },
                    cancellationToken),
                CancellationToken = cancellationToken,
            });
        if (write.Failure is not null) return write.Failure;
        return Results.Ok(ApiResponse.Ok(snapshot with
        {
            Me = snapshot.Me with { LastReadSeq = snapshot.LatestSeq },
        }));
    }

    private static async Task<IResult> PostMessageAsync(
        [AsParameters] RescueSessionEndpointRequest endpoint,
        SendRescueMessageRequest request)
    {
        var cancellationToken = endpoint.CancellationToken;
        var services = endpoint.ToServices();
        var context = await services.Authorizer.RequireParticipantAsync(
            endpoint.HttpRequest,
            endpoint.Id!,
            cancellationToken);
        if (context.Failure is not null) return context.Failure;

        var text = request.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > MessageTextMaxLength)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "text 需为 1~500 字符", 400);
        }

        var write = await RescueSessionWriteGuard.ExecuteActiveAsync(
            new RescueSessionActiveWrite<RescueSessionMessageRecord>
            {
                HelpId = context.Help!.Id,
                Services = services,
                Write = () => services.SessionStore.AppendMessageAsync(
                    new RescueTextMessageCommand
                    {
                        HelpRequestId = context.Help.Id,
                        SenderOpenId = context.User!.OpenId,
                        SenderName = context.Participant!.DisplayName,
                        Text = text,
                        Now = DateTimeOffset.UtcNow
                    },
                    cancellationToken),
                CancellationToken = cancellationToken,
            });
        if (write.Failure is not null) return write.Failure;
        var message = RescueSessionMapper.ToMessage(write.Value!);
        await endpoint.Broadcaster.BroadcastAsync(context.Help!.Id, "message", message);
        return Results.Ok(ApiResponse.Ok(message));
    }

    private static async Task<IResult> PostStatusEventAsync(
        [AsParameters] RescueSessionEndpointRequest endpoint,
        SendRescueStatusEventRequest request)
    {
        return await RescueSessionStatusFlow.ExecuteAsync(new RescueSessionStatusInput
        {
            HttpRequest = endpoint.HttpRequest,
            HelpId = endpoint.Id!,
            Request = request,
            Options = endpoint.RescueOptions.Value,
            Services = endpoint.ToServices(),
            Broadcaster = endpoint.Broadcaster,
            CancellationToken = endpoint.CancellationToken,
        });
    }

    private static async Task<IResult> ReportLocationAsync(
        [AsParameters] RescueSessionEndpointRequest endpoint,
        ReportRescueLocationRequest request)
    {
        var cancellationToken = endpoint.CancellationToken;
        var services = endpoint.ToServices();
        var context = await services.Authorizer.RequireParticipantAsync(
            endpoint.HttpRequest,
            endpoint.Id!,
            cancellationToken);
        if (context.Failure is not null) return context.Failure;

        var validationFailure = ValidateLocation(request);
        if (validationFailure is not null) return validationFailure;
        return await PersistLocationAsync(new RescueLocationEndpointInput
        {
            Endpoint = endpoint,
            Services = services,
            Context = context,
            Request = request
        });
    }

    private static async Task<IResult> PersistLocationAsync(RescueLocationEndpointInput input)
    {
        var cancellationToken = input.Endpoint.CancellationToken;
        var reportedAt = DateTimeOffset.MinValue;
        var write = await RescueSessionWriteGuard.ExecuteActiveAsync(
            new RescueSessionActiveWrite<RescueSessionParticipantRecord>
            {
                HelpId = input.Context.Help!.Id,
                Services = input.Services,
                Write = () =>
                {
                    reportedAt = DateTimeOffset.UtcNow;
                    return input.Services.SessionStore.UpdateLocationAsync(
                        new RescueSessionLocationCommand
                        {
                            HelpRequestId = input.Context.Help.Id,
                            OpenId = input.Context.User!.OpenId,
                            Lat = input.Request.Lat!.Value,
                            Lng = input.Request.Lng!.Value,
                            Now = reportedAt
                        },
                        cancellationToken);
                },
                CancellationToken = cancellationToken,
            });
        if (write.Failure is not null) return write.Failure;
        var participantRecord = write.Value!;
        var state = RescueSessionProjection.FoldParticipantStates(
            input.Context.Session!)[input.Context.User!.OpenId];
        var participant = RescueSessionMapper.ToParticipant(new ParticipantMappingInput
        {
            Participant = participantRecord,
            State = state,
            Context = new RescueMappingContext(
                input.Endpoint.RescueOptions.Value,
                reportedAt)
        });
        await input.Endpoint.Broadcaster.BroadcastAsync(new RescueBroadcastCommand
        {
            HelpId = input.Context.Help!.Id,
            Type = "participant",
            Data = participant,
            ParticipantVersion = reportedAt
        });
        return Results.Ok(ApiResponse.Ok(new ReportRescueLocationResponse(
            participantRecord.LocationReportedAt!.Value)));
    }

    private static IResult? ValidateLocation(ReportRescueLocationRequest request)
    {
        return request.Lat is null
            || request.Lng is null
            || request.Lat is < -90 or > 90
            || request.Lng is < -180 or > 180
            ? EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "lat/lng 不合法", 400)
            : null;
    }

    private sealed record RescueLocationEndpointInput
    {
        public required RescueSessionEndpointRequest Endpoint { get; init; }
        public required SessionEndpointServices Services { get; init; }
        public required ParticipantContextResult Context { get; init; }
        public required ReportRescueLocationRequest Request { get; init; }
    }
}
