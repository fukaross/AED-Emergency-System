namespace AedBackend.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin");

        group.MapAdminAuthEndpoints();
        group.MapAdminDashboardEndpoints();
        group.MapAdminHelpEndpoints();
        group.MapAdminUserEndpoints();
        group.MapAdminAedEndpoints();
        group.MapAdminManagementEndpoints();
        group.MapAdminAuditEndpoints();

        return app;
    }
}
