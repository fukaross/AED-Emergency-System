using AedBackend.Core.Models.Aed;
using AedBackend.Core.Models.Knowledge;
using AedBackend.Core.Models.Responder;
using AedBackend.Core.Models.User;
using AedBackend.Core.Models.Volunteer;

namespace AedBackend.Infrastructure.Persistence.Sqlite;

internal static partial class SqliteMapper
{
    public static AedDeviceEntity ToEntity(AedDeviceRecord item)
    {
        return new AedDeviceEntity
        {
            Id = item.Id,
            Name = item.Name,
            Lat = item.Lat,
            Lng = item.Lng,
            Status = item.Status,
            StatusNote = item.StatusNote,
            Image = item.Image,
            Address = item.Address,
            LastMaintenance = item.LastMaintenance,
            NextMaintenance = item.NextMaintenance,
            Maintainer = item.Maintainer
        };
    }

    public static AedDeviceRecord ToRecord(AedDeviceEntity item)
    {
        return new AedDeviceRecord
        {
            Id = item.Id,
            Name = item.Name,
            Lat = item.Lat,
            Lng = item.Lng,
            Status = item.Status,
            StatusNote = item.StatusNote,
            Image = item.Image,
            Address = item.Address,
            LastMaintenance = item.LastMaintenance,
            NextMaintenance = item.NextMaintenance,
            Maintainer = item.Maintainer
        };
    }

    public static ResponderProfileEntity ToEntity(ResponderProfileRecord item)
    {
        return new ResponderProfileEntity
        {
            Id = item.Id, RecordId = item.RecordId, Name = item.Name, Gender = item.Gender,
            BirthDate = item.BirthDate, Phone = item.Phone, IdNumber = item.IdNumber,
            PhoneMasked = item.PhoneMasked, IdMasked = item.IdMasked, AvatarUrl = item.AvatarUrl,
            Campus = item.Campus, Level = item.Level, QualificationStatus = item.QualificationStatus,
            Lat = item.Lat, Lng = item.Lng, CertificateNo = item.CertificateNo,
            CertificateNoMasked = item.CertificateNoMasked, IssuingAuthority = item.IssuingAuthority,
            IssueDate = item.IssueDate, WorkUnit = item.WorkUnit, SkillCpr = item.SkillCpr,
            SkillAed = item.SkillAed, SkillStb = item.SkillStb, TrainingYear = item.TrainingYear,
            TrainingProject = item.TrainingProject, TrainingSessionName = item.TrainingSessionName,
            TheoryScore = item.TheoryScore, TraumaAssessmentResult = item.TraumaAssessmentResult,
            CprAssessmentResult = item.CprAssessmentResult, AedAssessmentResult = item.AedAssessmentResult,
            CprAedCombinedResult = item.CprAedCombinedResult, SourceFile = item.SourceFile,
            SourceSheet = item.SourceSheet, SourceRowNo = item.SourceRowNo,
            SourceSeqNo = item.SourceSeqNo, Remark = item.Remark,
            BoundOpenId = item.BoundOpenId, Source = item.Source,
            SourceApplicationId = item.SourceApplicationId
        };
    }

    public static ResponderProfileRecord ToRecord(ResponderProfileEntity item)
    {
        return new ResponderProfileRecord
        {
            Id = item.Id, RecordId = item.RecordId, Name = item.Name, Gender = item.Gender,
            BirthDate = item.BirthDate, Phone = item.Phone, IdNumber = item.IdNumber,
            PhoneMasked = item.PhoneMasked, IdMasked = item.IdMasked, AvatarUrl = item.AvatarUrl,
            Campus = item.Campus, Level = item.Level, QualificationStatus = item.QualificationStatus,
            Lat = item.Lat, Lng = item.Lng, CertificateNo = item.CertificateNo,
            CertificateNoMasked = item.CertificateNoMasked, IssuingAuthority = item.IssuingAuthority,
            IssueDate = item.IssueDate, WorkUnit = item.WorkUnit, SkillCpr = item.SkillCpr,
            SkillAed = item.SkillAed, SkillStb = item.SkillStb, TrainingYear = item.TrainingYear,
            TrainingProject = item.TrainingProject, TrainingSessionName = item.TrainingSessionName,
            TheoryScore = item.TheoryScore, TraumaAssessmentResult = item.TraumaAssessmentResult,
            CprAssessmentResult = item.CprAssessmentResult, AedAssessmentResult = item.AedAssessmentResult,
            CprAedCombinedResult = item.CprAedCombinedResult, SourceFile = item.SourceFile,
            SourceSheet = item.SourceSheet, SourceRowNo = item.SourceRowNo,
            SourceSeqNo = item.SourceSeqNo, Remark = item.Remark,
            BoundOpenId = item.BoundOpenId, Source = item.Source,
            SourceApplicationId = item.SourceApplicationId
        };
    }

