using AedBackend.Api.Endpoints;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Rescue;
using AedBackend.Core.Services.Rescue;
using Microsoft.AspNetCore.Http;

namespace AedBackend.Tests.Rescue;

public sealed class RescueSessionClosedMappingApiTests
{
    [Fact]
    public async Task WriteGuard_MapsAtomicClosedRejectionToConflict1016()
    {
        var result = await RescueSessionWriteGuard.ExecuteAsync<RescueSessionRecord>(() =>
            Task.FromException<RescueSessionRecord>(new RescueSessionClosedException("help-1")));

        Assert.Null(result.Value);
        var failure = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result.Failure);
        Assert.Equal(StatusCodes.Status409Conflict, failure.StatusCode);
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result.Failure);
        var response = Assert.IsType<ApiResponse>(value.Value);
        Assert.Equal(ErrorCodes.RescueSessionClosed, response.Code);
    }
}
