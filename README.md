<div align="center">

<img src="admin/public/favicon.svg" width="84" height="84" alt="AED 急救协作图标" />

# AED 急救协作

校园/园区急救协作系统迁移模板：找设备、发起求助、实时协作、学习急救技能，并提供只读管理端监控。

`uni-app + Vue 3 + TypeScript` · `Vue 3 + Vite + Element Plus` · `ASP.NET Core Minimal API / .NET 10`

</div>

---

## 项目定位

本仓库是从既有 AED 急救系统整理出的**可配置、本地可演示、数据脱敏的迁移模板**。仓库内的 `demo/` 数据是独立合成数据，不包含生产数据库、真实姓名、手机号、微信凭据、地图 Key、服务器地址或上传文件。

当前支持三档运行模式：

| 档位 | 用途 | 后端 | 外部副作用 |
| --- | --- | :---: | --- |
| `mock` | 纯前端 UI/交互预览 | 否 | 不访问后端、不真实拨号、不发送通知 |
| `local-demo` | 本机三端真实联调 | 是，默认 `http://127.0.0.1:5150` | 仅本机 SQLite 和本地上传，演示 code 只在 Development + 回环监听下可用 |
| `wechat` | 真实微信环境 | 是，自备 HTTPS/WSS | 需要真实微信、地图、订阅消息等配置；拒绝演示 code |

> **当前状态（2026-10-28）**：迁移主体与历史用户 UAT 已完成；10 月 28 日审查所列 5 项问题已修复，后端全量测试 438/438 与格式检查通过，一键本机演示启动与管理员登录自检通过。详见 [`docs/VERIFICATION.md`](docs/VERIFICATION.md)。真实微信/真机、地图/GPS、托管 CI、生产部署和 Nginx WebSocket Upgrade 仍未在本模板仓验证。本仓库已选 GNU GPL v3.0-only（`GPL-3.0-only`，见 [`LICENSE`](LICENSE)）；公开发布前仍需确认原闭源项目代码、课程内容、设计素材及第三方组件均可按该许可证再分发，且尚未授权公开发布。

## 主要能力

- AED 设备查询、状态与校区筛选。
- 求助发布、活动求助防重、关单围栏和历史记录。
- 急救会话：认证急救员加入、消息、图片、状态事件、最新位置、HTTP 快照追平和 WebSocket 扇出。
- 求助人脱敏实时视图，以及管理端只读会话监控。
- 急救课堂：课程、关键步骤、统计摘要和阅读量计数。
- 管理端：AED 设备、用户/身份审核、求助、课程、审计日志和会话监控。
- 单一 `config/site.json` 驱动机构名称、地图中心、校区和服务半径。
- 本地演示数据初始化、三角色切换、脱敏扫描和一键启动。

## 目录结构

```text
AED-Emergency-System/
├── config/site.json                 # 机构、地图、校区和服务半径
├── demo/                             # 合成数据、角色和演示校验脚本
├── miniprogram/                      # uni-app 微信小程序
├── admin/                            # Vue 3 + Vite 管理端
├── backend/                          # .NET 10 API、领域层、基础设施与测试
├── scripts/start-local-demo.ps1     # Windows PowerShell 一键本地演示
├── scripts/verify-sanitization.py   # 发布前脱敏扫描器
└── docs/                             # 架构、定制、验证记录
```

## 本地演示启动

### 环境要求

- Windows 11 + PowerShell 7（其他平台未在本仓验证）。
- .NET SDK `10.0.103` 或兼容的 .NET 10 SDK。
- Node.js `22.13+` 或 `24+`。Node 18 和较早的 20.x 不满足当前锁定依赖要求。
- pnpm `10+`。
- Python `3.10+` 仅用于维护者脱敏扫描。

### 推荐：一键准备、启动和登录自检

在仓库根目录执行：

```powershell
pwsh -File scripts/start-local-demo.ps1
```

脚本会：

1. 校验 `config/site.json` 与 `demo/` 合成演示数据。
2. 检查 Node 版本、Admin 构建产物和小程序档位/API 地址；构建缓存缺失或不匹配时自动重建。
3. 在指定空目录初始化 SQLite 演示库。
4. 以 Development + 本机回环方式启动后端。
5. 交互式读取管理员密码，执行 bootstrap、普通登录和 `/api/admin/auth/me` 自检。

管理员密码只在当前进程使用，不写入仓库。常用参数：

```powershell
# 只准备前端产物和 SQLite，不启动服务
pwsh -File scripts/start-local-demo.ps1 -PrepareOnly

# 复用已有演示库；脚本仍会检查构建档位和 API 地址
pwsh -File scripts/start-local-demo.ps1 -SkipInit

# 代码或站点配置变化后强制重建前端
pwsh -File scripts/start-local-demo.ps1 -SkipInit -RebuildFrontends
```

启动后：

- 管理端：`http://127.0.0.1:5150/admin/`
- Swagger：`http://127.0.0.1:5150/swagger`
- 健康检查：`http://127.0.0.1:5150/health`
- 微信开发者工具导入：`miniprogram/dist/build/mp-weixin`

### 手动启动

```powershell
pnpm --dir miniprogram install --frozen-lockfile
pnpm --dir admin install --frozen-lockfile

node demo/tools/verify-demo.mjs

$dataRoot = "$env:TEMP\aed-local-demo"
dotnet run --project backend/src/AedBackend.Api -- init-demo-data --data-root $dataRoot

pnpm --dir admin build

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:AED_DATA_PROVIDER = 'Sqlite'
$env:AED_SQLITE_PATH = "$dataRoot\aed.db"
$env:WeChat__UploadsDirectory = "$dataRoot\uploads"
$env:AED_API_PUBLIC_BASE_URL = 'http://127.0.0.1:5150'
$env:AED_PUSH_ADMIN_KEY = '<本机随机引导密钥>'
$env:AED_ADMIN_SESSION_HASH_SECRET = '<本机随机会话哈希密钥>'
$env:Demo__Enabled = 'true'
dotnet run --project backend/src/AedBackend.Api --launch-profile http
```

