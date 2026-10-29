using System.Security.Cryptography;
using System.Text;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.Help;
using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Help;
using Microsoft.AspNetCore.Mvc;

namespace AedBackend.Api.Endpoints;

/** 游客求助查询与关单端点，保持无登录态的 followupCode / closeToken 边界。 */
internal static class GuestHelpEndpoints
{
    internal static async Task<IResult> QueryAsync(
        GuestHelpLookupRequest request,
        IHelpRequestStore helpStore,
        CancellationToken cancellationToken)
    {
        var record = await FindAuthorizedGuestAsync(
            new GuestAuthorization(request.RequestNo, request.FollowupCode, GuestCredentialKind.Followup),
            helpStore,
            cancellationToken);
        if (record is null) return GuestNotFound();

        return Results.Ok(ApiResponse.Ok(HelpResponseMapper.ToGuestLookup(record)));
    }

    internal static async Task<IResult> CloseAsync(
        [AsParameters] GuestCloseHelpEndpointRequest endpoint)
    {
        var record = await FindAuthorizedGuestAsync(
            new GuestAuthorization(
                endpoint.Body.RequestNo,
                endpoint.Body.CloseToken,
                GuestCredentialKind.Close),
            endpoint.HelpStore,
            endpoint.CancellationToken);
        if (record is null) return GuestNotFound();

        return await HelpEndpoints.UpdateHelpAsync(
            () => endpoint.Closure.CloseAsync(
                new HelpClosureCommand
                {
                    HelpId = record.Id,
                    Status = HelpStatuses.FalseAlarm,
                    Note = endpoint.Body.Note
                },
                endpoint.CancellationToken),
            updated => Results.Ok(ApiResponse.Ok(new GuestCloseHelpResponse(
                updated.RequestNo,
                HelpStatuses.FalseAlarm,
                updated.ClosedAt!.Value))));
    }

    private static async Task<HelpRequestRecord?> FindAuthorizedGuestAsync(
        GuestAuthorization authorization,
        IHelpRequestStore helpStore,
        CancellationToken cancellationToken)
    {
        var requestNo = authorization.RequestNo;
        var credential = authorization.Credential;
        if (string.IsNullOrWhiteSpace(requestNo)
            || string.IsNullOrWhiteSpace(credential))
        {
            return null;
        }

        var record = await helpStore.FindByIdOrRequestNoAsync(requestNo, cancellationToken);
        if (record is null
            || record.GuestSession is null
            || !string.Equals(record.RequestChannel, "guest", StringComparison.Ordinal)
            || !string.Equals(record.RequestNo, requestNo, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var expected = authorization.Kind == GuestCredentialKind.Followup
            ? record.GuestSession.FollowupCode
            : record.GuestSession.CloseToken;
        return FixedTimeEquals(expected, credential) ? record : null;
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(actual));
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private static IResult GuestNotFound()
    {
        return EndpointFailures.JsonFailure(ErrorCodes.ResourceNotFound, "求助不存在", 404);
    }

    private sealed record GuestAuthorization(
        string? RequestNo,
        string? Credential,
        GuestCredentialKind Kind);

    private enum GuestCredentialKind
    {
        Followup,
        Close,
    }
}
