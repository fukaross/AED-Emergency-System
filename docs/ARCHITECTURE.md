# 架构说明

本文只描述**已经实现并在本地验证过**的结构；未验证能力见 [VERIFICATION.md](VERIFICATION.md)。

## 1. 三端与共享配置

```
config/site.json ──┐
                   ├─→ miniprogram/config/site-config.ts（构建时读取并校验）
                   ├─→ admin/src/config/site-config.ts（构建时读取并校验）
                   └─→ backend（csproj Content 链接到输出目录 config/site.json，启动时校验）

demo/ ──→ demo/tools/verify-demo.mjs（校验）
      ──→ backend init-demo-data（JSON → SQLite，补齐学校档案/绑定）
      ──→ miniprogram/scripts/sync-demo-data.mjs → mock/generated/*.generated.ts
```

- 站点内容（机构名、标题、校区 code/名称/中心、检索城市、服务半径、急救热线）只有一个手工维护真源：
  `config/site.json`（`schemaVersion=1`）。三端不各写一份，也不用环境变量覆盖站点内容。
- 校区 code 是**配置派生字符串**，不是源码枚举：新增第三校区只改配置与演示数据，未知 code 走参数错误，不静默回落。
- 距离语义统一为 Haversine + 整数米四舍五入；`distance > serviceRadiusKm*1000` 才算超半径，等于边界不判离线。

## 2. 后端分层（Api → Core → Infrastructure）

| 层 | 职责 |
| --- | --- |
| `AedBackend.Api` | 端点、鉴权入口、WebSocket 扇出、启动门禁（站点配置校验、Demo 模式校验）、CLI（`init-demo-data` / 迁移器） |
| `AedBackend.Core` | DTO/模型/服务接口/校验器/错误码，不依赖基础设施 |
| `AedBackend.Infrastructure` | JSON 与 SQLite 双 provider、演示模式、站点配置加载、上传、微信客户端 |

数据源由 `Data:Provider` 在 `Json`（回滚源）与 `Sqlite`（完整能力，本地演示默认）之间切换，**API 契约不随 provider 改变**。

## 3. 急救会话：HTTP 权威 + WS 扇出

- **HTTP 是读写真源**：会话快照、增量、消息、状态事件、位置、`rescue-view` 均走 HTTP，带明确的鉴权与错误码。
- **WebSocket 只做推送扇出**（`/ws/help/{id}/session`，`Authorization: Bearer`）：握手鉴权、每连接发送锁；
  非终态信封只作“有变化”提示，客户端必须用 HTTP 追平并维护私有 confirmed seq。
- 断线降级为轮询；终态信封是唯一可立即提交的 WS 特例，且由服务端关闭栅栏保证。
- **位置 TTL**：前台 5s 上报，客户端按 `location.reportedAt` + 服务端 `serverTime` 校准推进 45s 灰显 / 90s 移除；
  后端只存最新位置，关单即清除（含误报、游客关单、管理端关单三条路径）。
- **关闭围栏**：发布初始化、加入、关单与所有会话写入在同一键上串行并持锁重查状态，避免“关单后又被写入”。

## 4. 客户端安全与生命周期

- **auth epoch / account epoch**：logout、刷新、切账号后，迟到的请求结果不得写回；401/1002/1018 统一终止认证，
  业务 403（如急救员资格不足）不触发登出。
- 页面隐藏/卸载、切会话、关单都有 generation/epoch 隔离，避免轮询复活与迟到响应覆盖新状态。
- 求助详情权限收紧为「求助人 + 认证急救员（+ 管理员）」；`rescue-view` 仅求助人或已加入参与者可见。
- 管理端会话监控是**严格只读**（列表/详情轮询、abort + request sequence + 显隐暂停 + 关闭即停）。
- 演示模式统一禁止真实拨号（`utils/dial.ts`）、真实订阅发送、OSS 上传与外发；订阅入口仅在 `wechat` 档出现。

## 5. 公开边界

- 公开源码、文档与合成演示数据组成交付集；依赖、构建产物、运行数据与本地秘密由根 `.gitignore` 排除。
- `backend/src/AedBackend.Api/wwwroot/admin/` 是 Admin 构建产物（可重建），由 `.gitignore` 排除，
  但发布前仍纳入脱敏扫描（“实际构建产物”必须存在，否则扫描失败）。
- 维护者的 Agent 指令、任务看板、报告与私有检测规则仅由本地 `.git/info/exclude` 排除，不写入公开 `.gitignore`。脱敏扫描器内置识别这些本地目录和指令文件；历史迁移另行检查所有可达提交的路径和内容。
