using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.Models.User;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.User;
using AedBackend.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

internal static partial class AdminSchoolIdentityEndpoints
{
    private static readonly IReadOnlySet<string> ValidStatuses = new HashSet<string> { "all", "submitted", "approved", "rejected" };
    private static readonly IReadOnlySet<string> QueryPersonTypes = new HashSet<string> { "all", "student", "teacher", "staff" };
    private static readonly IReadOnlySet<string> ProfilePersonTypes = new HashSet<string> { "student", "teacher", "staff" };

    public static RouteGroupBuilder MapAdminSchoolIdentityEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/school-profiles", ListProfilesAsync).WithName("AdminListSchoolProfiles");
        group.MapGet("/school-profiles/{id:int}", GetProfileAsync).WithName("AdminGetSchoolProfile");
        group.MapPost("/school-profiles", CreateProfileAsync).WithName("AdminCreateSchoolProfile");
        group.MapPut("/school-profiles/{id:int}", UpdateProfileAsync).WithName("AdminUpdateSchoolProfile");
        group.MapGet("/school-bindings", ListBindingsAsync).WithName("AdminListSchoolBindings");
        group.MapGet("/school-bindings/{id:int}", GetBindingAsync).WithName("AdminGetSchoolBinding");
        group.MapPut("/school-bindings/{id:int}/approve", ApproveBindingAsync).WithName("AdminApproveSchoolBinding");
        group.MapPut("/school-bindings/{id:int}/reject", RejectBindingAsync).WithName("AdminRejectSchoolBinding");
        return group;
    }

    private static async Task<IResult> ListProfilesAsync(
        HttpContext context,
        int? page,
        int? pageSize,
        string? personType,
        string? keyword,
        ISchoolPersonProfileStore profiles,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        var auth = await AuthorizeAsync(context, authorization, dataOptions, ct);
        if (!auth.Succeeded) return auth.Result!;
        var query = CreateProfileQuery(page, pageSize, personType, keyword);
        if (query.Error is not null) return BadRequest(query.Error);

        var result = await profiles.ListAsync(query.Query, ct);
        return Results.Ok(ApiResponse.Ok(new AdminSchoolProfileListResponse(
            result.Items.Select(ToProfileListItem).ToArray(),
            result.Total, result.Page, result.PageSize, result.TotalPages)));
    }

    private static async Task<IResult> GetProfileAsync(
        HttpContext context,
        int id,
        ISchoolPersonProfileStore profiles,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        var auth = await AuthorizeAsync(context, authorization, dataOptions, ct);
        if (!auth.Succeeded) return auth.Result!;
        var profile = await profiles.GetByIdAsync(id, ct);
        return profile is null
            ? EndpointFailures.JsonFailure(ErrorCodes.SchoolProfileNotFound, "学校档案不存在", 404)
            : Results.Ok(ApiResponse.Ok(ToProfileDetail(profile)));
    }

    private static async Task<IResult> CreateProfileAsync(
        HttpContext context,
        UpsertSchoolProfileRequest body,
        ISchoolPersonProfileStore profiles,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        var auth = await AuthorizeAsync(context, authorization, dataOptions, ct);
        if (!auth.Succeeded) return auth.Result!;
        var profile = CreateProfileRecord(body);
        if (profile.Error is not null) return BadRequest(profile.Error);

        var created = await profiles.CreateAsync(profile.Profile, ct);
        return Results.Ok(ApiResponse.Ok(ToProfileDetail(created)));
    }

    private static async Task<IResult> UpdateProfileAsync(
        HttpContext context,
        int id,
        UpsertSchoolProfileRequest body,
        ISchoolPersonProfileStore profiles,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        var auth = await AuthorizeAsync(context, authorization, dataOptions, ct);
        if (!auth.Succeeded) return auth.Result!;
        var profile = CreateProfileRecord(body);
        if (profile.Error is not null) return BadRequest(profile.Error);

        var updated = await profiles.UpdateAsync(id, profile.Profile, ct);
        return updated is null
            ? EndpointFailures.JsonFailure(ErrorCodes.SchoolProfileNotFound, "学校档案不存在", 404)
            : Results.Ok(ApiResponse.Ok(ToProfileDetail(updated)));
    }

    private static async Task<IResult> ListBindingsAsync(
        HttpContext context,
        int? page,
        int? pageSize,
        string? status,
        string? keyword,
        ISchoolBindingApplicationStore applications,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        var auth = await AuthorizeAsync(context, authorization, dataOptions, ct);
        if (!auth.Succeeded) return auth.Result!;
        var query = CreateAdminQuery(page, pageSize, status, keyword);
        if (query.Error is not null) return BadRequest(query.Error);

        var result = await applications.ListAllAsync(query.Query, ct);
        return Results.Ok(ApiResponse.Ok(new AdminSchoolBindingListResponse(
            result.Items.Select(ToBindingListItem).ToArray(),
            result.Total, result.Page, result.PageSize, result.TotalPages)));
    }

    private static async Task<IResult> GetBindingAsync(
        HttpContext context,
        int id,
        ISchoolBindingApplicationStore applications,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        var auth = await AuthorizeAsync(context, authorization, dataOptions, ct);
        if (!auth.Succeeded) return auth.Result!;
        var item = await applications.GetByIdAsync(id, ct);
        return item is null
            ? EndpointFailures.JsonFailure(ErrorCodes.SchoolBindingNotFound, "学校绑定申请不存在", 404)
            : Results.Ok(ApiResponse.Ok(ToBindingDetail(item)));
    }

    private static async Task<IResult> ApproveBindingAsync(
        HttpContext context,
        int id,
        ApproveSchoolBindingRequest body,
        IUserIdentityReviewService reviewer,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        var auth = await AuthorizeAsync(context, authorization, dataOptions, ct);
        if (!auth.Succeeded) return auth.Result!;
        var profile = body.NewProfile is null ? (null, (string?)null) : CreateProfileRecord(body.NewProfile);
        if (profile.Item2 is not null) return BadRequest(profile.Item2);

        try
        {
            var result = await reviewer.ApproveSchoolBindingAsync(id, new SchoolBindingApproveCommand(
                auth.Principal!, body.SchoolProfileId, profile.Item1, TrimOptional(body.ReviewNote),
                AdminEndpointHelpers.GetClientIp(context),
                AdminEndpointHelpers.GetUserAgent(context.Request)), ct);
            return Results.Ok(ApiResponse.Ok(new AdminSchoolBindingReviewResponse(ToBindingDetail(result))));
        }
        catch (UserIdentityReviewException ex)
        {
            return EndpointFailures.JsonFailure(ex.ErrorCode, ex.Message, ex.StatusCode);
        }
    }

    private static async Task<IResult> RejectBindingAsync(
        HttpContext context,
        int id,
        RejectUserIdentityRequest body,
        IUserIdentityReviewService reviewer,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        var auth = await AuthorizeAsync(context, authorization, dataOptions, ct);
        if (!auth.Succeeded) return auth.Result!;
        var note = ValidateReviewNote(body.ReviewNote);
        if (note.Error is not null) return BadRequest(note.Error);

        try
        {
            var result = await reviewer.RejectSchoolBindingAsync(id, new UserIdentityRejectCommand(
                auth.Principal!, note.Note!, AdminEndpointHelpers.GetClientIp(context),
                AdminEndpointHelpers.GetUserAgent(context.Request)), ct);
            return Results.Ok(ApiResponse.Ok(new AdminSchoolBindingReviewResponse(ToBindingDetail(result))));
        }
        catch (UserIdentityReviewException ex)
        {
            return EndpointFailures.JsonFailure(ex.ErrorCode, ex.Message, ex.StatusCode);
        }
    }

    private static async Task<AuthResult> AuthorizeAsync(
        HttpContext context,
        IAdminAuthorizationService authorization,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return new AuthResult(false, FeatureRequiresSqlite(), null);
        var auth = await authorization.RequireAdminSessionAsync(
            AdminEndpointHelpers.ReadAdminKey(context.Request),
            AdminEndpointHelpers.GetClientIp(context),
            AdminEndpointHelpers.GetUserAgent(context.Request),
            ct);
        return auth.Succeeded
            ? new AuthResult(true, null, auth.Principal)
            : new AuthResult(false, AdminEndpointHelpers.AuthorizationFailure(auth), null);
    }

    private static (SchoolPersonProfileRecord Profile, string? Error) CreateProfileRecord(UpsertSchoolProfileRequest body)
    {
        var profile = new SchoolPersonProfileRecord
        {
            PersonType = Trim(body.PersonType),
            Name = Trim(body.Name),
            College = Trim(body.College),
            ClassName = TrimOptional(body.ClassName),
            StudentId = TrimOptional(body.StudentId),
            StaffId = TrimOptional(body.StaffId),
            Phone = TrimOptional(body.Phone),
            Status = string.IsNullOrWhiteSpace(body.Status) ? "active" : body.Status!.Trim()
        };
        return (profile, ValidateProfile(profile));
    }

    private static string? ValidateProfile(SchoolPersonProfileRecord profile)
    {
        if (!ProfilePersonTypes.Contains(profile.PersonType)) return "人员类型仅支持 student/teacher/staff";
        if (string.IsNullOrWhiteSpace(profile.Name) || string.IsNullOrWhiteSpace(profile.College))
            return "姓名和学院不能为空";
        if (profile.PersonType == "student" && string.IsNullOrWhiteSpace(profile.StudentId))
            return "学生档案必须填写学号";
        if (profile.PersonType != "student" && string.IsNullOrWhiteSpace(profile.StaffId))
            return "教职工档案必须填写工号";
        if (profile.Status is not ("active" or "inactive")) return "档案状态仅支持 active/inactive";
        return null;
    }

    private static (UserIdentityAdminQuery Query, string? Error) CreateAdminQuery(
        int? page,
        int? pageSize,
        string? status,
        string? keyword)
    {
        var safePage = page ?? 1;
        var safeSize = pageSize ?? 20;
        if (safePage < 1) return (new UserIdentityAdminQuery(), "page 必须大于等于 1");
        if (safeSize is < 1 or > 100) return (new UserIdentityAdminQuery(), "pageSize 必须在 1 到 100 之间");
        if (!string.IsNullOrWhiteSpace(status) && !ValidStatuses.Contains(status))
            return (new UserIdentityAdminQuery(), $"状态不支持: {status}");
        return (new UserIdentityAdminQuery(safePage, safeSize, status, keyword), null);
    }

    private static (SchoolPersonProfileQuery Query, string? Error) CreateProfileQuery(
        int? page,
        int? pageSize,
        string? personType,
        string? keyword)
    {
        var safePage = page ?? 1;
        var safeSize = pageSize ?? 20;
        if (safePage < 1) return (new SchoolPersonProfileQuery(), "page 必须大于等于 1");
        if (safeSize is < 1 or > 100) return (new SchoolPersonProfileQuery(), "pageSize 必须在 1 到 100 之间");
        if (!string.IsNullOrWhiteSpace(personType) && !QueryPersonTypes.Contains(personType))
            return (new SchoolPersonProfileQuery(), $"人员类型不支持: {personType}");
        return (new SchoolPersonProfileQuery(safePage, safeSize, personType, keyword), null);
    }

    private static (string? Note, string? Error) ValidateReviewNote(string? note)
    {
        var value = note?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200)
            return (null, "审核意见必须为 1-200 字");
        return (value, null);
    }

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;
    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IResult BadRequest(string message) => EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, message, 400);
    private static IResult FeatureRequiresSqlite() => EndpointFailures.JsonFailure(ErrorCodes.FeatureRequiresSqlite, "Phase 7D 学校身份接口仅支持 SQLite 数据源", 501);
    private sealed record AuthResult(bool Succeeded, IResult? Result, Core.Models.Admin.AdminPrincipal? Principal);
}
