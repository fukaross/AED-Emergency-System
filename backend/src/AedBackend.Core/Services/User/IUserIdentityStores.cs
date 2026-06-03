using AedBackend.Core.Models.User;

namespace AedBackend.Core.Services.User;

public interface ISchoolPersonProfileStore
{
    Task<SchoolPersonProfileRecord> CreateAsync(
        SchoolPersonProfileRecord profile,
        CancellationToken cancellationToken);

    Task<SchoolPersonProfileRecord?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<SchoolPersonProfileRecord?> UpdateAsync(
        int id,
        SchoolPersonProfileRecord profile,
        CancellationToken cancellationToken);

    Task<UserIdentityPage<SchoolPersonProfileRecord>> ListAsync(
        SchoolPersonProfileQuery query,
        CancellationToken cancellationToken);
}

public interface ISchoolBindingApplicationStore
{
    Task<SchoolBindingApplicationRecord> SubmitAsync(
        string openId,
        SchoolBindingPayloadRecord payload,
        CancellationToken cancellationToken);

    Task<SchoolBindingApplicationRecord?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<UserIdentityPage<SchoolBindingApplicationRecord>> ListByUserAsync(
        string openId,
        UserIdentityQuery query,
        CancellationToken cancellationToken);

    Task<UserIdentityPage<SchoolBindingApplicationRecord>> ListAllAsync(
        UserIdentityAdminQuery query,
        CancellationToken cancellationToken);
}

public interface IResponderClaimApplicationStore
{
    Task<ResponderClaimApplicationRecord> SubmitAsync(
        string openId,
        ResponderClaimPayloadRecord payload,
        CancellationToken cancellationToken);

    Task<ResponderClaimApplicationRecord?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<UserIdentityPage<ResponderClaimApplicationRecord>> ListByUserAsync(
        string openId,
        UserIdentityQuery query,
        CancellationToken cancellationToken);

    Task<UserIdentityPage<ResponderClaimApplicationRecord>> ListAllAsync(
        UserIdentityAdminQuery query,
        CancellationToken cancellationToken);
}

public interface IUserIdentityReviewService
{
    Task<SchoolBindingApplicationRecord> ApproveSchoolBindingAsync(
        int id,
        SchoolBindingApproveCommand command,
        CancellationToken cancellationToken);

    Task<SchoolBindingApplicationRecord> RejectSchoolBindingAsync(
        int id,
        UserIdentityRejectCommand command,
        CancellationToken cancellationToken);

    Task<ResponderClaimApplicationRecord> ApproveResponderClaimAsync(
        int id,
        ResponderClaimApproveCommand command,
        CancellationToken cancellationToken);

    Task<ResponderClaimApplicationRecord> RejectResponderClaimAsync(
        int id,
        UserIdentityRejectCommand command,
        CancellationToken cancellationToken);
}
