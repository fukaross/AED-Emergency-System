using AedBackend.Core.Services.Volunteer;

namespace AedBackend.Infrastructure.Volunteer;

public sealed class JsonVolunteerApplicationReviewService : IVolunteerApplicationReviewService
{
    public Task<VolunteerApplicationReviewResult> ApproveAsync(
        int id,
        VolunteerApplicationApproveCommand command,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Phase 7 志愿者审核接口仅支持 SQLite 数据源");
    }

    public Task<VolunteerApplicationReviewResult> RejectAsync(
        int id,
        VolunteerApplicationRejectCommand command,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Phase 7 志愿者审核接口仅支持 SQLite 数据源");
    }
}
