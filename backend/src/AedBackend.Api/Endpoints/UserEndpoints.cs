using System.Text.Json;
using System.Text.RegularExpressions;
using AedBackend.Core.Constants;
using AedBackend.Core.DTOs.Common;
using AedBackend.Core.DTOs.User;
using AedBackend.Core.Models.Volunteer;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Volunteer;
using AedBackend.Core.Services.WeChat;
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
        return app;
    }

    private static async Task<IResult> GetProfileAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await RequireUserAsync(request, tokenService, users, ct);
        return current is null
            ? Unauthorized()
            : Results.Ok(ApiResponse.Ok(ToProfile(current)));
    }

    private static async Task<IResult> UpdateProfileAsync(
        HttpRequest request,
        JsonElement body,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await RequireUserAsync(request, tokenService, users, ct);
        if (current is null) return Unauthorized();

        var patchResult = CreatePatch(body);
        if (patchResult.Error is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, patchResult.Error, 400);

        try
        {
            await users.UpdateProfileAsync(current.OpenId, patchResult.Patch, ct);
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
        IVolunteerApplicationStore applications,
        IOptions<DataOptions> dataOptions,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await RequireUserAsync(request, tokenService, users, ct);
        if (current is null) return Unauthorized();

        var payloadResult = CreatePayload(body);
        if (payloadResult.Error is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, payloadResult.Error, 400);

        try
        {
            var saved = await applications.SubmitAsync(
                current.OpenId,
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
        IVolunteerApplicationStore applications,
        IOptions<DataOptions> dataOptions,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        if (!dataOptions.Value.UseSqlite()) return FeatureRequiresSqlite();
        var current = await RequireUserAsync(request, tokenService, users, ct);
        if (current is null) return Unauthorized();

        var queryResult = CreateApplicationQuery(page, pageSize);
        if (queryResult.Error is not null)
            return EndpointFailures.JsonFailure(ErrorCodes.ParameterInvalid, queryResult.Error, 400);

        try
        {
            var items = await applications.ListByUserAsync(current.OpenId, queryResult.Query, ct);
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

    private static async Task<ExperienceUserRecord?> RequireUserAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore users,
        CancellationToken ct)
    {
        return await EndpointUsers.FindCurrentUserAsync(request, tokenService, users, ct);
    }

    private static IResult Unauthorized()
    {
        return EndpointFailures.JsonFailure(ErrorCodes.Unauthorized, "登录 token 无效", 401);
    }

    private static IResult FeatureRequiresSqlite()
    {
        return EndpointFailures.JsonFailure(
            ErrorCodes.FeatureRequiresSqlite,
            "Phase 7 用户与志愿者接口仅支持 SQLite 数据源",
            501);
    }
}
