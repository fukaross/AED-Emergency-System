using System.Net;

namespace AedBackend.Tests.Admin;

public sealed class AdminCompatibilityApiTests : AdminApiTestBase
{
    [Fact]
    public async Task OpsEndpoints_AcceptLegacyKeyAndAdminSession()
    {
        SeedHelpRequests(Help("help-1"));

        UseLegacyKey();
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/ops/help-requests")).StatusCode);

        UseAdminKey(await BootstrapAndLoginAsync());
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/ops/help-requests")).StatusCode);
    }

    [Fact]
    public async Task DevWeChatEndpoints_AcceptLegacyKeyAndAdminSession()
    {
        SeedExperienceUsers(("openid-1", "REDACTED"));

        UseLegacyKey();
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/dev/wechat/experience-users")).StatusCode);

        UseAdminKey(await BootstrapAndLoginAsync());
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/dev/wechat/experience-users")).StatusCode);
    }
}
