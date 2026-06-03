using AedBackend.Core.Models.User;
using AedBackend.Core.Services.User;

namespace AedBackend.Infrastructure.Volunteer;

public sealed class JsonSchoolPersonProfileStore : ISchoolPersonProfileStore
{
    public Task<SchoolPersonProfileRecord> CreateAsync(SchoolPersonProfileRecord profile, CancellationToken ct)
        => throw NotSupported();
    public Task<SchoolPersonProfileRecord?> GetByIdAsync(int id, CancellationToken ct)
        => throw NotSupported();
    public Task<SchoolPersonProfileRecord?> UpdateAsync(int id, SchoolPersonProfileRecord profile, CancellationToken ct)
        => throw NotSupported();
    public Task<bool> DeleteAsync(int id, CancellationToken ct)
        => throw NotSupported();
    public Task<UserIdentityPage<SchoolPersonProfileRecord>> ListAsync(SchoolPersonProfileQuery query, CancellationToken ct)
        => throw NotSupported();
    private static NotSupportedException NotSupported() => new("Phase 7D 学校档案接口仅支持 SQLite 数据源");
}

public sealed class JsonSchoolBindingApplicationStore : ISchoolBindingApplicationStore
{
    public Task<SchoolBindingApplicationRecord> SubmitAsync(string openId, SchoolBindingPayloadRecord payload, CancellationToken ct)
        => throw NotSupported();
    public Task<SchoolBindingApplicationRecord?> GetByIdAsync(int id, CancellationToken ct)
        => throw NotSupported();
    public Task<UserIdentityPage<SchoolBindingApplicationRecord>> ListByUserAsync(string openId, UserIdentityQuery query, CancellationToken ct)
        => throw NotSupported();
    public Task<UserIdentityPage<SchoolBindingApplicationRecord>> ListAllAsync(UserIdentityAdminQuery query, CancellationToken ct)
        => throw NotSupported();
    public Task<bool> DeleteAsync(int id, CancellationToken ct)
        => throw NotSupported();
    private static NotSupportedException NotSupported() => new("Phase 7D 学校绑定接口仅支持 SQLite 数据源");
}

public sealed class JsonResponderClaimApplicationStore : IResponderClaimApplicationStore
{
    public Task<ResponderClaimApplicationRecord> SubmitAsync(string openId, ResponderClaimPayloadRecord payload, CancellationToken ct)
        => throw NotSupported();
    public Task<ResponderClaimApplicationRecord?> GetByIdAsync(int id, CancellationToken ct)
        => throw NotSupported();
    public Task<UserIdentityPage<ResponderClaimApplicationRecord>> ListByUserAsync(string openId, UserIdentityQuery query, CancellationToken ct)
        => throw NotSupported();
    public Task<UserIdentityPage<ResponderClaimApplicationRecord>> ListAllAsync(UserIdentityAdminQuery query, CancellationToken ct)
        => throw NotSupported();
    private static NotSupportedException NotSupported() => new("Phase 7D 急救员认领接口仅支持 SQLite 数据源");
}

public sealed class JsonUserIdentityReviewService : IUserIdentityReviewService
{
    public Task<SchoolBindingApplicationRecord> ApproveSchoolBindingAsync(int id, SchoolBindingApproveCommand command, CancellationToken ct)
        => throw NotSupported();
    public Task<SchoolBindingApplicationRecord> RejectSchoolBindingAsync(int id, UserIdentityRejectCommand command, CancellationToken ct)
        => throw NotSupported();
    public Task<ResponderClaimApplicationRecord> ApproveResponderClaimAsync(int id, ResponderClaimApproveCommand command, CancellationToken ct)
        => throw NotSupported();
    public Task<ResponderClaimApplicationRecord> RejectResponderClaimAsync(int id, UserIdentityRejectCommand command, CancellationToken ct)
        => throw NotSupported();
    private static NotSupportedException NotSupported() => new("Phase 7D 审核接口仅支持 SQLite 数据源");
}
