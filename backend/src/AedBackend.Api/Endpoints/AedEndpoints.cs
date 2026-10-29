using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Aed;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Aed;
using AedBackend.Core.Services.Help;

namespace AedBackend.Api.Endpoints;

public static class AedEndpoints
{
    public static IEndpointRouteBuilder MapAedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/aed")
            .WithTags("AED");

        group.MapGet("/nearby", GetNearbyAsync).WithName("GetNearbyAed");
        group.MapGet("/{id}", GetDetailAsync).WithName("GetAedDetail");

        return app;
    }

    private static async Task<IResult> GetNearbyAsync(
        double lat,
        double lng,
        int? radius,
        IAedDeviceStore store,
        CancellationToken cancellationToken)
    {
        var validationError = ValidatePoint(lat, lng);
        if (validationError is not null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, validationError, 400);
        }

        var nearby = await store.GetNearbyAsync(lat, lng, radius, cancellationToken);
        return Results.Ok(ApiResponse.Ok(nearby.Select(ToResponse).ToArray()));
    }

    private static async Task<IResult> GetDetailAsync(
        string id,
        IAedDeviceStore store,
        CancellationToken cancellationToken)
    {
        var device = await store.FindByIdAsync(id, cancellationToken);
        return device is null
            ? EndpointFailures.JsonFailure(ErrorCodes.AedNotFound, "AED 设备不存在", 404)
            : Results.Ok(ApiResponse.Ok(ToResponse(new NearbyAedDevice(device, 0))));
    }

    private static string? ValidatePoint(double lat, double lng)
    {
        if (lat is < -90 or > 90) return "lat 不合法";
        return lng is < -180 or > 180 ? "lng 不合法" : null;
    }

    private static AedItemResponse ToResponse(NearbyAedDevice item)
    {
        var device = item.Device;
        return new AedItemResponse(
            device.Id,
            device.Name,
            device.Lat,
            device.Lng,
            device.Status,
            item.Distance,
            device.Image,
            device.Address,
            device.LastMaintenance,
            device.NextMaintenance,
            device.Maintainer);
    }
}
