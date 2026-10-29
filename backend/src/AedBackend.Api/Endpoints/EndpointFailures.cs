using AedBackend.Core.DTOs.Common;

namespace AedBackend.Api.Endpoints;

internal static class EndpointFailures
{
    public static IResult JsonFailure(int code, string message, int statusCode)
    {
        return Results.Json(ApiResponse.Fail(code, message), statusCode: statusCode);
    }
}
