using AedBackend.Core.Models.Volunteer;
using AedBackend.Core.Services.Volunteer;

namespace AedBackend.Infrastructure.Volunteer;

public sealed class JsonVolunteerApplicationStore : IVolunteerApplicationStore
{
    public Task<VolunteerApplicationRecord> SubmitAsync(
        string openId,
        VolunteerApplicationPayloadRecord payload,
        int? derivedFromId,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Phase 7 志愿者申请接口仅支持 SQLite 数据源");
    }

    public Task<VolunteerApplicationRecord?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Phase 7 志愿者申请接口仅支持 SQLite 数据源");
    }

    public Task<VolunteerApplicationPage> ListByUserAsync(
        string openId,
        VolunteerApplicationQuery query,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Phase 7 志愿者申请接口仅支持 SQLite 数据源");
    }

    public Task<VolunteerApplicationPage> ListAllAsync(
        AdminVolunteerApplicationQuery query,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Phase 7 志愿者申请接口仅支持 SQLite 数据源");
    }
}
