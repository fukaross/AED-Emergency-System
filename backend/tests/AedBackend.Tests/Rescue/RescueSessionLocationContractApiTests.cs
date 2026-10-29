using System.Net;
using System.Net.Http.Json;
using AedBackend.Core.Constants;
using AedBackend.Core.Services.Rescue;
using AedBackend.Tests.Admin;
using Microsoft.Extensions.DependencyInjection;

namespace AedBackend.Tests.Rescue;

public sealed class RescueSessionLocationContractApiTests : AdminApiTestBase
{
    private const string ResponderToken = "token-responder";

    [Fact]
    public async Task MissingCoordinates_AreRejectedWithoutWritingDefaults()
    {
        SeedLoggedInExperienceUsers(null, ("openid-2", ResponderToken));
        SeedBoundResponders(("rescuer-1", "openid-2", "active", "张急救"));
        SeedHelpRequests(Help("help-1"));
        UseUserToken(ResponderToken);
        var join = await Client.PostAsync("/api/help/help-1/session/join", null);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);

        var missingBoth = await Client.PutAsJsonAsync(
            "/api/help/help-1/session/location",
            new { });
        var missingLng = await Client.PutAsJsonAsync(
            "/api/help/help-1/session/location",
            new { lat = 0 });

        await AssertErrorAsync(missingBoth, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
        await AssertErrorAsync(missingLng, HttpStatusCode.BadRequest, ErrorCodes.ParameterInvalid);
        var store = Services.GetRequiredService<IRescueSessionStore>();
        var session = await store.FindByHelpIdAsync("help-1", CancellationToken.None);
        var participant = Assert.Single(session!.Participants);
        Assert.Null(participant.LocationReportedAt);
    }
}
