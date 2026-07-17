using AedBackend.Core.Services.Rescue;

namespace AedBackend.Api.Endpoints;

internal sealed record RescueSessionWriteResult<T>(T? Value, IResult? Failure)
    where T : class;

internal static class RescueSessionWriteGuard
{
    public static async Task<RescueSessionWriteResult<T>> ExecuteAsync<T>(Func<Task<T>> write)
        where T : class
    {
        try
        {
            return new RescueSessionWriteResult<T>(await write(), null);
        }
        catch (RescueSessionClosedException)
        {
            return new RescueSessionWriteResult<T>(null, RescueSessionContext.SessionClosed());
        }
    }
}
