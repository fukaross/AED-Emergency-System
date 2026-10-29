using AedBackend.Core.Models.WeChat;
using AedBackend.Core.Services.WeChat;
using AedBackend.Infrastructure.WeChat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AedBackend.Infrastructure.Demo;

/**
 * CONTRACT-003 的受限本地身份适配：复用 POST /api/auth/wechat-login 与 IWeChatLoginClient，
 * Demo 开启时把 roles.json 的固定 loginCode 映射到对应 openId，不访问微信、不接受任意 OpenId；
 * 未知 code 返回与微信 invalid code 相同语义的错误（由端点映射为既有参数错误）。
 * Demo 关闭（真实模式）时完全委托真实微信客户端，演示 code 不会命中本地映射。
 */
public sealed class DemoAwareWeChatLoginClient(
    IOptions<DemoOptions> demoOptions,
    DemoRoleCatalog roles,
    WeChatLoginClient inner,
    ILogger<DemoAwareWeChatLoginClient> logger) : IWeChatLoginClient
{
    /** 与微信 jscode2session 的 invalid code 语义一致，端点会映射为 HTTP 400 / code 1001。 */
    public const int UnknownDemoCodeErrorCode = 40029;

    public Task<WeChatCodeSessionResult> CodeToSessionAsync(
        string code,
        CancellationToken cancellationToken)
    {
        if (!demoOptions.Value.Enabled)
        {
            return inner.CodeToSessionAsync(code, cancellationToken);
        }

        var role = roles.FindByLoginCode(code);
        if (role is null)
        {
            logger.LogWarning("演示登录被拒绝：未知演示 code（未调用微信接口）");
            return Task.FromResult(new WeChatCodeSessionResult(
                false,
                null,
                UnknownDemoCodeErrorCode,
                "演示模式仅接受固定演示 code"));
        }

        logger.LogInformation(
            "演示登录：role={Role} openId={OpenId}（本地受限适配，未调用微信接口）",
            role.Role,
            role.OpenId);
        return Task.FromResult(new WeChatCodeSessionResult(
            true,
            new WeChatLoginSession(role.OpenId, null),
            0,
            "demo-ok"));
    }
}
