using AedBackend.Core.Services.Admin;
using AedBackend.Core.Services.Help;
using AedBackend.Core.Services.Rescue;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.Rescue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AedBackend.Api.Endpoints;

/** 管理端会话列表的框架绑定参数。 */
internal sealed class AdminRescueSessionListEndpointRequest
{
    public HttpContext HttpContext { get; init; } = default!;
    public string? Status { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public CancellationToken CancellationToken { get; init; }

    [FromServices]
    public IAdminAuthorizationService Authorization { get; init; } = default!;

    [FromServices]
    public IHelpRequestStore HelpStore { get; init; } = default!;

    [FromServices]
    public IRescueSessionStore SessionStore { get; init; } = default!;
}

/** 管理端会话详情的框架绑定参数。 */
internal sealed class AdminRescueSessionDetailEndpointRequest
{
    public HttpContext HttpContext { get; init; } = default!;
    public string HelpId { get; init; } = string.Empty;
    public CancellationToken CancellationToken { get; init; }

    [FromServices]
    public IAdminAuthorizationService Authorization { get; init; } = default!;

    [FromServices]
    public IOptions<RescueOptions> RescueOptions { get; init; } = default!;

    [FromServices]
    public IHelpRequestStore HelpStore { get; init; } = default!;

    [FromServices]
    public IRescueSessionStore SessionStore { get; init; } = default!;

    [FromServices]
    public IExperienceUserStore UserStore { get; init; } = default!;

    [FromServices]
    public IResponderStore ResponderStore { get; init; } = default!;

    [FromServices]
    public IAdminAuditLogStore AuditLogs { get; init; } = default!;
}
