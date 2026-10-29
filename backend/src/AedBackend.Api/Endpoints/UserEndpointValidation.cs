using System.Text.Json;
using AedBackend.Core.DTOs.User;
using AedBackend.Core.Models.User;
using AedBackend.Core.Models.Volunteer;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.User;
using AedBackend.Core.Services.Volunteer;
using AedBackend.Core.Validators;

namespace AedBackend.Api.Endpoints;

internal static partial class UserEndpoints
{
    private const int MaxNameLength = 40;
    private const int DefaultApplicationPage = 1;
    private const int DefaultApplicationPageSize = 20;
    private const int MaxApplicationPageSize = 100;
    private static readonly IReadOnlySet<string> PersonTypes = new HashSet<string> { "student", "teacher", "staff" };

    private static (UpdateExperienceUserProfilePatch Patch, string? Error) CreatePatch(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
            return (CreateEmptyPatch(), "请求体必须是对象");

        var name = ReadOptionalString(body, "name");
        var avatarUrl = ReadOptionalString(body, "avatarUrl");
        var gender = ReadOptionalString(body, "gender");
        var phone = ReadOptionalString(body, "phone");
        var error = ValidateProfile(name, avatarUrl, gender, phone);
        if (error is not null) return (CreateEmptyPatch(), error);

        return (new UpdateExperienceUserProfilePatch(
            ToOptional(name),
            ToOptional(avatarUrl),
            ToOptional(gender),
            ToOptional(phone)), null);
    }

    private static string? ValidateProfile(
        OptionalText name,
        OptionalText avatarUrl,
        OptionalText gender,
        OptionalText phone)
    {
        if (name.IsInvalid || avatarUrl.IsInvalid || gender.IsInvalid || phone.IsInvalid)
            return "请求字段类型不正确";
        if (name.HasValue && !string.IsNullOrWhiteSpace(name.Value)
            && name.Value.Trim().Length > MaxNameLength)
            return "昵称不能超过40个字符";
        if (gender.HasValue && !string.IsNullOrWhiteSpace(gender.Value)
            && !Genders.Contains(gender.Value.Trim()))
            return "性别仅支持 male/female/unknown";
        if (phone.HasValue && !string.IsNullOrWhiteSpace(phone.Value)
            && !PhonePattern.IsMatch(phone.Value.Trim()))
            return "手机号格式不正确";
        if (avatarUrl.HasValue && !string.IsNullOrWhiteSpace(avatarUrl.Value)
            && !IsAllowedUrl(avatarUrl.Value))
            return "头像 URL 不支持";
        return null;
    }

    private static (VolunteerApplicationPayloadRecord Payload, string? Error) CreatePayload(
        SubmitVolunteerApplicationRequest body)
    {
        var payload = ToPayload(body);
        return (payload, ValidateVolunteerPayload(payload));
    }

    private static (VolunteerApplicationQuery Query, string? Error) CreateApplicationQuery(
        int? page,
        int? pageSize)
    {
        var currentPage = page ?? DefaultApplicationPage;
        var currentPageSize = pageSize ?? DefaultApplicationPageSize;
        if (currentPage < 1) return (new VolunteerApplicationQuery(), "page 必须大于等于 1");
        if (currentPageSize is < 1 or > MaxApplicationPageSize)
            return (new VolunteerApplicationQuery(), "pageSize 必须在 1 到 100 之间");
        return (new VolunteerApplicationQuery(currentPage, currentPageSize), null);
    }

    private static (UserIdentityQuery Query, string? Error) CreateIdentityQuery(
        int? page,
        int? pageSize)
    {
        var currentPage = page ?? DefaultApplicationPage;
        var currentPageSize = pageSize ?? DefaultApplicationPageSize;
        if (currentPage < 1) return (new UserIdentityQuery(), "page 必须大于等于 1");
        if (currentPageSize is < 1 or > MaxApplicationPageSize)
            return (new UserIdentityQuery(), "pageSize 必须在 1 到 100 之间");
        return (new UserIdentityQuery(currentPage, currentPageSize), null);
    }

