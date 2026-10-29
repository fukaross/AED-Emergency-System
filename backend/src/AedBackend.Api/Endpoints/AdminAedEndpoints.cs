using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Aed;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;

namespace AedBackend.Api.Endpoints;

internal static class AdminAedEndpoints
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private static readonly IReadOnlySet<string> ValidStatuses = new HashSet<string>
    {
        "online",
        "offline"
    };

    public static RouteGroupBuilder MapAdminAedEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/aed-devices", ListDevicesAsync).WithName("AdminListAedDevices");
        group.MapGet("/aed-devices/{id}", GetDeviceAsync).WithName("AdminGetAedDevice");
        group.MapPost("/aed-devices", CreateDeviceAsync).WithName("AdminCreateAedDevice");
        group.MapPut("/aed-devices/{id}", UpdateDeviceAsync).WithName("AdminUpdateAedDevice");
        group.MapDelete("/aed-devices/{id}", DeleteDeviceAsync).WithName("AdminDeleteAedDevice");

        return group;
    }

    private static Task<AdminAuthorizationResult> RequireAdminAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        return authorization.RequireAdminSessionAsync(
            AdminEndpointHelpers.ReadAdminKey(context.Request),
            AdminEndpointHelpers.GetClientIp(context),
            AdminEndpointHelpers.GetUserAgent(context.Request),
            cancellationToken);
    }

    private static async Task<IResult> ListDevicesAsync(
        HttpContext context,
        int? page,
        int? pageSize,
        string? status,
        string? keyword,
        IAedDeviceStore store,
        IAdminAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var devices = await store.GetAllAsync(cancellationToken);
        var filtered = ApplyDeviceFilters(devices, status, keyword);

        var safePage = Math.Max(DefaultPage, page ?? DefaultPage);
        var safePageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, 100);
        var items = filtered.Select(ToListItem).ToArray();
        var paged = items.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToArray();

        return Results.Ok(ApiResponse.Ok(new AdminAedDeviceListResponse(
            paged,
            items.Length,
            safePage,
            safePageSize,
            Math.Max(1, (int)Math.Ceiling(items.Length / (double)safePageSize)))));
    }

    private static async Task<IResult> GetDeviceAsync(
        HttpContext context,
        string id,
        IAedDeviceStore store,
        IAdminAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var device = await store.FindByIdAsync(id, cancellationToken);
        return device is null
            ? EndpointFailures.JsonFailure(ErrorCodes.AedNotFound, "AED 设备不存在", 404)
            : Results.Ok(ApiResponse.Ok(ToListItem(device)));
    }

    private static async Task<IResult> CreateDeviceAsync(
        HttpContext context,
        CreateAedDeviceRequest body,
        IAedDeviceStore store,
        IAdminAuthorizationService authorization,
        IAdminAuditLogStore auditLogStore,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var validError = ValidateDevicePayload(body.Name, body.Status, body.StatusNote);
        if (validError is not null) return validError;

        if (await store.ExistsByNameAsync(body.Name.Trim(), null, cancellationToken))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.AedNameDuplicate, "设备名称已存在", 409);
        }

        var status = string.IsNullOrWhiteSpace(body.Status) ? "online" : body.Status;

        var device = new AedDeviceRecord
        {
            Name = body.Name.Trim(),
            Lat = body.Lat,
            Lng = body.Lng,
            Status = status,
            StatusNote = body.StatusNote?.Trim(),
            Image = string.IsNullOrWhiteSpace(body.Image) ? "/static/images/aed-placeholder.png" : body.Image.Trim(),
            Address = body.Address?.Trim() ?? string.Empty,
            LastMaintenance = body.LastMaintenance?.Trim() ?? string.Empty,
            NextMaintenance = body.NextMaintenance?.Trim() ?? string.Empty,
            Maintainer = body.Maintainer?.Trim() ?? string.Empty
        };

        var created = await store.AddAsync(device, cancellationToken);

        await auditLogStore.AppendAsync(AdminEndpointHelpers.CreateAudit(
            "AedDevice", "device.created", auth.Principal, created.Id,
            $"新增 AED 设备：{created.Name}",
            context), cancellationToken);

        return Results.Ok(ApiResponse.Ok(ToListItem(created)));
    }

    private static async Task<IResult> UpdateDeviceAsync(
        HttpContext context,
        string id,
        UpdateAedDeviceRequest body,
        IAedDeviceStore store,
        IAdminAuthorizationService authorization,
        IAdminAuditLogStore auditLogStore,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var existing = await store.FindByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.AedNotFound, "AED 设备不存在", 404);
        }

        var updatedName = body.Name?.Trim();
        if (updatedName is not null && updatedName.Length == 0)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "设备名称不能为空", 400);
        }
        if (updatedName is not null
            && !updatedName.Equals(existing.Name, StringComparison.OrdinalIgnoreCase)
            && await store.ExistsByNameAsync(updatedName, id, cancellationToken))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.AedNameDuplicate, "设备名称已存在", 409);
        }

        var updatedStatus = body.Status ?? existing.Status;
        if (!ValidStatuses.Contains(updatedStatus))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, $"status 不支持: {updatedStatus}", 400);
        }

        var updatedStatusNote = body.StatusNote is not null
            ? (string.IsNullOrWhiteSpace(body.StatusNote) ? null : body.StatusNote.Trim())
            : existing.StatusNote;

        if (updatedStatus == "offline" && string.IsNullOrWhiteSpace(updatedStatusNote))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.AedOfflineRequiresNote, "设备离线时必须填写状态备注", 400);
        }

        var updated = new AedDeviceRecord
        {
            Id = existing.Id,
            Name = updatedName ?? existing.Name,
            Lat = body.Lat ?? existing.Lat,
            Lng = body.Lng ?? existing.Lng,
            Status = updatedStatus,
            StatusNote = updatedStatusNote,
            Image = body.Image?.Trim() ?? existing.Image,
            Address = body.Address?.Trim() ?? existing.Address,
            LastMaintenance = body.LastMaintenance?.Trim() ?? existing.LastMaintenance,
            NextMaintenance = body.NextMaintenance?.Trim() ?? existing.NextMaintenance,
            Maintainer = body.Maintainer?.Trim() ?? existing.Maintainer
        };

        var result = await store.UpdateAsync(id, updated, cancellationToken);
        if (result is null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.AedNotFound, "AED 设备不存在", 404);
        }

        var statusChanged = updatedStatus != existing.Status;
        var summary = statusChanged
            ? $"更新 AED 设备：{result.Name}，状态变更为 {updatedStatus}"
            : $"更新 AED 设备：{result.Name}";

        await auditLogStore.AppendAsync(AdminEndpointHelpers.CreateAudit(
            "AedDevice", statusChanged ? "device.status_changed" : "device.updated",
            auth.Principal, result.Id, summary, context), cancellationToken);

        return Results.Ok(ApiResponse.Ok(ToListItem(result)));
    }

    private static async Task<IResult> DeleteDeviceAsync(
        HttpContext context,
        string id,
        IAedDeviceStore store,
        IAdminAuthorizationService authorization,
        IAdminAuditLogStore auditLogStore,
        CancellationToken cancellationToken)
    {
        var auth = await RequireAdminAsync(context, authorization, cancellationToken);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var existing = await store.FindByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.AedNotFound, "AED 设备不存在", 404);
        }

        var deleted = await store.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.AedNotFound, "AED 设备不存在", 404);
        }

        await auditLogStore.AppendAsync(AdminEndpointHelpers.CreateAudit(
            "AedDevice", "device.deleted", auth.Principal, id,
            $"删除 AED 设备：{existing.Name}", context), cancellationToken);

        return Results.Ok(ApiResponse.Ok(new { deleted = true }));
    }

    private static IResult? ValidateDevicePayload(string? name, string? status, string? statusNote)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "设备名称不能为空", 400);
        }

        var resolvedStatus = string.IsNullOrWhiteSpace(status) ? "online" : status;
        if (!ValidStatuses.Contains(resolvedStatus))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, $"status 不支持: {resolvedStatus}", 400);
        }

        if (resolvedStatus == "offline" && string.IsNullOrWhiteSpace(statusNote))
        {
            return EndpointFailures.JsonFailure(ErrorCodes.AedOfflineRequiresNote, "设备离线时必须填写状态备注", 400);
        }

        return null;
    }

    private static IReadOnlyList<AedDeviceRecord> ApplyDeviceFilters(
        IReadOnlyList<AedDeviceRecord> devices,
        string? status,
        string? keyword)
    {
        var filtered = devices.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            filtered = filtered.Where(d => d.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            filtered = filtered.Where(d =>
                d.Id.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || d.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || d.Address.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.OrderBy(d =>
        {
            var numPart = d.Id.StartsWith("AED-", StringComparison.OrdinalIgnoreCase)
                ? d.Id[4..]
                : d.Id;
            return int.TryParse(numPart, out var n) ? n : int.MaxValue;
        }).ToArray();
    }

    private static AdminAedDeviceListItem ToListItem(AedDeviceRecord device)
    {
        return new AdminAedDeviceListItem(
            device.Id,
            device.Name,
            device.Lat,
            device.Lng,
            device.Status,
            device.StatusNote,
            device.Image,
            device.Address,
            device.LastMaintenance,
            device.NextMaintenance,
            device.Maintainer);
    }
}
