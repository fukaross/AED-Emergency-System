using AedBackend.Core.Constants;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Models.User;
using AedBackend.Core.Services.User;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class SqliteUserIdentityReviewService : IUserIdentityReviewService
{
    private readonly IDbContextFactory<AedDbContext> factory;

    public SqliteUserIdentityReviewService(IDbContextFactory<AedDbContext> factory)
    {
        this.factory = factory;
    }

    public async Task<SchoolBindingApplicationRecord> ApproveSchoolBindingAsync(
        int id,
        SchoolBindingApproveCommand command,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var app = await GetPendingSchoolBindingAsync(db, id, ct);
        var profileId = await ResolveSchoolProfileIdAsync(db, app, command, ct);

        app.SchoolProfileId = profileId;
        ApplySchoolReview(app, SchoolBindingStatuses.Approved, command.Reviewer, command.ReviewNote);
        db.AdminAuditLogs.Add(CreateAudit(
            "school_binding",
            "school_binding.approved",
            command.Reviewer,
            id.ToString(),
            $"通过学校绑定申请：#{id}",
            command.ClientIp,
            command.UserAgent,
            new Dictionary<string, object?>
            {
                ["applicationId"] = id,
                ["openId"] = app.OpenId,
                ["schoolProfileId"] = profileId
            }));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToRecord(app);
    }

    public async Task<SchoolBindingApplicationRecord> RejectSchoolBindingAsync(
        int id,
        UserIdentityRejectCommand command,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var app = await GetPendingSchoolBindingAsync(db, id, ct);

        ApplySchoolReview(app, SchoolBindingStatuses.Rejected, command.Reviewer, command.ReviewNote);
        db.AdminAuditLogs.Add(CreateAudit(
            "school_binding",
            "school_binding.rejected",
            command.Reviewer,
            id.ToString(),
            $"驳回学校绑定申请：#{id}",
            command.ClientIp,
            command.UserAgent,
            new Dictionary<string, object?> { ["applicationId"] = id, ["openId"] = app.OpenId }));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToRecord(app);
    }

    public async Task<ResponderClaimApplicationRecord> ApproveResponderClaimAsync(
        int id,
        ResponderClaimApproveCommand command,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var app = await GetPendingResponderClaimAsync(db, id, ct);
        var responder = await GetBindableResponderAsync(db, command.ResponderProfileId, ct);

        responder.BoundOpenId = app.OpenId;
        app.ResponderProfileId = responder.Id;
        ApplyClaimReview(app, ResponderClaimStatuses.Approved, command.Reviewer, command.ReviewNote);
        db.AdminAuditLogs.Add(CreateAudit(
            "responder_claim",
            "responder_claim.approved",
            command.Reviewer,
            id.ToString(),
            $"通过急救员认领申请：#{id}",
            command.ClientIp,
            command.UserAgent,
            new Dictionary<string, object?>
            {
                ["applicationId"] = id,
                ["openId"] = app.OpenId,
                ["responderProfileId"] = responder.Id
            }));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToRecord(app);
    }

    public async Task<ResponderClaimApplicationRecord> RejectResponderClaimAsync(
        int id,
        UserIdentityRejectCommand command,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var app = await GetPendingResponderClaimAsync(db, id, ct);

        ApplyClaimReview(app, ResponderClaimStatuses.Rejected, command.Reviewer, command.ReviewNote);
        db.AdminAuditLogs.Add(CreateAudit(
            "responder_claim",
            "responder_claim.rejected",
            command.Reviewer,
            id.ToString(),
            $"驳回急救员认领申请：#{id}",
            command.ClientIp,
            command.UserAgent,
            new Dictionary<string, object?> { ["applicationId"] = id, ["openId"] = app.OpenId }));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToRecord(app);
    }

    private static async Task<SchoolBindingApplicationEntity> GetPendingSchoolBindingAsync(
        AedDbContext db,
        int id,
        CancellationToken ct)
    {
        var app = await db.SchoolBindingApplications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (app is null) throw ReviewError(ErrorCodes.SchoolBindingNotFound, "学校绑定申请不存在", 404);
        if (app.Status != SchoolBindingStatuses.Submitted)
            throw ReviewError(ErrorCodes.SchoolBindingAlreadyProcessed, "学校绑定申请已处理", 409);
        return app;
    }

    private static async Task<ResponderClaimApplicationEntity> GetPendingResponderClaimAsync(
        AedDbContext db,
        int id,
        CancellationToken ct)
    {
        var app = await db.ResponderClaimApplications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (app is null) throw ReviewError(ErrorCodes.ResponderClaimNotFound, "急救员认领申请不存在", 404);
        if (app.Status != ResponderClaimStatuses.Submitted)
            throw ReviewError(ErrorCodes.ResponderClaimAlreadyProcessed, "急救员认领申请已处理", 409);
        return app;
    }

    private static async Task<int> ResolveSchoolProfileIdAsync(
        AedDbContext db,
        SchoolBindingApplicationEntity app,
        SchoolBindingApproveCommand command,
        CancellationToken ct)
    {
        if (command.SchoolProfileId is int existingId)
        {
            var exists = await db.SchoolPersonProfiles.AnyAsync(x => x.Id == existingId, ct);
            if (!exists) throw ReviewError(ErrorCodes.SchoolProfileNotFound, "学校档案不存在", 404);
            return existingId;
        }

        if (command.NewProfile is null)
        {
            throw ReviewError(ErrorCodes.ParameterInvalid, "必须选择或创建学校档案", 400);
        }

        var now = DateTimeOffset.UtcNow;
        var profile = SqliteMapper.ToEntity(command.NewProfile with { CreatedAt = now, UpdatedAt = now });
        db.SchoolPersonProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return profile.Id;
    }

    private static async Task<ResponderProfileEntity> GetBindableResponderAsync(
        AedDbContext db,
        string responderId,
        CancellationToken ct)
    {
        var responder = await db.ResponderProfiles.FirstOrDefaultAsync(x => x.Id == responderId, ct);
        if (responder is null) throw ReviewError(ErrorCodes.RescuerNotFound, "急救员档案不存在", 404);
        if (!string.IsNullOrWhiteSpace(responder.BoundOpenId))
            throw ReviewError(ErrorCodes.ResponderAlreadyBound, "急救员档案已绑定", 409);
        return responder;
    }

    private static void ApplySchoolReview(
        SchoolBindingApplicationEntity app,
        string status,
        AdminPrincipal reviewer,
        string? note)
    {
        app.Status = status;
        app.ReviewNote = NullIfBlank(note);
        app.ReviewedBy = reviewer.Username;
        app.ReviewedAt = DateTimeOffset.UtcNow;
        app.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ApplyClaimReview(
        ResponderClaimApplicationEntity app,
        string status,
        AdminPrincipal reviewer,
        string? note)
    {
        app.Status = status;
        app.ReviewNote = NullIfBlank(note);
        app.ReviewedBy = reviewer.Username;
        app.ReviewedAt = DateTimeOffset.UtcNow;
        app.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static AdminAuditLogEntity CreateAudit(
        string eventType,
        string eventName,
        AdminPrincipal actor,
        string targetId,
        string summary,
        string? clientIp,
        string? userAgent,
        IReadOnlyDictionary<string, object?> metadata)
    {
        return SqliteMapper.ToEntity(new AdminAuditLogRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            EventType = eventType,
            EventName = eventName,
            TargetId = targetId,
            ActorType = "admin",
            ActorId = actor.Id,
            ActorName = actor.Username,
            ActorRole = actor.Role,
            Summary = summary,
            Metadata = metadata,
            IpAddress = clientIp,
            UserAgent = userAgent,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static SchoolBindingApplicationRecord ToRecord(SchoolBindingApplicationEntity app)
    {
        return SqliteMapper.ToRecord(
            app,
            SqliteSchoolBindingApplicationStore.Deserialize<SchoolBindingPayloadRecord>(app.PayloadJson));
    }

    private static ResponderClaimApplicationRecord ToRecord(ResponderClaimApplicationEntity app)
    {
        return SqliteMapper.ToRecord(
            app,
            SqliteSchoolBindingApplicationStore.Deserialize<ResponderClaimPayloadRecord>(app.PayloadJson));
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static UserIdentityReviewException ReviewError(int code, string message, int status)
    {
        return new UserIdentityReviewException(code, message, status);
    }
}
