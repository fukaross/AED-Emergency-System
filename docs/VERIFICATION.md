# 验证记录（2026-10-27；2026-10-28 补充审查与问题修复）

验证结果按轮次记录，不用旧仓库历史测试数代替当前结果。§1–7 保留 2026-10-27 独立验收者的执行记录与 Lead 复核结论；§8 为当前状态，§9 记录 2026-10-28 只读审查的发现、实际验证与待办。

## 1. 环境

| 项 | 值 |
| --- | --- |
| OS | Windows 11（本仓只在 Windows PowerShell 7 下验证；其他平台未验证） |
| .NET SDK | 10.0.103 |
| Node.js / pnpm | 24.22.1 / 10.17.1 |
| Python | 3.10.11（含 PyYAML 6.0.2，仅维护者本地脱敏工具需要） |
| 本地后端 | `http://127.0.0.1:5150`（launchSettings `http` profile） |

## 2. 脱敏与公开边界（AC-008 / TEST-008）

> 脱敏扫描器、反例自检脚本与私有规则由维护者在本地工作区保留，不随本仓库发布；以下为本地执行结果。

结果（2026-10-27 本机执行，Admin 重建后）：

- 交付源码 **808** 个文件 + 实际构建产物 **82** 个文件（`backend/src/AedBackend.Api/wwwroot/admin`）= 890，
  其中文本 **876**、跳过二进制 13。
- 本地允许清单 **148** 条精确例外（文件路径 + 字段 + 值 + 类别 + 合成来源），已登记移除的导出文件 5 个；
  本次豁免 **173** 处（含同一文件多行命中）。
- **0 处违规 / 0 处边界或清单问题 → exit 0**。**16** 处 `WARN` 为“内部审计事件名”“引号内通用词”“回环地址掩码”，
  不计失败（非凭据/个人信息）。
- 检测项覆盖：旧机构/旧域名/旧校区/旧服务器/旧 AppID/旧坐标残留；AppSecret/管理密钥/token/私钥/OSS 凭据；
  身份证、手机号、真实姓名；交付边界（`_agent-team/`、`node_modules`、`dist`、`bin/obj`、运行 `data/`、
  `uploads/`、非样例 `.env`、本地秘密、`wwwroot/admin` 构建产物）与导出允许清单一致性。
- 缺预期构建产物或扫描到 0 个文本文件 → 非零退出（已验证：删除构建产物时扫描器失败）。

隔离反例自检（维护者本地脚本）：

- 在系统临时目录创建样本：旧机构词元转写、合成假 AppSecret/OSS Key/私钥块、真实格式假身份证与假手机号。
- 扫描器对样本 **exit 1**，逐行定位到样本文件（80–88 处命中，含 `credential-appsecret`、`personal-id-number`、
  `personal-mobile` 等类别），且不回显命中值。
- 样本目录随即删除（不在仓内、不进交付包）；随后对交付集复扫 **exit 0**。

## 3. 干净环境首次启动闭环（AC-004 / TEST-004）

```powershell
# 1) 配置与演示数据校验（不联网、不写文件）
node demo/tools/verify-demo.mjs

# 2) 空目录初始化 SQLite 演示数据（不启动服务、不建管理员）
dotnet run --project backend/src/AedBackend.Api -- init-demo-data --data-root <系统临时目录>\aed-demo-<随机>

# 3) Development + 回环 + Demo 开关启动后端（127.0.0.1:5150）

# 4) 管理员 bootstrap（header X-Aed-Admin-Key）→ 普通登录（deviceId）
```

结果：

- `verify-demo.mjs` 423/423 通过（站点配置 + 演示数据引用/校区/距离性质，含 30 km 边界语义）。
- `init-demo-data` **exit 0**：生成 `aed.db`（393,216 B）与 `uploads/`；表计数 `experience_users=7`、
  `responder_profiles=7`、`aed_devices=10`、`courses=4`、`quick_actions=2`、`knowledge_summary=1`、
  `school_person_profiles=3`、`school_binding_applications=3`；求助/会话/管理员初始为空。
