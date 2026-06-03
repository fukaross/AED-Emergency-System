using AedBackend.Core.DTOs.Admin;
using AedBackend.Core.Models.User;

namespace AedBackend.Api.Endpoints;

internal static partial class AdminSchoolIdentityEndpoints
{
    private static AdminSchoolProfileListItem ToProfileListItem(SchoolPersonProfileRecord item)
    {
        return new AdminSchoolProfileListItem(item.Id.ToString(), item.PersonType, item.Name,
            item.College, item.ClassName, item.StudentId, item.StaffId, MaskPhone(item.Phone),
            item.Status, item.UpdatedAt);
    }

    private static AdminSchoolProfileDetail ToProfileDetail(SchoolPersonProfileRecord item)
    {
        return new AdminSchoolProfileDetail(item.Id.ToString(), item.PersonType, item.Name,
            item.College, item.ClassName, item.StudentId, item.StaffId, item.Phone,
            item.Status, item.CreatedAt, item.UpdatedAt);
    }

    private static AdminSchoolBindingListItem ToBindingListItem(SchoolBindingApplicationRecord item)
    {
        return new AdminSchoolBindingListItem(item.Id.ToString(), item.OpenId, item.Payload.Name,
            item.Payload.College, item.Payload.StudentId, item.Payload.StaffId, item.Status,
            item.SchoolProfileId, item.CreatedAt, item.UpdatedAt, item.ReviewedAt);
    }

    private static AdminSchoolBindingDetail ToBindingDetail(SchoolBindingApplicationRecord item)
    {
        return new AdminSchoolBindingDetail(item.Id.ToString(), item.OpenId, item.Status,
            item.SchoolProfileId, ToPayload(item.Payload), item.ReviewNote, item.ReviewedBy,
            item.ReviewedAt, item.CreatedAt, item.UpdatedAt);
    }

    private static AdminSchoolBindingPayload ToPayload(SchoolBindingPayloadRecord payload)
    {
        return new AdminSchoolBindingPayload(payload.PersonType, payload.Name, payload.College,
            payload.ClassName, payload.StudentId, payload.StaffId, payload.Phone, payload.Note);
    }

    private static string? MaskPhone(string? phone)
    {
        return string.IsNullOrWhiteSpace(phone) || phone.Length < 7
            ? phone
            : $"{phone[..3]}****{phone[^4..]}";
    }
}