    private static (SchoolBindingPayloadRecord Payload, string? Error) CreateSchoolBindingPayload(
        SubmitSchoolBindingRequest body)
    {
        var payload = new SchoolBindingPayloadRecord
        {
            PersonType = Trim(body.PersonType),
            Name = Trim(body.Name),
            College = Trim(body.College),
            ClassName = NullIfEmpty(body.ClassName),
            StudentId = NullIfEmpty(body.StudentId),
            StaffId = NullIfEmpty(body.StaffId),
            Phone = NullIfEmpty(body.Phone),
            Note = NullIfEmpty(body.Note)
        };
        return (payload, ValidateSchoolBinding(payload));
    }

    private static (ResponderClaimPayloadRecord Payload, string? Error) CreateResponderClaimPayload(
        SubmitResponderClaimRequest body)
    {
        var payload = new ResponderClaimPayloadRecord
        {
            Name = Trim(body.Name),
            IdNumberLast6 = Trim(body.IdNumberLast6),
            Phone = Trim(body.Phone),
            CertificateNo = NullIfEmpty(body.CertificateNo),
            Note = NullIfEmpty(body.Note)
        };
        return (payload, ValidateResponderClaim(payload));
    }

    private static VolunteerApplicationPayloadRecord ToPayload(
        SubmitVolunteerApplicationRequest body)
    {
        return new VolunteerApplicationPayloadRecord
        {
            IdPhoto = ToRecord(body.IdPhoto),
            Name = Trim(body.Name),
            Gender = Trim(body.Gender),
            BirthDate = Trim(body.BirthDate),
            Phone = Trim(body.Phone),
            Email = Trim(body.Email),
            PoliticalStatus = Trim(body.PoliticalStatus),
            Education = Trim(body.Education),
            Occupation = Trim(body.Occupation),
            ProfessionalTitle = Trim(body.ProfessionalTitle),
            EmergencyContactName = Trim(body.EmergencyContactName),
            EmergencyContactPhone = Trim(body.EmergencyContactPhone),
            QualificationImages = ToRecords(body.QualificationImages),
            VolunteerCertificateImages = ToRecords(body.VolunteerCertificateImages),
            IdNumber = Trim(body.IdNumber),
            IdCardFront = ToRecord(body.IdCardFront),
            IdCardBack = ToRecord(body.IdCardBack),
            HomeAddress = Trim(body.HomeAddress),
            PersonalResume = Trim(body.PersonalResume),
            JoinReason = Trim(body.JoinReason),
            CommitmentAccepted = body.CommitmentAccepted
        };
    }

    private static string? ValidateVolunteerPayload(VolunteerApplicationPayloadRecord payload)
    {
        if (!payload.CommitmentAccepted) return "请确认入队承诺";
        if (HasMissingRequiredText(payload)) return "志愿者申请字段不能为空";
        if (!Genders.Contains(payload.Gender)) return "性别仅支持 male/female/unknown";
        if (!PhonePattern.IsMatch(payload.Phone) || !PhonePattern.IsMatch(payload.EmergencyContactPhone))
            return "手机号格式不正确";
        var idError = IdNumberValidator.Validate(payload.IdNumber);
        if (idError is not null) return idError;
        if (!HasRequiredImages(payload)) return "申请图片必须先上传为 URL";
        return ValidateAttachments(payload.QualificationImages)
            ?? ValidateAttachments(payload.VolunteerCertificateImages);
    }

    private static bool HasMissingRequiredText(VolunteerApplicationPayloadRecord payload)
    {
        var required = new[]
        {
            payload.Name, payload.Gender, payload.BirthDate, payload.Phone, payload.Email,
            payload.PoliticalStatus, payload.Education, payload.Occupation, payload.ProfessionalTitle,
            payload.EmergencyContactName, payload.EmergencyContactPhone, payload.IdNumber,
            payload.HomeAddress, payload.PersonalResume, payload.JoinReason
        };
        return required.Any(string.IsNullOrWhiteSpace);
    }

