using System.Text.Json;
using System.Text.RegularExpressions;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.User;
using AedBackend.Core.Models.Volunteer;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.User;
using AedBackend.Core.Services.Volunteer;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Demo;
using AedBackend.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

internal static partial class UserEndpoints
{
    private static readonly Regex PhonePattern = new(@"^1[3-9]\d{9}$", RegexOptions.Compiled);
    private static readonly IReadOnlySet<string> Genders = new HashSet<string> { "male", "female", "unknown" };

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/user").WithTags("User");
        group.MapGet("/profile", GetProfileAsync).WithName("GetUserProfile");
        group.MapPut("/profile", UpdateProfileAsync).WithName("UpdateUserProfile");
        group.MapPost("/volunteer-applications", SubmitVolunteerApplicationAsync)
            .WithName("SubmitVolunteerApplication");
        group.MapGet("/volunteer-applications", ListVolunteerApplicationsAsync)
            .WithName("ListMyVolunteerApplications");
        group.MapUserArchiveEndpoints();
        group.MapPost("/school-bindings", SubmitSchoolBindingAsync)
            .WithName("SubmitSchoolBinding");
        group.MapGet("/school-bindings", ListSchoolBindingsAsync)
            .WithName("ListMySchoolBindings");
        group.MapPost("/responder-claims", SubmitResponderClaimAsync)
            .WithName("SubmitResponderClaim");
        group.MapGet("/responder-claims", ListResponderClaimsAsync)
            .WithName("ListMyResponderClaims");
        return app;
    }

    private static async Task<IResult> GetProfileAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IUserAccountStatusService accountStatus,
        ISchoolBindingApplicationStore schoolBindings,
        ISchoolPersonProfileStore schoolProfiles,
        IResponderStore responders,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await RequireUserAsync(request, tokenService, users, accountStatus, ct);
        return current.Failure is not null
            ? current.Failure
            : Results.Ok(ApiResponse.Ok(await ToProfileAsync(
                current.User!, accountStatus, schoolBindings, schoolProfiles, responders, ct)));
    }

    private static async Task<IResult> UpdateProfileAsync(
        HttpRequest request,
        JsonElement body,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IUserAccountStatusService accountStatus,
        IResponderStore responders,
        IOptions<DataOptions> dataOptions,
        IOptions<DemoOptions> demoOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await RequireUserAsync(request, tokenService, users, accountStatus, ct);
        if (current.Failure is not null) return current.Failure;

        var patchResult = CreatePatch(body);
        if (patchResult.Error is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, patchResult.Error, 400);

        // 演示手机号只来自固定演示角色，不开放任意号码修改入口。
        if (demoOptions.Value.Enabled && patchResult.Patch.Phone.HasValue)
        {
            return EndpointFailures.JsonFailure(
                ErrorCodes.Forbidden,
                "演示模式手机号来自固定演示角色，不支持修改",
                403);
        }

        try
        {
            var openId = current.User!.OpenId;
            await users.UpdateProfileAsync(openId, patchResult.Patch, ct);
            await SyncBoundResponderProfileAsync(openId, patchResult.Patch, responders, ct);
            return Results.Ok(ApiResponse.Ok());
        }
        catch (NotSupportedException)
        {
            return FeatureRequiresSqlite();
        }
    }

    private static async Task<IResult> SubmitVolunteerApplicationAsync(
        HttpRequest request,
        SubmitVolunteerApplicationRequest body,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IUserAccountStatusService accountStatus,
        IVolunteerApplicationStore applications,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await RequireUserAsync(request, tokenService, users, accountStatus, ct);
        if (current.Failure is not null) return current.Failure;

        var payloadResult = CreatePayload(body);
        if (payloadResult.Error is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, payloadResult.Error, 400);

        try
        {
            var saved = await applications.SubmitAsync(
                current.User!.OpenId,
                payloadResult.Payload,
                body.DerivedFromId,
                ct);
            return Results.Ok(ApiResponse.Ok(new VolunteerApplicationResponse(
                saved.Id.ToString(),
                saved.Status,
                saved.CreatedAt)));
        }
        catch (InvalidOperationException ex)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, ex.Message, 400);
        }
        catch (NotSupportedException)
        {
            return FeatureRequiresSqlite();
        }
    }

    private static async Task<IResult> ListVolunteerApplicationsAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IUserAccountStatusService accountStatus,
        IVolunteerApplicationStore applications,
        IOptions<DataOptions> dataOptions,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await RequireUserAsync(request, tokenService, users, accountStatus, ct);
        if (current.Failure is not null) return current.Failure;

        var queryResult = CreateApplicationQuery(page, pageSize);
        if (queryResult.Error is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, queryResult.Error, 400);

        try
        {
            var items = await applications.ListByUserAsync(current.User!.OpenId, queryResult.Query, ct);
            return Results.Ok(ApiResponse.Ok(new VolunteerApplicationListResponse(
                items.Items.Select(ToApplicationDetail).ToArray(),
                items.Total,
                items.Page,
                items.PageSize,
                items.TotalPages)));
        }
        catch (NotSupportedException)
        {
            return FeatureRequiresSqlite();
        }
    }

    private static async Task<IResult> SubmitSchoolBindingAsync(
        HttpRequest request,
        SubmitSchoolBindingRequest body,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IUserAccountStatusService accountStatus,
        ISchoolBindingApplicationStore applications,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await RequireUserAsync(request, tokenService, users, accountStatus, ct);
        if (current.Failure is not null) return current.Failure;

        var payload = CreateSchoolBindingPayload(body);
        if (payload.Error is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, payload.Error, 400);

        try
        {
            var saved = await applications.SubmitAsync(current.User!.OpenId, payload.Payload, ct);
            return Results.Ok(ApiResponse.Ok(new SchoolBindingResponse(
                saved.Id.ToString(), saved.Status, saved.CreatedAt)));
        }
        catch (InvalidOperationException ex)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.PendingApplicationExists, ex.Message, 409);
        }
        catch (NotSupportedException)
        {
            return FeatureRequiresSqlite();
        }
    }

    private static async Task<IResult> ListSchoolBindingsAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IUserAccountStatusService accountStatus,
        ISchoolBindingApplicationStore applications,
        IOptions<DataOptions> dataOptions,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await RequireUserAsync(request, tokenService, users, accountStatus, ct);
        if (current.Failure is not null) return current.Failure;

        var query = CreateIdentityQuery(page, pageSize);
        if (query.Error is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, query.Error, 400);

        var result = await applications.ListByUserAsync(current.User!.OpenId, query.Query, ct);
        return Results.Ok(ApiResponse.Ok(new SchoolBindingListResponse(
            result.Items.Select(ToSchoolBindingDetail).ToArray(),
            result.Total,
            result.Page,
            result.PageSize,
            result.TotalPages)));
    }

    private static async Task<IResult> SubmitResponderClaimAsync(
        HttpRequest request,
        SubmitResponderClaimRequest body,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IUserAccountStatusService accountStatus,
        IResponderClaimApplicationStore applications,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await RequireUserAsync(request, tokenService, users, accountStatus, ct);
        if (current.Failure is not null) return current.Failure;

        var payload = CreateResponderClaimPayload(body);
        if (payload.Error is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, payload.Error, 400);

        try
        {
            var saved = await applications.SubmitAsync(current.User!.OpenId, payload.Payload, ct);
            return Results.Ok(ApiResponse.Ok(new ResponderClaimResponse(
                saved.Id.ToString(), saved.Status, saved.CreatedAt)));
        }
        catch (InvalidOperationException ex)
        {
            return EndpointFailures.JsonFailure(ErrorCodes.PendingApplicationExists, ex.Message, 409);
        }
        catch (NotSupportedException)
        {
            return FeatureRequiresSqlite();
        }
    }

    private static async Task<IResult> ListResponderClaimsAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IUserAccountStatusService accountStatus,
        IResponderClaimApplicationStore applications,
        IOptions<DataOptions> dataOptions,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await RequireUserAsync(request, tokenService, users, accountStatus, ct);
        if (current.Failure is not null) return current.Failure;

        var query = CreateIdentityQuery(page, pageSize);
        if (query.Error is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, query.Error, 400);

        var result = await applications.ListByUserAsync(current.User!.OpenId, query.Query, ct);
        return Results.Ok(ApiResponse.Ok(new ResponderClaimListResponse(
            result.Items.Select(ToResponderClaimDetail).ToArray(),
            result.Total,
            result.Page,
            result.PageSize,
            result.TotalPages)));
    }

    private static async Task<CurrentUserResult> RequireUserAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IUserAccountStatusService accountStatus,
        CancellationToken ct)
    {
        return await EndpointUsers.RequireActiveUserAsync(
            request,
            new ActiveUserDependencies(tokenService, users, accountStatus),
            ct);
    }

    private static IResult FeatureRequiresSqlite()
    {
        return EndpointFailures.JsonFailure(
            ErrorCodes.FeatureRequiresSqlite,
            "Phase 7 用户与志愿者接口仅支持 SQLite 数据源",
            501);
    }
}
