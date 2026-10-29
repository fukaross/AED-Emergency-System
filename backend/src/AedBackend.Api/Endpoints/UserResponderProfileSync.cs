using AedBackend.Core.Models.Responder;
using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.Help;

namespace AedBackend.Api.Endpoints;

internal static partial class UserEndpoints
{
    private static async Task SyncBoundResponderProfileAsync(
        string openId,
        UpdateExperienceUserProfilePatch patch,
        IResponderStore responders,
        CancellationToken ct)
    {
        var sync = CreateResponderSyncPatch(patch);
        if (!sync.HasChanges) return;

        var bound = (await responders.GetAllAsync(ct))
            .FirstOrDefault(item => item.BoundOpenId == openId);
        if (bound is null) return;

        var updated = ApplyResponderSyncPatch(bound, sync);
        var saved = await responders.UpdateAsync(bound.Id, updated, ct);
        if (saved is null)
            throw new InvalidOperationException("已绑定急救员档案同步失败");
    }

    private static ResponderSyncPatch CreateResponderSyncPatch(
        UpdateExperienceUserProfilePatch patch)
    {
        return new ResponderSyncPatch(
            NonEmpty(patch.Name),
            NonEmpty(patch.AvatarUrl),
            NonEmpty(patch.Phone));
    }

    private static ResponderProfileRecord ApplyResponderSyncPatch(
        ResponderProfileRecord profile,
        ResponderSyncPatch patch)
    {
        return profile with
        {
            Name = patch.Name ?? profile.Name,
            AvatarUrl = patch.AvatarUrl ?? profile.AvatarUrl,
            Phone = patch.Phone ?? profile.Phone
        };
    }

    private static string? NonEmpty(PatchValue<string?> value)
    {
        return value is { HasValue: true, Value: { } text }
            && !string.IsNullOrWhiteSpace(text)
                ? text.Trim()
                : null;
    }

    private sealed record ResponderSyncPatch(
        string? Name,
        string? AvatarUrl,
        string? Phone)
    {
        public bool HasChanges => Name is not null || AvatarUrl is not null || Phone is not null;
    }
}
