# AedBackend 后端

ASP.NET Core Minimal API (.NET 10)，分层 `Api → Core → Infrastructure`。
数据源通过 `Data:Provider` 在 `Json`（只读/回滚）与 `Sqlite`（完整能力，默认用于本地演示）之间切换，
API 契约不随 provider 改变；JSON provider 不提供学校身份/绑定等 SQLite 专属能力。

本文件只写已经实现并可在本地执行的入口；根 README 由整合任务汇总。

## 1. 必需输入

| 输入 | 位置 | 说明 |
| --- | --- | --- |
| 站点配置 | 仓库根 `config/site.json` | 构建时链接为输出目录 `config/site.json`，后端启动即校验；`schemaVersion` 必须为 1 |
| 演示角色 | 仓库根 `demo/roles.json` | 构建时链接为 `config/demo-roles.json`（只读产物），演示 code 的唯一来源 |
| 演示数据 | 仓库根 `demo/data/*.json` | 构建时链接为 `config/demo-data/*.json`，供 `init-demo-data` 消费 |

## 2. 从空目录初始化演示数据

```powershell
dotnet build backend/AedBackend.sln
dotnet run --project backend/src/AedBackend.Api -- init-demo-data --data-root <本地绝对目录>
```

- `--data-root` 必须是绝对路径，且只允许本仓目录下（`config/`、`demo/`、`backend/src` 等源目录除外）、
  系统临时目录，或环境变量 `AED_DEMO_LOCAL_DATA_ROOTS`（`;` 分隔）声明的本地数据根。
- 目标目录必须为空或不存在；命令不提供强制重置/覆盖/删除开关，也不会启动服务、发 HTTP 或创建管理员。
- 成功时打印数据目录、SQLite、上传目录的绝对路径与各表计数；失败以非零退出码结束，不留下“看似完整”的数据库。
- 该命令只做数据初始化：建库复用既有 JSON→SQLite 迁移器，并补齐 `demo/roles.json` 的学校档案与
  `openId` 绑定关系，最后对账三个演示角色确实满足现有真实授权规则。

## 3. 启动本地演示后端

`init-demo-data` 成功后，用环境变量指向同一份数据（示例值请自行替换，不要提交到仓库）：

```powershell
$env:AED_DATA_PROVIDER = "Sqlite"
$env:AED_SQLITE_PATH = "<data-root>\aed.db"
$env:WeChat__UploadsDirectory = "<data-root>\uploads"
$env:AED_PUSH_ADMIN_KEY = "<本机随机引导密钥>"
$env:AED_ADMIN_SESSION_HASH_SECRET = "<本机随机会话哈希密钥>"
$env:Demo__Enabled = "true"
dotnet run --project backend/src/AedBackend.Api --launch-profile http
```

`--launch-profile http` 监听 `http://127.0.0.1:5150`。

演示模式的启用条件是 **Development 环境 + `Demo:Enabled=true` + 仅回环监听**，并且要求
`Data:Provider=Sqlite`、`Upload:Provider=Local`（禁止 OSS）、`WeChat:PublicBaseUrl` 指回本机。
任一条件不满足都会在启动时报错退出；真实模式（`Demo:Enabled=false`）不接受演示 code。

## 4. 管理员初始化与普通登录

引导密钥只用于首次初始化，请使用本机随机值，不要预置弱口令：

```powershell
# 1) 创建首个超级管理员（header 携带引导密钥）
curl -X POST http://127.0.0.1:5150/api/admin/auth/bootstrap `
  -H "Content-Type: application/json" `
  -H "X-Aed-Admin-Key: $env:AED_PUSH_ADMIN_KEY" `
  -d '{"username":"admin","password":"<你输入的强密码>"}'

# 2) 普通登录：返回 adminKey（等价于 Admin 端的会话 key）
curl -X POST http://127.0.0.1:5150/api/admin/auth/login `
  -H "Content-Type: application/json" `
  -d '{"username":"admin","password":"<你输入的强密码>","deviceId":"local-admin-device"}'
```

重复 bootstrap 保留既有 HTTP 409 语义（`code 2019`）；登录后的 `adminKey` 放在 `X-Aed-Admin-Key` 头访问 `/api/admin/*`。

## 5. 演示身份登录（仅本地演示模式）

```powershell
# 固定演示 code：demo-requester / demo-rescuer / demo-unqualified
curl -X POST http://127.0.0.1:5150/api/auth/wechat-login `
  -H "Content-Type: application/json" -d '{"code":"demo-requester"}'
```

演示 code 只把 `demo/roles.json` 的 `loginCode` 映射到对应 `OpenId`，之后仍走原有 token、账号状态、
急救员资格与业务授权；未知 code 返回既有参数错误（HTTP 400 / code 1001），账号禁用返回 HTTP 403 / code 1018。
演示模式不发送真实微信订阅消息（返回“演示未发送”状态与日志）、禁止 OSS 上传，手机号只来自固定演示角色，
`POST /api/auth/wechat-phone` 与资料手机号修改在该模式下明确拒绝。

## 6. 配置键与环境变量

| 配置键 | 环境变量（别名） | 说明 |
| --- | --- | --- |
| `Data:Provider` | `AED_DATA_PROVIDER` | `Json` / `Sqlite` |
| `Data:SqlitePath` | `AED_SQLITE_PATH` | SQLite 文件绝对/相对路径 |
| `WeChat:AppId` | `WECHAT_APP_ID` | 真实微信模式必填 |
| `WeChat:AppSecret` | `WECHAT_APP_SECRET` | 真实微信模式必填；不得入仓 |
| `WeChat:SubscribeTemplateId` | `WECHAT_SUBSCRIBE_TEMPLATE_ID` | 真实模式求助推送模板 |
| `WeChat:AdminKey` | `AED_PUSH_ADMIN_KEY` | 管理引导密钥 |
| `Admin:SessionHashSecret` | `AED_ADMIN_SESSION_HASH_SECRET` | 管理会话哈希密钥（登录必需） |
| `WeChat:PublicBaseUrl` | `AED_API_PUBLIC_BASE_URL` | 上传回链前缀；演示模式必须指回本机 |
| `Upload:Provider` | `AED_UPLOAD_PROVIDER` | `Local` / `Oss` |
| `Upload:Oss:*` | `AED_OSS_*` | 真实 OSS 凭据，只来自本地环境 |
| `Demo:Enabled` | `Demo__Enabled` | 受限本地演示开关（仅 Development + 回环） |

站点内容（机构名、标题、校区、服务半径、地图中心）只来自 `config/site.json`，不允许用环境变量覆盖；
修改后需要重新构建并重启后端，不承诺热更新。

### 本机秘密文件

`appsettings.Development.local.json` 不会被 .NET 默认加载，后端在 Development 下显式加入它；
加入后重新追加环境变量与命令行来源，因此环境变量/CLI 仍然优先。可参考
`src/AedBackend.Api/appsettings.Development.local.example.json`：

```json
{
  "WeChat": { "AdminKey": "<本机随机引导密钥>" },
  "Admin": { "SessionHashSecret": "<本机随机会话哈希密钥>" }
}
```

该文件已在 `backend/.gitignore` 中忽略；任何情况下都不要把真实 AppSecret、管理密钥或 OSS 凭据写入仓库。
