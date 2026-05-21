using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.WeChat;

namespace AedBackend.Api.Endpoints;

internal static class EndpointUsers
{
    public static async Task<ExperienceUserRecord?> FindCurrentUserAsync(
        HttpRequest request,
        ISessionTokenService tokenService,
        IExperienceUserStore store,
        CancellationToken cancellationToken)
    {
        var token = EndpointSecurity.ReadBearerToken(request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return await store.FindByTokenHashAsync(
            tokenService.HashToken(token),
            cancellationToken);
    }
}
