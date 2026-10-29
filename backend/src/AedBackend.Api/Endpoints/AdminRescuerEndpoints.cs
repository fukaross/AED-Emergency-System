using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.WeChat;
using AedBackend.Core.Validators;

namespace AedBackend.Api.Endpoints;

internal static class AdminRescuerEndpoints
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;

    private static readonly IReadOnlySet<string> ValidCampuses = new HashSet<string> { "guilinyang", "fucheng" };
    private static readonly IReadOnlySet<string> ValidLevels = new HashSet<string> { "honor", "trainee", "official", "core" };
    private static readonly IReadOnlySet<string> ValidStatuses = new HashSet<string> { "active", "inactive" };
    private static readonly IReadOnlySet<string> ValidAssessmentResults = new HashSet<string> { "合格", "不合格", "未考核" };

    public static RouteGroupBuilder MapAdminRescuerEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/rescuers", ListRescuersAsync).WithName("AdminListRescuers");
        group.MapGet("/rescuers/{id}", GetRescuerAsync).WithName("AdminGetRescuer");
        group.MapPut("/rescuers/{id}", UpdateRescuerAsync).WithName("AdminUpdateRescuer");

        return group;
    }

    private static Task<AdminAuthorizationResult> RequireAdminAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        CancellationToken ct)
    {
        return authorization.RequireAdminSessionAsync(
            AdminEndpointHelpers.ReadAdminKey(context.Request),
            AdminEndpointHelpers.GetClientIp(context),
            AdminEndpointHelpers.GetUserAgent(context.Request),
            ct);
    }

    private static async Task<IResult> ListRescuersAsync(
        HttpContext context,
        int? page,
        int? pageSize,
        string? campus,
        string? level,
        string? qualificationStatus,
        string? keyword,
        IResponderStore store,
        IAdminAuthorizationService authorization,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var profiles = await store.GetAllAsync(ct);
        var filtered = ApplyFilters(profiles, campus, level, qualificationStatus, keyword);

        var safePage = Math.Max(DefaultPage, page ?? DefaultPage);
        var safePageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, 100);
        var items = filtered.Select(AdminRescuerMapper.ToListItem).ToArray();
        var paged = items.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToArray();

        return Results.Ok(ApiResponse.Ok(new AdminRescuerListResponse(
            paged,
            items.Length,
            safePage,
            safePageSize,
            Math.Max(1, (int)Math.Ceiling(items.Length / (double)safePageSize)))));
    }

    private static async Task<IResult> GetRescuerAsync(
        HttpContext context,
        string id,
        IResponderStore store,
        IExperienceUserStore users,
        IAdminAuthorizationService authorization,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var profile = await store.FindByIdAsync(id, ct);
        if (profile is null)
            return EndpointFailures.JsonFailure(ErrorCodes.RescuerNotFound, "急救员不存在", 404);

        var boundUserName = await ResolveBoundUserNameAsync(profile.BoundOpenId, users, ct);
        return Results.Ok(ApiResponse.Ok(AdminRescuerMapper.ToDetail(profile, boundUserName)));
    }

    private static async Task<IResult> UpdateRescuerAsync(
        HttpContext context,
        string id,
        UpdateRescuerRequest body,
        IResponderStore store,
        IExperienceUserStore users,
        IAdminAuthorizationService authorization,
        IAdminAuditLogStore auditLogStore,
        CancellationToken ct)
    {
        var auth = await RequireAdminAsync(context, authorization, ct);
        if (!auth.Succeeded) return AdminEndpointHelpers.AuthorizationFailure(auth);

        var existing = await store.FindByIdAsync(id, ct);
        if (existing is null)
            return EndpointFailures.JsonFailure(ErrorCodes.RescuerNotFound, "急救员不存在", 404);

        var validationError = ValidateUpdateFields(body);
        if (validationError is not null) return validationError;

        var target = AdminRescuerUpdateMapper.Resolve(body, existing);

        if (string.IsNullOrWhiteSpace(target.Name))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "姓名不能为空", 400);
        if (!ValidCampuses.Contains(target.Campus))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, $"校区不支持: {target.Campus}", 400);
        if (target.Level is not null && !ValidLevels.Contains(target.Level))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, $"等级不支持: {target.Level}", 400);
        if (!ValidStatuses.Contains(target.QualificationStatus))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, $"资格状态不支持: {target.QualificationStatus}", 400);

        var result = await store.UpdateAsync(id, target.ToRecord(id), ct);
        if (result is null)
            return EndpointFailures.JsonFailure(ErrorCodes.RescuerNotFound, "急救员不存在", 404);

        await auditLogStore.AppendAsync(AdminEndpointHelpers.CreateAudit(
            "Rescuer", "rescuer.updated",
            auth.Principal, result.Id,
            $"更新急救员：{result.Name}",
            context), ct);

        var boundUserName = await ResolveBoundUserNameAsync(result.BoundOpenId, users, ct);
        return Results.Ok(ApiResponse.Ok(AdminRescuerMapper.ToDetail(result, boundUserName)));
    }

    private static IResult? ValidateUpdateFields(UpdateRescuerRequest body)
    {
        if (body.HasName && string.IsNullOrWhiteSpace(body.Name))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, "姓名不能为空", 400);

        if (body.HasCampus && !ValidCampuses.Contains(body.Campus ?? string.Empty))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, $"校区不支持: {body.Campus}", 400);

        if (body.HasLevel && !string.IsNullOrWhiteSpace(body.Level)
            && !ValidLevels.Contains(body.Level))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, $"等级不支持: {body.Level}", 400);

        if (body.HasQualificationStatus
            && !ValidStatuses.Contains(body.QualificationStatus ?? string.Empty))
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, $"资格状态不支持: {body.QualificationStatus}", 400);

        if (body.HasIdNumber)
        {
            var idError = IdNumberValidator.Validate(body.IdNumber);
            if (idError is not null)
                return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, idError, 400);
        }

        var assessmentError = ValidateAssessmentResult(
            body.HasTraumaAssessmentResult,
            body.TraumaAssessmentResult,
            "创伤救护考核");
        if (assessmentError is not null) return assessmentError;

        assessmentError = ValidateAssessmentResult(
            body.HasCprAssessmentResult,
            body.CprAssessmentResult,
            "CPR考核");
        if (assessmentError is not null) return assessmentError;

        assessmentError = ValidateAssessmentResult(
            body.HasAedAssessmentResult,
            body.AedAssessmentResult,
            "AED考核");
        if (assessmentError is not null) return assessmentError;

        assessmentError = ValidateAssessmentResult(
            body.HasCprAedCombinedResult,
            body.CprAedCombinedResult,
            "综合考核");
        if (assessmentError is not null) return assessmentError;

        return null;
    }

    private static IResult? ValidateAssessmentResult(bool hasValue, string? value, string fieldName)
    {
        if (!hasValue || string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return ValidAssessmentResults.Contains(trimmed)
            ? null
            : EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, $"{fieldName}结果不支持: {value}", 400);
    }

    private static IReadOnlyList<ResponderProfileRecord> ApplyFilters(
        IReadOnlyList<ResponderProfileRecord> profiles,
        string? campus,
        string? level,
        string? qualificationStatus,
        string? keyword)
    {
        var filtered = profiles.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(campus) && campus != "all")
            filtered = filtered.Where(p => p.Campus.Equals(campus, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(level) && level != "all")
            filtered = filtered.Where(p =>
                p.Level is not null && p.Level.Equals(level, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(qualificationStatus) && qualificationStatus != "all")
            filtered = filtered.Where(p => p.QualificationStatus.Equals(qualificationStatus, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            filtered = filtered.Where(p =>
                p.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || p.Id.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || p.RecordId.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || (p.CertificateNo is not null && p.CertificateNo.Contains(kw, StringComparison.OrdinalIgnoreCase))
                || (p.CertificateNoMasked is not null && p.CertificateNoMasked.Contains(kw, StringComparison.OrdinalIgnoreCase))
                || (p.WorkUnit is not null && p.WorkUnit.Contains(kw, StringComparison.OrdinalIgnoreCase)));
        }

        return filtered.OrderBy(p => p.Id).ToArray();
    }

    private static async Task<string?> ResolveBoundUserNameAsync(
        string? openId,
        IExperienceUserStore? users,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(openId) || users is null) return null;
        var user = (await users.ListUsersAsync(ct)).FirstOrDefault(item => item.OpenId == openId);
        return string.IsNullOrWhiteSpace(user?.Name) ? null : user.Name;
    }
}