- 演示角色对账：`requester`（非急救员）、`active-rescuer`（`DEMO-RSP-001`，certified=True）、
  `unqualified`（`DEMO-RSP-002`，certified=False），三者 `schoolBinding=True`。
- 后端启动后 `/health` **200**；`POST /api/admin/auth/bootstrap` **200**；重复 bootstrap **409 / code 2019**；
  普通 Admin 登录 + `deviceId` **200**，`GET /api/admin/auth/me` **200**（super_admin）。
- **持久化**：停进程后用同一 SQLite 重启 → `/health` 200，Admin 与演示求助人重新登录 200；
  `nearby` 200（9 条）、课程 200（4 门）、求助历史 200（1 条 closed）保持不变。
- 负向：非空目录/已存在库执行初始化 → **exit 2** 且不覆盖；未登录访问受保护端点 **401/1002**；
  Demo 关闭时演示 code 登录 **400/1001** 并明确提示缺真实微信配置（不静默降级、不外发）。

## 4. 业务、权限与实时语义（AC-005 / TEST-005、TEST-006）

使用同一新 SQLite 与真实 HTTP（非 Mock）：

- 三个演示角色登录均 **200**（经真实登录接口换 bearer）。
- 求助发布 → 认证急救员加入 → 消息（`seq=2`）→ 状态事件 taken_aed / arrived / 撤销（`seq` 3–5）
  → 另一端 HTTP 快照 `latestSeq=5`（**HTTP 为权威**，WS 只作变化提示）→ 求助人 `rescue-view` 200
  → Admin 只读会话列表/详情 200。
- 权限负向：`unqualified` 加入会话/读私有快照 **403/1003**；未登录 **401/1002**；
  关单后继续写入 **409/1016**；会话关闭后的位置与实时任务清理由既有定向测试覆盖。
- 配置驱动：临时新增第三校区 `north` 后重启，后端接受新 code（Admin 更新急救员校区 200）；
  未知 code `nowhere` **400/1001**；验收后 `config/site.json` 已恢复，SHA-256 与验收前一致。

## 5. 三端工程门禁（独立验收者实跑）

| 端 | 命令 | 结果 |
| --- | --- | --- |
| backend | `dotnet build backend/AedBackend.sln` | exit 0，4 项目，0 warning / 0 error |
| backend | `dotnet test backend/AedBackend.sln` | **432/433**；唯一失败为既有并发用例 `StoreJoin_ConcurrentDuplicate_ReturnsExactlyOneNewParticipant`，**隔离重跑通过**，未修改测试（另一轮全量曾 433/433） |
| backend | `dotnet format backend/AedBackend.sln --verify-no-changes` | exit 0 |
| miniprogram | `pnpm typecheck` / `pnpm lint` | exit 0 / exit 0（0 warning） |
| miniprogram | `pnpm test` | **352/352**（60 files） |
| miniprogram | `pnpm contract:validate` | exit 0（OpenAPI 2 条既有 warning：license 表述、allOf 单 ref） |
| miniprogram | `pnpm build:mp-weixin` | exit 0，`dist/build/mp-weixin` **261 文件 / 642,870 B** |
| admin | `pnpm lint` / `pnpm test` | exit 0 / **102/102**（19 files） |
| admin | `pnpm build` | exit 0，输出 `backend/src/AedBackend.Api/wwwroot/admin`，**82 文件 / 2,635,073 B**，标题读自 `config/site.json` |
| 演示数据 | `node demo/tools/verify-demo.mjs` | 423/423 通过 |

这轮后端全量结果为 432/433，失败用例隔离重跑通过，另一轮全量曾为 433/433。失败根因尚未确认；
2026-10-28 审查撤回“隔离必过、属既有测试特性”的定性。后续应保留失败输出并定位原因，再决定修复范围。

## 6. 未验证项（必须在交付说明中如实保留）

