using AedBackend.Core.Models.Help;
using AedBackend.Core.Services.Rescue;

namespace AedBackend.Api.Endpoints;

internal sealed record RescueSessionWriteResult<T>(T? Value, IResult? Failure)
    where T : class;

internal static class RescueSessionWriteGuard
{
    public static async Task<RescueSessionWriteResult<T>> ExecuteActiveAsync<T>(
        RescueSessionActiveWrite<T> input)
        where T : class
    {
        using var lease = await input.Services.Lifecycle.EnterAsync(
            input.HelpId,
            input.CancellationToken);
        var help = await input.Services.HelpStore.FindByIdOrRequestNoAsync(
            input.HelpId,
            input.CancellationToken);
        if (help is null)
            return new RescueSessionWriteResult<T>(null, RescueSessionContext.NotFound());
        if (HelpStatuses.IsClosed(help.Status))
            return new RescueSessionWriteResult<T>(null, RescueSessionContext.SessionClosed());
        return await ExecuteAsync(input.Write);
    }

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

internal sealed record RescueSessionActiveWrite<T>
    where T : class
{
    public required string HelpId { get; init; }
    public required SessionEndpointServices Services { get; init; }
    public required Func<Task<T>> Write { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}
