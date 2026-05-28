using AedBackend.Core.Models.Responder;

namespace AedBackend.Core.Services.Help;

public interface IResponderStore
{
    Task<IReadOnlyList<ResponderProfileRecord>> GetAllAsync(CancellationToken cancellationToken);

    Task<ResponderProfileRecord?> FindByIdAsync(string id, CancellationToken cancellationToken);

    Task<ResponderProfileRecord?> UpdateAsync(
        string id,
        ResponderProfileRecord updated,
        CancellationToken cancellationToken);
}