- 托管 CI（GitHub Actions）**未在平台上运行**；工作流只做了本地 YAML 解析与命令一致性核对。
- 真实微信档：`wx.login`、合法域名、HTTPS/WS、订阅消息、OSS 上传、真机渲染与双人真机联调均**未验证**。
- 微信开发者工具渲染与交互、真实 GPS、地图底图（无 Key）、图片报告上传、位置 TTL 的界面表现**未验证**。
- WebSocket（`/ws/help/{id}/session`）握手与帧**未实测**；HTTP 追平语义已验证。
- 禁用账号 403/1018 未单独界面实操（后端定向测试覆盖）。
- 第三校区**前端**运行期生效未重建验证（后端已通过）。
- 视频课程分支未覆盖（无授权视频素材，4 门演示课程均为 `article`）。
- 非 Windows 平台、`dotnet publish`、生产部署与 Nginx `/ws/` Upgrade 未验证。
- 代码与素材再分发权（OPEN-003）与候选许可证（MIT 未定稿）待用户确认。

## 7. 复现方式

```powershell
# 完整门禁（按仓库根执行）
dotnet build backend/AedBackend.sln
dotnet test backend/AedBackend.sln
dotnet format backend/AedBackend.sln --verify-no-changes
cd miniprogram; pnpm install --frozen-lockfile; pnpm typecheck; pnpm lint; pnpm test; pnpm contract:validate; pnpm build:mp-weixin; cd ..
cd admin; pnpm install --frozen-lockfile; pnpm lint; pnpm test; pnpm build; cd ..

# 脱敏检查由维护者在本地工作区执行（Admin 构建产物必须存在；工具与私有规则不随仓库发布）
```

## 8. 验收状态

- 2026-10-27 本地技术验收记录已形成，包含干净启动、业务/权限与脱敏检查；后端全量测试结果按 §5 的 432/433 与隔离重跑记录保留。
- 用户 UAT：8 项清单**已全部通过**（登录与会话保持、无旧机构信息、仪表盘、AED 设备读写与校验、用户/急救员校区、
  求助与会话空态、急救课堂、权限与错误态）；UAT 期间反馈的 2 处显示问题（设备操作列换行与表头对齐、环形图中心文字偏移）
  已修复并由用户复检确认。
- 2026-10-28 外部只读审查发现 1 项 P1、3 项 P2、1 项 P3；修复及当前证据见 §10。
- 当前状态：**迁移主体、历史 UAT 和本轮本机修复已完成；许可证及素材再分发权仍待维护者确认。** 本仓未初始化 Git、未 commit/push/部署；公开发布尚未授权。

## 9. 2026-10-28 发布前只读审查

范围：对照原项目抽查关键实现，核对演示隔离、一键启动、运行要求和文档真实性。以下编号用于后续修复追踪，行号对应本轮审查时的文件。

§9 是 2026-10-28 的原始审查快照，保留审查当时的发现、状态与证据；§10 记录修复和最新验证结果，当前状态以 §10 为准。

### 9.1 待修复问题（审查时状态）

| 编号 | 优先级 | 问题与位置 | 审查时状态 |
| --- | --- | --- | --- |
| REVIEW-20261028-01 | P1 | `DemoModeGuard.ResolveListenAddresses` 的优先级与实际 Kestrel 绑定行为相反 | 待修复 |
| REVIEW-20261028-02 | P2 | 一键脚本复用小程序产物时未检查编入的 API 地址 | 待修复 |
| REVIEW-20261028-03 | P2 | Node 18 的环境要求与锁定依赖不一致 | 待修复 |
| REVIEW-20261028-04 | P2 | README 手动 bootstrap 步骤跨终端丢失引导密钥 | 待修复 |
| REVIEW-20261028-05 | P3 | README 全绿表述与全量测试记录不一致，失败根因未确认 | 记录措辞已纠正；README 与失败原因待处理 |

