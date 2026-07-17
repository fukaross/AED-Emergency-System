# ADR 0001：急救会话采用 WebSocket 推送扇出，HTTP 保持读写真源

- 状态：已接受（2026-07-17）
- 关联：GitHub issue [fukaross/REDACTED#1](https://github.com/fukaross/REDACTED/issues/1)；契约 `Design/BackEnd.md` §4.5A

## 背景

急救会话要求状态事件（已拿取 AED / 已到达现场）、文字消息与参与者实时位置在秒级内同步给所有会话参与者。项目现状是全链路 HTTP 轮询、零长连接：

- 小程序与管理端所有数据均通过 `GET` 轮询获取，无服务端主动推送能力；
- 后端为 ASP.NET Core Minimal API，无 SignalR 或 WebSocket 代码与依赖；
- 部署为单实例 systemd 服务（`127.0.0.1:5150`）+ Nginx 反向代理，无横向扩容诉求。

急救场景下轮询的代价：5s 轮询意味着状态同步最坏延迟 5s 以上，且会话页前台持续轮询对电量与弱网不友好。甲方核心诉求（谁拿了 AED、设备实时移动画面）本质是实时协同问题。

## 决策

引入 WebSocket，但严格限定为**推送扇出通道**：

1. `WS /ws/help/{id}/session` 仅向已进入会话的认证急救员推送三类信封：`message` / `participant` / `help_status`。
2. **HTTP API 保持读写真源**：加入会话、发消息、发状态事件、上报位置、拉取快照/增量全部走 HTTP；WS 信封只提示数据已变化，客户端在连接成功、从后台恢复和收到 `message` 信封后通过 HTTP 追平并确认已读；高频 `participant` 信封可直接合并，最终一致性仍以 HTTP 快照为准。
3. 客户端 → 服务端的 WS 消息一律忽略，通道内不实现任何业务逻辑。
4. 断线降级：WS 建连失败、报错或断开时，客户端显式回退到 `GET /api/help/{id}/session?afterSeq=` 轮询；重新连接成功后立即通过 HTTP 追平，保证弱网与前后台切换场景功能可用。
5. 分期落地：一期仅小程序会话页使用 WS；求助人只读视图（`GET /api/help/{id}/rescue-view`）与管理端监控页（`/api/admin/rescue-sessions*`）继续轮询，后续期再评估是否铺开。
6. 实现采用 ASP.NET Core 内置 `System.Net.WebSockets`（`app.UseWebSockets()`），**不引入 SignalR**。

## 备选方案

| 方案 | 否决理由 |
|------|----------|
| 维持全轮询（5s 短轮询） | 最坏延迟高、前台轮询耗电耗流量；甲方"实时看到 AED 移动"的诉求体验差。但保留为 WS 断线降级路径。 |
| SSE（Server-Sent Events） | 微信小程序不支持原生 SSE；管理端可用但两端技术栈分裂。 |
| SignalR | 引入完整框架（连接管理、协议协商、横向扩展背板）远超"扇出三类信封"的需求；单实例部署用不到其扩展能力；增加依赖与运维面。 |
| 微信订阅消息推送状态变化 | 一次授权只能推送一次，无法持续触达；共识第 15 条已明确状态变化不发订阅消息。 |

## 后果

- 正向：会话内状态与位置同步达到近实时；弱网降级路径明确（回退轮询）；HTTP 契约仍是唯一事实源，测试主接缝不变（HTTP 集成测试 + Store 层测试）。
- 代价：后端新增长连接生命周期管理（连接注册/注销/扇出）；Nginx 需放行 WS 升级请求（部署时确认 `Upgrade` / `Connection` 头转发）；测试需补 WS 连接/鉴权 smoke。仓库提供 `deploy/nginx/aed-api-websocket.conf.example` 作为上线配置基线。
- 约束：WS 层不得生长业务逻辑；任何写诉求只能回到 HTTP 端点。若未来铺开求助人视图/管理端，沿用同一信封模型。
- 鉴权：WS 握手使用 `Authorization: Bearer <opaque-token>` 请求头，校验逻辑复用现有 token hash、账号状态与会话参与者判定；禁止将 30 天有效的凭证放入 URL query、代理访问日志或错误页面。
- 位置 TTL：服务端和客户端均以最新位置的 `reportedAt` 为依据计算 45 秒灰显、90 秒移除；普通互动时间 `lastSeenAt` 不得延长位置有效期。健康 WS 不额外广播计时事件，客户端用本地时钟推进显示状态。
