namespace AedBackend.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin");

        group.MapAdminAuthEndpoints();
        group.MapAdminDashboardEndpoints();
        group.MapAdminHelpEndpoints();
        group.MapAdminRescueSessionEndpoints();
        group.MapAdminUserEndpoints();
        group.MapAdminAedEndpoints();
        group.MapAdminRescuerEndpoints();
        group.MapAdminVolunteerApplicationEndpoints();
        group.MapAdminSchoolIdentityEndpoints();
        group.MapAdminResponderClaimEndpoints();
        group.MapAdminManagementEndpoints();
        group.MapAdminAuditEndpoints();
        group.MapAdminKnowledgeEndpoints();
        group.MapAdminCourseEndpoints();
        group.MapAdminQuickActionEndpoints();

        return app;
    }
}