另开一个 PowerShell 终端时，环境变量不会自动继承。bootstrap 前必须在第二个终端设置**与后端终端相同的**引导密钥：

```powershell
$env:AED_PUSH_ADMIN_KEY = '<与后端终端相同的本机随机引导密钥>'
curl.exe -X POST http://127.0.0.1:5150/api/admin/auth/bootstrap `
  -H "Content-Type: application/json" `
  -H "X-Aed-Admin-Key: $env:AED_PUSH_ADMIN_KEY" `
  -d '{"username":"admin","password":"<至少8位强密码>"}'
```

构建本地联调小程序：

```powershell
$env:VITE_APP_MODE = 'local-demo'
$env:VITE_API_BASE_URL = 'http://127.0.0.1:5150'
pnpm --dir miniprogram build:mp-weixin
```

## 配置与定制

大多数机构定制只需要修改根目录 [`config/site.json`](config/site.json)：

```json
{
  "schemaVersion": 1,
  "site": {
    "organizationName": "示范园区",
    "appTitle": "AED 急救协作",
    "adminTitle": "AED 管理端",
    "coordinateSystem": "gcj02",
    "map": {
      "center": { "latitude": 31.2315, "longitude": 121.4785 },
      "searchCity": "示例城市"
    },
    "campuses": [
      { "code": "east", "name": "东区", "center": { "latitude": 31.245, "longitude": 121.505 } },
      { "code": "west", "name": "西区", "center": { "latitude": 31.218, "longitude": 121.452 } }
    ],
    "serviceRadiusKm": 30,
    "rescuePhone": ""
  }
}
```

修改配置后的生效方式：

| 修改 | 生效方式 |
| --- | --- |
| `config/site.json` | 后端重启；小程序和 Admin 重新构建 |
| `demo/data/*.json` | 在新的空目录重新运行 `init-demo-data`；再同步小程序演示数据 |
| `miniprogram/.env.*` | 重新构建小程序 |
| `admin/.env.*` | 重新构建 Admin；地图需要 `VITE_AMAP_KEY` / `VITE_AMAP_SECURITY_CODE` |

详细字段说明见 [`docs/CUSTOMIZATION.md`](docs/CUSTOMIZATION.md)。不要把真实 AppSecret、管理密钥、OSS 凭据或地图 Key 写入仓库。

## 开发命令与验证

```powershell
# 后端（仓库根执行）
dotnet build backend/AedBackend.sln
dotnet test backend/AedBackend.sln
dotnet format backend/AedBackend.sln --verify-no-changes

# 小程序
pnpm --dir miniprogram typecheck
pnpm --dir miniprogram lint
pnpm --dir miniprogram test
pnpm --dir miniprogram contract:validate
pnpm --dir miniprogram build:mp-weixin

# Admin
pnpm --dir admin lint
pnpm --dir admin test
pnpm --dir admin build

# 发布前脱敏门禁（维护者工作区需要私有规则文件）
python scripts/verify-sanitization.py --root .
pwsh -File scripts/selftest-sanitization.ps1
```

当前轮次的真实命令、测试数量、失败输出和未验证边界统一记录在 [`docs/VERIFICATION.md`](docs/VERIFICATION.md)。不要用本 README 中的历史数量替代验证记录。

## 架构摘要

- 后端使用 Minimal API，领域模型和服务接口位于 Core，JSON/SQLite、微信适配、上传和站点配置位于 Infrastructure。
- HTTP 是急救会话读写真源；WebSocket 仅负责变化扇出，断线后由 HTTP 快照/增量追平。
- 会话关闭使用 `ClosedAt` 写入围栏，级联清除实时位置并拒绝关闭后的会话写入。
- 小程序通过 auth epoch、请求 generation 和服务端时间校准，避免退出登录、切账号或迟到响应污染当前状态。
- Admin 会话监控保持只读，使用 abort、request sequence 和页面生命周期栅栏防止迟到轮询复活。

详见 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)。

## 已知限制与发布边界

1. 真实微信登录、合法域名、HTTPS/WSS、订阅消息、OSS、GPS、地图底图和真机双人联调未在本仓验证。
2. WebSocket `/ws/help/{id}/session` 的生产 Nginx Upgrade 未在本模板仓验证；本地 HTTP 会话链路和后端定向测试已有记录。
3. 演示课程不包含未授权视频素材，当前示例课程为图文类型。
4. 托管 CI 只提交了工作流定义，尚未在平台上运行。
5. 已选择 GPL-3.0-only；公开发布前仍须核实原项目代码、课程文案、设计素材、图片及第三方组件的再分发权，并完成正式发布授权。

## 许可证与贡献

本项目采用 GNU GPL v3.0-only，完整条款见 [`LICENSE`](LICENSE)。在公开发布前，仍须确认原项目代码与所有随仓库发布的文案、设计素材、图片及第三方组件具备相应授权；贡献流程尚待补充。请勿提交真实数据、凭据、生产配置、未经授权的图片/视频或外部服务密钥。

## 文档索引

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)：分层、会话实时机制和安全边界。
- [`docs/CUSTOMIZATION.md`](docs/CUSTOMIZATION.md)：站点、校区、地图和环境配置。
- [`docs/VERIFICATION.md`](docs/VERIFICATION.md)：本机实跑结果、审查修复记录和未验证项。
- `backend/`：后端项目说明与 ADR。
- `demo/`：合成数据和演示校验说明。





