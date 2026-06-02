using AedBackend.Core.Constants;
using AedBackend.Core.Models.Admin;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Models.Volunteer;
using AedBackend.Core.Services.Volunteer;
using Microsoft.EntityFrameworkCore;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal sealed class SqliteVolunteerApplicationReviewService : IVolunteerApplicationReviewService
{
    private const string SourceVolunteerApplication = "volunteer_application";
    private readonly IDbContextFactory<AedDbContext> factory;
    private readonly SqliteVolunteerApplicationReviewOptions options;

    public SqliteVolunteerApplicationReviewService(
        IDbContextFactory<AedDbContext> factory,
        SqliteVolunteerApplicationReviewOptions options)
    {
        this.factory = factory;
        this.options = options;
    }

    public async Task<VolunteerApplicationReviewResult> ApproveAsync(
        int id,
        VolunteerApplicationApproveCommand command,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var entity = await GetPendingApplicationAsync(db, id, ct);
        var payload = DeserializePayload(entity.PayloadJson);
        var now = DateTimeOffset.UtcNow;
        var responderId = options.ForcedResponderId
            ?? await GenerateResponderIdAsync(db, ct);
        var responder = CreateResponder(responderId, entity, payload, command);

        db.ResponderProfiles.Add(SqliteMapper.ToEntity(responder));
        ApplyReview(entity, VolunteerApplicationStatuses.Approved, command.Reviewer, command.ReviewNote, now);
        entity.CreatedRescuerId = responder.Id;
        db.AdminAuditLogs.Add(CreateAudit(
            "volunteer_application",
            "volunteer_application.approved",
            command.Reviewer,
            entity.Id.ToString(),
            $"通过志愿者申请：#{entity.Id}，创建急救员草稿 {responder.Id}",
            command.ClientIp,
            command.UserAgent,
            new Dictionary<string, object?>
            {
                ["applicationId"] = entity.Id,
                ["createdRescuerId"] = responder.Id,
                ["openId"] = entity.OpenId,
                ["applicantName"] = payload.Name
            }));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new VolunteerApplicationReviewResult(ToRecord(entity), responder);
    }

    public async Task<VolunteerApplicationReviewResult> RejectAsync(
        int id,
        VolunteerApplicationRejectCommand command,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var entity = await GetPendingApplicationAsync(db, id, ct);
        var payload = DeserializePayload(entity.PayloadJson);
        ApplyReview(entity, VolunteerApplicationStatuses.Rejected, command.Reviewer, command.ReviewNote, DateTimeOffset.UtcNow);
        db.AdminAuditLogs.Add(CreateAudit(
            "volunteer_application",
            "volunteer_application.rejected",
            command.Reviewer,
            entity.Id.ToString(),
            $"驳回志愿者申请：#{entity.Id}",
            command.ClientIp,
            command.UserAgent,
            new Dictionary<string, object?>
            {
                ["applicationId"] = entity.Id,
                ["openId"] = entity.OpenId,
                ["applicantName"] = payload.Name
            }));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new VolunteerApplicationReviewResult(ToRecord(entity), null);
    }

    private static async Task<VolunteerApplicationEntity> GetPendingApplicationAsync(
        AedDbContext db,
        int id,
        CancellationToken ct)
    {
        var entity = await db.VolunteerApplications.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null)
        {
            throw new VolunteerApplicationReviewException(
                ErrorCodes.VolunteerApplicationNotFound,
                "志愿者申请不存在",
                404);
        }

        if (!entity.Status.Equals(VolunteerApplicationStatuses.Submitted, StringComparison.OrdinalIgnoreCase))
        {
            throw new VolunteerApplicationReviewException(
                ErrorCodes.VolunteerApplicationAlreadyProcessed,
                "志愿者申请已处理",
                409);
        }

        return entity;
    }

    private static void ApplyReview(
        VolunteerApplicationEntity entity,
        string status,
        AdminPrincipal reviewer,
        string? note,
        DateTimeOffset now)
    {
        entity.Status = status;
        entity.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        entity.ReviewedBy = reviewer.Username;
        entity.ReviewedAt = now;
        entity.UpdatedAt = now;
    }

    private static ResponderProfileRecord CreateResponder(
        string id,
        VolunteerApplicationEntity entity,
        VolunteerApplicationPayloadRecord payload,
        VolunteerApplicationApproveCommand command)
    {
        return new ResponderProfileRecord
        {
            Id = id,
            RecordId = id,
            Name = payload.Name.Trim(),
            Gender = payload.Gender,
            BirthDate = payload.BirthDate,
            Phone = payload.Phone,
            IdNumber = payload.IdNumber,
            PhoneMasked = true,
            IdMasked = true,
            AvatarUrl = payload.IdPhoto.Url,
            Campus = command.Campus,
            Level = "trainee",
            QualificationStatus = "inactive",
            Lat = command.Lat,
            Lng = command.Lng,
            TrainingYear = DateTimeOffset.UtcNow.Year,
            Source = SourceVolunteerApplication,
            SourceApplicationId = entity.Id,
            BoundOpenId = entity.OpenId
        };
    }

    private static async Task<string> GenerateResponderIdAsync(AedDbContext db, CancellationToken ct)
    {
        var ids = await db.ResponderProfiles.Select(item => item.Id).ToArrayAsync(ct);
        var max = ids.Where(id => id.StartsWith("JZR-", StringComparison.OrdinalIgnoreCase))
            .Select(id => int.TryParse(id.AsSpan(4), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"JZR-{max + 1:D5}";
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

    private static VolunteerApplicationRecord ToRecord(VolunteerApplicationEntity entity)
    {
        return SqliteMapper.ToRecord(entity, DeserializePayload(entity.PayloadJson));
    }

    private static VolunteerApplicationPayloadRecord DeserializePayload(string json)
    {
        return System.Text.Json.JsonSerializer.Deserialize<VolunteerApplicationPayloadRecord>(
                json,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
            ?? new VolunteerApplicationPayloadRecord();
    }
}

internal sealed class SqliteVolunteerApplicationReviewOptions
{
    public string? ForcedResponderId { get; init; }
}