    public static VolunteerApplicationEntity ToEntity(
        VolunteerApplicationRecord item,
        string payloadJson)
    {
        return new VolunteerApplicationEntity
        {
            Id = item.Id,
            OpenId = item.OpenId,
            Status = item.Status,
            DerivedFromId = item.DerivedFromId,
            AttemptNumber = item.AttemptNumber,
            PayloadJson = payloadJson,
            ReviewNote = item.ReviewNote,
            ReviewedBy = item.ReviewedBy,
            ReviewedAt = item.ReviewedAt,
            CreatedRescuerId = item.CreatedRescuerId,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    public static VolunteerApplicationRecord ToRecord(
        VolunteerApplicationEntity item,
        VolunteerApplicationPayloadRecord payload)
    {
        return new VolunteerApplicationRecord
        {
            Id = item.Id,
            OpenId = item.OpenId,
            Status = item.Status,
            DerivedFromId = item.DerivedFromId,
            AttemptNumber = item.AttemptNumber,
            Payload = payload,
            ReviewNote = item.ReviewNote,
            ReviewedBy = item.ReviewedBy,
            ReviewedAt = item.ReviewedAt,
            CreatedRescuerId = item.CreatedRescuerId,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    public static SchoolPersonProfileEntity ToEntity(SchoolPersonProfileRecord item)
    {
        return new SchoolPersonProfileEntity
        {
            Id = item.Id, PersonType = item.PersonType, Name = item.Name,
            College = item.College, ClassName = item.ClassName, StudentId = item.StudentId,
            StaffId = item.StaffId, Phone = item.Phone, Status = item.Status,
            CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
        };
    }

    public static SchoolPersonProfileRecord ToRecord(SchoolPersonProfileEntity item)
    {
        return new SchoolPersonProfileRecord
        {
            Id = item.Id, PersonType = item.PersonType, Name = item.Name,
            College = item.College, ClassName = item.ClassName, StudentId = item.StudentId,
            StaffId = item.StaffId, Phone = item.Phone, Status = item.Status,
            CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
        };
    }

    public static SchoolBindingApplicationEntity ToEntity(
        SchoolBindingApplicationRecord item,
        string payloadJson)
    {
        return new SchoolBindingApplicationEntity
        {
            Id = item.Id, OpenId = item.OpenId, Status = item.Status,
            SchoolProfileId = item.SchoolProfileId, PayloadJson = payloadJson,
            ReviewNote = item.ReviewNote, ReviewedBy = item.ReviewedBy,
            ReviewedAt = item.ReviewedAt, CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    public static SchoolBindingApplicationRecord ToRecord(
        SchoolBindingApplicationEntity item,
        SchoolBindingPayloadRecord payload)
    {
        return new SchoolBindingApplicationRecord
        {
            Id = item.Id, OpenId = item.OpenId, Status = item.Status,
            SchoolProfileId = item.SchoolProfileId, Payload = payload,
            ReviewNote = item.ReviewNote, ReviewedBy = item.ReviewedBy,
            ReviewedAt = item.ReviewedAt, CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    public static ResponderClaimApplicationEntity ToEntity(
        ResponderClaimApplicationRecord item,
        string payloadJson)
    {
        return new ResponderClaimApplicationEntity
        {
            Id = item.Id, OpenId = item.OpenId, Status = item.Status,
            ResponderProfileId = item.ResponderProfileId, PayloadJson = payloadJson,
            ReviewNote = item.ReviewNote, ReviewedBy = item.ReviewedBy,
            ReviewedAt = item.ReviewedAt, CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    public static ResponderClaimApplicationRecord ToRecord(
        ResponderClaimApplicationEntity item,
        ResponderClaimPayloadRecord payload)
    {
        return new ResponderClaimApplicationRecord
        {
            Id = item.Id, OpenId = item.OpenId, Status = item.Status,
            ResponderProfileId = item.ResponderProfileId, Payload = payload,
            ReviewNote = item.ReviewNote, ReviewedBy = item.ReviewedBy,
            ReviewedAt = item.ReviewedAt, CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    public static KnowledgeSummaryEntity ToEntity(KnowledgeSummary item)
    {
        return new KnowledgeSummaryEntity
        {
            Id = 1,
            UpdatedAt = item.UpdatedAt,
            Items = item.Items.Select(ToEntity).ToList()
        };
    }

    public static KnowledgeSummary ToRecord(KnowledgeSummaryEntity item)
    {
        return new KnowledgeSummary
        {
            UpdatedAt = item.UpdatedAt,
            Items = item.Items.OrderBy(i => i.SortOrder).Select(ToRecord).ToList()
        };
    }

    private static KnowledgeStatItemEntity ToEntity(KnowledgeStatItem item)
    {
        return new KnowledgeStatItemEntity
        {
            Key = item.Key,
            Title = item.Title,
            Value = item.Value,
            SortOrder = item.SortOrder
        };
    }

    private static KnowledgeStatItem ToRecord(KnowledgeStatItemEntity item)
    {
        return new KnowledgeStatItem
        {
            Key = item.Key,
            Title = item.Title,
            Value = item.Value,
            SortOrder = item.SortOrder
        };
    }
}