**REVIEW-20261028-01：演示监听隔离。**

- 位置：`backend/src/AedBackend.Infrastructure/Demo/DemoModeGuard.cs:74–88`；`backend/tests/AedBackend.Tests/Demo/DemoModeGuardTests.cs:173–182`。
- 触发条件：同时声明回环 `urls`（例如 `http://127.0.0.1:5150`）和非回环 `Kestrel:Endpoints:Http:Url`（例如 `http://0.0.0.0:5000`）。守卫遇到 `urls` 就提前返回，但 Kestrel 实际优先绑定显式端点。
- 影响：演示模式可能通过“仅回环”检查，却监听外部地址；公开固定演示 code 可换取真实 Bearer Token，因此会暴露演示身份入口。
- 本轮证据：使用现有 Debug 后端 DLL、临时合成 SQLite 和回环随机端口实跑。日志明确输出 `Overriding address(es) 'http://127.0.0.1:0'. Binding to endpoints defined via IConfiguration and/or UseKestrel() instead.`，随后服务启动。全程未监听非回环地址，检查后停止进程。现有测试 `ResolveListenAddresses_PrefersUrlsOverKestrelEndpoints` 固定了错误优先级。
- 最小修复与验收：使守卫按实际绑定规则校验监听地址，并修正现有测试；确认“回环 urls + 非回环 Kestrel 端点”被拒绝。修复时同时核对 `HTTP_PORTS` / `HTTPS_PORTS` 等受支持的监听配置，避免无 `urls` 时直接假定 localhost；这部分本轮未实跑。

**REVIEW-20261028-02：小程序构建缓存。**

- 位置：`scripts/start-local-demo.ps1:54–61,123–139`；编译期 API 地址见 `miniprogram/utils/runtime-env.ts:31–34`。
- 触发条件：已有 local-demo 包指向其他地址或端口。脚本只在 `vendor.js` 中搜索 `("local-demo")`，即判定可以复用。
- 影响：脚本启动 5150 后端并宣布产物就绪，小程序却继续访问旧地址。本轮通过代码确认触发条件，未额外构建错误地址包；当前现有包确实指向 `127.0.0.1:5150`。
- 最小修复与验收：复用条件同时覆盖模式和 API 地址，无法确认时重建；用不同 API 地址的已有包确认不会被误判为可复用。无需借此扩展为通用构建缓存系统。

**REVIEW-20261028-03：Node 版本要求。**

- 位置：`README.md:221–225`、`scripts/start-local-demo.ps1:69–74`、两端 `package.json` 的 `engines`；依赖证据为 `admin/pnpm-lock.yaml:1532–1534`（Sass 要求 Node >=20.19.0）和 `miniprogram/pnpm-lock.yaml:2458–2460`（eslint-scope 要求 ^20.19.0 / ^22.13.0 / >=24）。
- 影响：按 Node 18 文档准备环境的新用户会进入锁定依赖不支持的运行环境；当前 Node 24 验证与 CI Node 22 配置未覆盖 Node 18。
- 最小修复与验收：统一 README、脚本提示和两端 engines，采用已验证 Node 24 或明确支持的 Node 22.13+；脚本在安装/构建前检查版本。无需为保留 Node 18 承诺而降级依赖。

**REVIEW-20261028-04：手动 bootstrap 指南。**

- 位置：`README.md:279–293`。
- 触发条件：第一个 PowerShell 终端设置 `$env:AED_PUSH_ADMIN_KEY` 并前台启动后端，按文档另开终端执行 bootstrap。新终端不会继承前一终端设置的进程环境变量。
- 影响：请求头中的引导密钥为空，照抄步骤无法完成初始化。
- 最小修复与验收：明确在第二终端设置同一密钥，或调整步骤组织方式；按文档从两个新终端完成一次 bootstrap。该段 curl JSON 转义是否另有问题，本轮未验证，不列为确定缺陷。

**REVIEW-20261028-05：测试结论与失败归因。**