    private static bool HasRequiredImages(VolunteerApplicationPayloadRecord payload)
    {
        return IsValidAttachment(payload.IdPhoto)
            && IsValidAttachment(payload.IdCardFront)
            && IsValidAttachment(payload.IdCardBack)
            && payload.QualificationImages.Count > 0
            && payload.VolunteerCertificateImages.Count > 0;
    }

    private static string? ValidateAttachments(IReadOnlyList<VolunteerImageAttachmentRecord> items)
    {
        return items.All(IsValidAttachment) ? null : "申请图片必须先上传为 URL";
    }

    private static bool IsValidAttachment(VolunteerImageAttachmentRecord item)
    {
        return IsAllowedUrl(item.Url);
    }

    private static bool IsAllowedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)) return true;
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string? ValidateSchoolBinding(SchoolBindingPayloadRecord payload)
    {
        if (!PersonTypes.Contains(payload.PersonType)) return "人员类型仅支持 student/teacher/staff";
        if (string.IsNullOrWhiteSpace(payload.Name) || string.IsNullOrWhiteSpace(payload.College))
            return "姓名和学院不能为空";
        if (payload.PersonType == "student" && string.IsNullOrWhiteSpace(payload.StudentId))
            return "学生绑定必须填写学号";
        if (payload.PersonType != "student" && string.IsNullOrWhiteSpace(payload.StaffId))
            return "教职工绑定必须填写工号";
        if (!string.IsNullOrWhiteSpace(payload.Phone) && !PhonePattern.IsMatch(payload.Phone))
            return "手机号格式不正确";
        return null;
    }

    private static string? ValidateResponderClaim(ResponderClaimPayloadRecord payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Name)) return "姓名不能为空";
        if (!System.Text.RegularExpressions.Regex.IsMatch(payload.IdNumberLast6, @"^\d{5}[\dXx]$"))
            return "身份证后 6 位格式不正确";
        if (!PhonePattern.IsMatch(payload.Phone)) return "手机号格式不正确";
        if (payload.CertificateNo is { Length: > 64 }) return "证书号不能超过 64 个字符";
        return null;
    }

    private static VolunteerImageAttachmentRecord ToRecord(VolunteerImageAttachmentDto? item)
    {
        return new VolunteerImageAttachmentRecord(
            Trim(item?.Url),
            NullIfEmpty(item?.FileId),
            NullIfEmpty(item?.Name));
    }

    private static IReadOnlyList<VolunteerImageAttachmentRecord> ToRecords(
        IReadOnlyList<VolunteerImageAttachmentDto>? items)
    {
        return items?.Select(ToRecord).ToArray() ?? [];
    }

    private static OptionalText ReadOptionalString(JsonElement body, string property)
    {
        if (!body.TryGetProperty(property, out var item)) return OptionalText.Missing;
        if (item.ValueKind == JsonValueKind.Null) return OptionalText.Present(null);
        return item.ValueKind == JsonValueKind.String
            ? OptionalText.Present(item.GetString())
            : OptionalText.Invalid;
    }

    private static PatchValue<string?> ToOptional(OptionalText text)
    {
        if (!text.HasValue) return PatchValue<string?>.Missing;
        return PatchValue<string?>.Present(
            string.IsNullOrWhiteSpace(text.Value) ? null : text.Value.Trim());
    }

    private static UpdateExperienceUserProfilePatch CreateEmptyPatch()
    {
        return new UpdateExperienceUserProfilePatch(
            PatchValue<string?>.Missing,
            PatchValue<string?>.Missing,
            PatchValue<string?>.Missing,
            PatchValue<string?>.Missing);
    }

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private readonly record struct OptionalText(bool HasValue, string? Value, bool IsInvalid)
    {
        public static OptionalText Missing => new(false, null, false);
        public static OptionalText Invalid => new(true, null, true);
        public static OptionalText Present(string? value) => new(true, value, false);
    }
}
