# AED Backend

> REDACTED校园 AED 急救微信小程序后端 — ASP.NET Core Minimal API

## 仓库信息

- 远程：`https://github.com/fukaross/REDACTED`（private，main 分支）
- 相邻仓库：
  - `Design/` — 前端工程 + 设计/接口规格（`fukaross/REDACTED`）
  - `Admin/` — 管理端 Web SPA（`fukaross/AED-Admin`，private）

## 技术栈

| 项 | 选型 |
|----|------|
| 框架 | ASP.NET Core Minimal API（.NET 10） |
| 分层 | `Api`（端点）→ `Core`（DTO/模型/接口/错误码）→ `Infrastructure`（持久化/微信/上传） |
| 数据 | JSON provider（默认，自动写种子）/ SQLite + EF Core（生产） |
| 认证 | 小程序侧自定义 token；Admin 侧账号密码 + session key（均非 JWT） |
| 上传 | Local（默认） / 阿里云 OSS |
| 实时 | WebSocket 推送扇出（急救会话，HTTP 为读写真源） |

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## 本地命令

```powershell
# 构建（4 个项目，0 errors / 0 warnings）
dotnet build

# 运行单元测试（tests/AedBackend.Tests）
dotnet test

# 格式校验
dotnet format --verify-no-changes

# 本地启动（默认 JSON provider，首次运行自动生成 data/ 种子文件）
dotnet run --project src/AedBackend.Api
```

启动后可访问：

- `GET /health` — 健康检查
- `GET /` — 服务信息
- `GET /api/aed/nearby?lat=19.974447&lng=110.517041` — 附近 AED（种子 24 台）
- Swagger（Development 环境）：`/swagger`

## 数据 Provider

默认 `Json`：`data/*.json`，文件不存在时自动写种子（AED 设备、课程、急救员、统计横幅等）。

生产使用 `Sqlite`：

```powershell
# 1. 先显式迁移（不要手工建库）
dotnet run --project src/AedBackend.Api -- migrate-json-to-sqlite --data-root <JSON目录> --db <SQLite路径>

# 2. 设置环境变量后启动
$env:AED_DATA_PROVIDER = "Sqlite"
$env:AED_SQLITE_PATH = "data/aed.db"
dotnet run --project src/AedBackend.Api
```

> 使用 Sqlite provider 时若数据库不存在，启动会显式报错并要求先执行迁移命令，不会自动建库。

## 必需环境变量

密钥均**不提交仓库**，部署时通过环境变量或 `appsettings.json` 注入：

| 变量 | 说明 |
|------|------|
| `WECHAT_APP_ID` / `WECHAT_APP_SECRET` | 微信小程序凭据；为空时微信登录/订阅链路不可用 |
| `WECHAT_SUBSCRIBE_TEMPLATE_ID` | 订阅消息模板 ID |
| `AED_PUSH_ADMIN_KEY` | Admin bootstrap / reset-super-admin 初始化密钥 |
| `AED_ADMIN_SESSION_HASH_SECRET` | Admin session HMAC-SHA256 密钥；缺失时 Admin 登录返回 500 |
| `AED_API_PUBLIC_BASE_URL` | 对外基础地址（推送跳转等用） |
| `AED_DATA_PROVIDER` / `AED_SQLITE_PATH` | 数据 Provider 与 SQLite 路径（别名见上） |
| `Upload__Provider` / `Upload__Oss__*` | 上传存储：`Local` 或 `Oss`（Endpoint/BucketName/AccessKeyId/AccessKeySecret） |

## 部署边界

- `data/`（运行时数据）、`publish/`、`obj/`、`bin/` 均不入库。
- `src/AedBackend.Api/wwwroot/admin/` 由 Admin 端 `pnpm build` 生成（被 .gitignore 忽略），本服务通过 `/admin/` 静态托管。
- 生产运行数据必须保留在 `/opt/REDACTED/data`，不得回落到 `app/data`；任何部署不得覆盖线上运行时数据。
- 管理端正式契约统一走 `/api/admin/*`；`/api/ops/*`、`/api/dev/*` 仅作兼容。

## 关键文档

| 文档 | 用途 |
|------|------|
| [AGENTS.md](AGENTS.md) | 后端真实进展、运行约束与协作约定 |
| [Design/BackEnd.md](../Design/BackEnd.md) | 后端接口权威规格（API / 数据库 / 错误码） |
| [docs/adr/](docs/adr/) | 架构决策记录（如 WS 选型 ADR） |
| [deploy/](deploy/) | systemd / Nginx 配置示例 |