- 位置：`README.md:24,400` 与本文 §5。历史全量结果为 432/433，失败用例为 `RescueSessionSqliteApiTests.StoreJoin_ConcurrentDuplicate_ReturnsExactlyOneNewParticipant`，隔离重跑通过，另一轮全量曾 433/433。
- 影响：将不同轮次结果合成“当前门禁全绿”，以及把失败直接定性为“既有测试特性、隔离必过”，超出了现有证据。
- 本轮仅阅读该用例和 SQLite JoinAsync 实现，确认实现已有写锁和事务；未确认失败根因，未重新运行全量或定向测试。
- 最小处理：README 按轮次陈述实际结果；保留失败输出并定位原因，不修改或削弱并发语义断言。本文中的无依据定性已在记录阶段纠正，实际失败原因仍待处理。

### 9.2 本轮实际验证与证据范围

| 检查 | 结果 |
| --- | --- |
| 发布前脱敏扫描（维护者本地工具） | exit 0；808 个公开源码文件 + 82 个 Admin 构建产物，0 违规 |
| `node demo/tools/verify-demo.mjs` | exit 0；423/423 |
| 当前 `miniprogram/dist/build/mp-weixin/common/vendor.js` | 包含 local-demo 档位与 `127.0.0.1:5150` |
| 现有后端程序的监听配置优先级 | 实跑确认 Kestrel 显式端点覆盖 urls；仅用回环随机端口，结束后停止进程 |
| 原项目关键文件对照 | API 层生命周期协调器、WS 广播器与小程序 `store/auth.ts` 和原项目对应文件一致；此为抽查 |
| 三端全量测试、构建、微信界面与真实外部服务 | 本轮未重跑，沿用历史记录并保留未验证项 |

本轮扫描使用维护者工作区中的私有规则与导出清单。审查阶段未修改源码和 README，未执行 Git 初始化、提交、推送或部署；本次后续写入仅用于记录审查结果与交接。

### 9.3 发布决定、README 与继续工作入口

- **许可证仍待决定（OPEN-003）**：根目录无 `LICENSE`，OpenAPI 为 `license: Proprietary`。发布前确认代码与素材再分发权及许可证，并统一 LICENSE、OpenAPI 和 README；本轮未选择许可证。
- **README 待精简**：删除未经测定的“30 秒”启动承诺，减少“完整闭环、零凭据、硬门禁”等重复宣传表达。保留项目用途、真实运行截图、最短启动命令、三个有实现支撑的技术点、许可证和简短限制；详细架构图、验证矩阵与内部任务编号留在相应文档。截图为后续编辑建议，本轮未新增截图。
- **继续顺序**：先修 REVIEW-20261028-01，再处理 02–04；对齐 05 的 README 表述并根据失败证据定位原因；最后定稿许可证、精简 README。修复时执行直接覆盖各问题的必要检查，不默认重开整套迁移。
- **原始审查停止点（历史记录）**：当时用户要求先保留审查记录，尚未授权修复；“问题均未修复”仅描述该轮停止时的状态，后续修复见 §10。本仓库仍未获公开发布授权。

## 10. 2026-10-28 审查问题修复

本节记录对 §9 五项问题的代码、文档修改和验收结果；§9 保留发现时的原始记录。

| 编号 | 修复 | 当前验收 |
| --- | --- | --- |
| REVIEW-20261028-01 | `DemoModeGuard` 按 Kestrel 有效优先级解析监听：默认显式端点优先；`PreferHostingUrls=true` 时按 urls；其后才检查 HTTP/HTTPS 端口变量，并保留 localhost 默认值。 | 定向回归测试通过；后端全量 438/438 通过。 |
| REVIEW-20261028-02 | 微信小程序构建记录档位与 API 地址元数据；一键脚本同时核对包模式与地址，缺少或无效元数据时重建。 | 错地址构建包触发重建，重建输出记录 `local-demo` / `http://127.0.0.1:5150`，一键 bootstrap/登录自检通过。 |
| REVIEW-20261028-03 | 小程序和 Admin 的 `engines.node` 统一为 `^22.13.0 || >=24.0.0`；一键脚本在安装/构建前拒绝不支持的版本。 | README、两端 engines 和脚本门槛已同步；本机 Node `v24.22.1` 启动自检通过。 |
| REVIEW-20261028-04 | README 两终端 bootstrap 步骤要求在第二终端设置同一引导密钥，修正 JSON 请求体转义。 | README 手动命令已修正；一键流程实跑完成 bootstrap 和普通登录。 |
| REVIEW-20261028-05 | README 移除未经实测的“30 秒”承诺和过时固定测试数量；调查后端并发加入失败。 | 根因为固定测试时间早于运行时间：状态监控会追加合法 `offline` 事件，原断言误把所有会话消息都当成 `joined`。测试改为使用当前 UTC 时间，并断言加入结果、参与者和唯一 `joined` 事件；定向测试通过，后端全量 **438/438** 通过。 |

10 月 28 日构建地址错配检查、Windows `-PrepareOnly`、本地后端启动、bootstrap、普通管理员登录、`/api/admin/auth/me` 和 `-NoWait` 停服均通过。后端 `dotnet test backend/AedBackend.sln` 全量 **438 passed, 0 failed, 0 skipped**；并发加入定向复测通过。 README、脚本和 package JSON 已核对；`node --check miniprogram/scripts/write-build-metadata.mjs` 通过。后端 `dotnet format backend/AedBackend.sln --verify-no-changes` 通过（0 files formatted）。当前选择 GNU GPL v3.0-only，已新增根目录 LICENSE，并同步 README、OpenAPI 与前后端 package metadata。原闭源项目代码及课程/设计/图片/第三方组件的再分发权仍待确认，且尚未授权公开发布；真实微信、地图/GPS、托管 CI、生产部署与 Nginx WebSocket Upgrade 仍未验证。

## 11. 2026-10-29 本地 Git 历史整理

三端源码历史经 `git-filter-repo 2.47.0` 过滤后导入，分别保留小程序 75、后端 37、管理端 31 个提交。原 176 个提交中，33 个仅涉及被移除的内部资料、环境配置或敏感数据，过滤后无公开变更而移除。143 个保留提交的作者日期、提交者日期已逐一与源记录核对；两个合并节点连接三端历史，随后以当前模板作为整合快照。历史快照已移除配置、真实种子与未核查二进制，运行基线是当前模板。

贡献者公开身份统一为 `fukaross <149389833+fukaross@users.noreply.github.com>`（GitHub 隐私邮箱，可关联账号贡献）。Agent 指令、工作目录、任务记录和私有规则仅通过本机 `.git/info/exclude` 排除；公开 `.gitignore` 保留依赖、构建、运行数据和本地环境文件规则。`git check-ignore -v` 已核对内部路径均由本地 exclude 命中。

本轮定向验证：三份过滤历史的所有可达 Blob 与提交信息扫描均为 0 违规，身份元数据不含源个人邮箱；现有脱敏反例自检正确拒绝 3 个样本文件、定位 80 处命中并清理样本。当前交付集扫描通过（808 个源码文件、82 个 Admin 构建文件，0 违规；保留通用内部事件名等 16 处非阻断警告）。应用源码沿用现有模板，本轮未重跑三端全量构建和业务测试。

提交身份调整与脱敏工具链由维护者在本地工作区维护，不随本仓库发布。本地 `.git/info/exclude` 不随 Git 克隆传播，携带 Agent 本地资料时需同步本机排除规则。此次只整理本地 Git，远程发布授权与外部运行验证仍按 §10 的边界处理。

2026-10-30 复核后进一步把维护者本地的脱敏与身份工具链移出公开历史和工作树，公开 `scripts/` 只保留一键配置与一键本地演示两个入口；提交身份统一为上述 GitHub 隐私邮箱。



