# demo/ — 独立合成演示数据（`demo-v1`）

本目录是 AED 开源迁移模板的**唯一演示数据源**。数据全部从空白独立合成，不来自任何真实
学校档案、真实 AED 台账、真实个人资料或生产数据库，也不保留与原系统记录的编号、数量或
一一对应关系。字段形状以目标仓后端模型/DTO 与小程序类型为准，本目录不自创业务字段语义。

| 项 | 值 |
| --- | --- |
| 数据版本 | `demo-v1` |
| 生成日期 | 2026-10-26 |
| 坐标系 | GCJ-02（高德/腾讯系坐标，与 `config/site.json` 的 `coordinateSystem` 一致） |
| 站点配置 | 同仓 `config/site.json`（校区 code 的唯一真源） |
| 校验入口 | `node demo/tools/verify-demo.mjs`（在仓库根目录执行） |
| 消费方 | 后端演示初始化（TASK-003）与小程序 Mock（TASK-004）；Admin 读取后端数据 |

> 卫生边界：本目录不得写入真实姓名、手机号、身份证、证书编号、真实学校/机构名、现网域名、
> 地图 Key 或任何凭据。演示环境禁止真实拨号、真实订阅消息、真实 OSS 上传与任意外发。

---

## 1. 目录与文件

```
demo/
├── data/                     # 11 个与后端 JSON provider/迁移器兼容的数据文件
│   ├── wechat-experience-users.json   # {"users":[...]}   演示用户（7）
│   ├── admin-users.json               # {"users":[]}      初始为空（管理员走 bootstrap）
│   ├── admin-user-profiles.json       # {"profiles":{...}} 用户身份/账号状态
│   ├── admin-audit-logs.json          # {"logs":[]}       初始为空
│   ├── aed-devices.json               # [...]             演示 AED 设备（10）
│   ├── rescuer-profiles.json          # [...]             急救员档案（7）
│   ├── knowledge-summary.json         # {...}             课堂统计横幅
│   ├── courses.json                   # [...]             课程（4）
│   ├── quick-actions.json             # [...]             关键步骤卡片（2）
│   ├── help-requests.json             # {"requests":[]}   初始为空
│   └── rescue-sessions.json           # {"sessions":[]}   初始为空
├── roles.json                # 固定演示角色注册表（3 个角色 + 负向样本），非 CLI 输入
├── tools/verify-demo.mjs     # 数据校验与 TEST-002 地理证据脚本
└── README.md                 # 本文件
```

后端迁移器的 JSON 命名与包裹结构来自
`backend/src/AedBackend.Infrastructure/Persistence/Sqlite/JsonRuntimeDataReader.cs`
与 `JsonRescueSessionPersistence.cs`：列表型文件为裸数组，`users/logs/requests/sessions`
为对象包裹，`profiles` 为以 openId 为键的字典。`help-requests.json` 与 `rescue-sessions.json`
的包裹键缺失会被迁移器判为非法文档，因此保留空数组而不是空文件。

`roles.json` 是演示角色注册表，不属于上面 11 个 CLI 输入；后端 `init-demo-data`（TASK-003）
应按需读取它来建立受限演示登录映射与学校绑定，**不要把它复制进传给迁移 CLI 的 data-root**。

---

## 2. 生成规则（独立性声明）

1. **用户/档案从空白生成**：OpenId 使用 `demo-openid-*`；姓名统一为「演示X（角色）」标签；
   生日、手机号、身份证、证书、培训日期、成绩、来源备注全部新造。
2. **手机号**统一为 `1300000000x` 合成段；**身份证**统一为保留段 `990000` + 合法校验位的
   18 位合成号码（后端 `IdNumberValidator` 只校验格式与校验位，这些值不代表任何真人）。
   合成号码仅用于本地演示，禁止外发或拨打。
3. **证书**统一为 `DEMO-CERT-260x` 前缀，`certificateNoMasked` 按现有脱敏规则
   （前 4 位 + `****` + 后 4 位）预先计算，与 `JsonResponderStore.ComputeMaskedFields` 一致。
4. **来源字段**不保留历史信息：`sourceFile` 指向本目录文件、`sourceSheet` 为 `demo-v1`、
   `sourceSeqNo` 等于档案 id、`remark` 标注「合成演示数据」，`sourceApplicationId` 为空。
5. **数量不沿用历史数据**：用户 7、档案 7、设备 10、课程 4、关键步骤 2，仅覆盖业务分支所需。
6. **GCJ-02 点位独立选择**：以 `config/site.json` 的两个校区中心为基准，按方位角与目标距离
   正向计算新点位（不是对任何旧坐标做平移），并用与后端相同的 Haversine 公式复核
   「整数米」距离。边界样本以目标距离直接生成，实测四舍五入正好落在 `30000`。
7. **课程与关键步骤不含急救教学正文**：仅保留列表/详情/步骤结构与中性「演示占位」文案，
   正式内容须由具备资质的培训人员审核后填入；免责声明保留。

---

## 3. 角色 ↔ 用户 ↔ 档案 ↔ 校区

`roles.json` 固定三个角色（SQLite 初始化与受限 local-demo 登录均使用这些 code）：

| 角色 | 演示登录 code | openId | 用户 | 档案 | 校区 | 资格 | 后端判定 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `requester` | `demo-requester` | `demo-openid-requester` | 演示甲（求助人） | 无 | east（东区） | 非急救员 | 可发布求助，不可加入会话 |
| `active-rescuer` | `demo-rescuer` | `demo-openid-rescuer-east` | 演示乙（急救员·东区） | `DEMO-RSP-001` | east（东区） | `active` + 已绑定 | 认证急救员：可加入会话/提交报告 |
| `unqualified` | `demo-unqualified` | `demo-openid-unqualified` | 演示丙（待认证） | `DEMO-RSP-002` | east（东区） | `inactive` | 403 / code 1003，不可参与 |

认证判定沿用现有真实规则（不得绕过）：`responder_profiles.boundOpenId` 绑定当前登录用户，
且 `qualificationStatus == "active"`（见后端 `EndpointRescuers.RequireCertifiedResponderAsync`）。
`admin-user-profiles.json` 的 `responderStatus/certified/level` 只是管理端展示字段，不替代上述规则。

负向样本（`roles.json.negativeSamples`）：`demo-openid-disabled` 账号被禁用但绑定了资格
`active` 的 `DEMO-RSP-003`，用于验证「账号禁用（HTTP 403 / code 1018）优先于急救员资格判断」。

### 用户一览（`demo/data/wechat-experience-users.json`）

| openId | 姓名 | 手机号 | primaryRole | 账号状态 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `demo-openid-requester` | 演示甲（求助人） | 13000000001 | student | active | 求助人角色 |
| `demo-openid-rescuer-east` | 演示乙（急救员·东区） | 13000000002 | teacher | active | 认证急救员角色（在线） |
| `demo-openid-unqualified` | 演示丙（待认证） | 13000000003 | student | active | 资格失效负向样本 |
| `demo-openid-disabled` | 演示丁（禁用账号） | 13000000004 | staff | **disabled** | 账号禁用负向样本 |
| `demo-openid-rescuer-west` | 演示戊（急救员·西区） | 13000000005 | student | active | 西区在线样本 |
| `demo-openid-rescuer-far` | 演示己（急救员·远区） | 13000000006 | student | active | 超出服务半径自动离线 |
| `demo-openid-rescuer-edge` | 演示庚（急救员·边界） | 13000000007 | student | active | 恰好 30 km 边界，不判离线 |

用户的 `sessionTokenHash` 全部为 `""`、`subscriptions` 全部为空，导入时不携带任何真实
token / session / 订阅模板。

### 急救员档案一览（`demo/data/rescuer-profiles.json`）

距离为档案坐标到其 `campus` 校区中心的 Haversine 整数米（与后端同公式）；在线判定规则：
资格非 `active` → 离线；否则到校区中心 `> 30000 m` → 自动离线，`== 30000 m` 仍在线。

| 档案 id | recordId | 绑定用户 | 校区 | 资格 | 等级 | 距校区中心 | 在线判定 | 覆盖分支 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `DEMO-RSP-001` | `DEMO-ARC-001` | demo-openid-rescuer-east | east | active | official | 150 m | online | 认证急救员主样本 |
| `DEMO-RSP-002` | `DEMO-ARC-002` | demo-openid-unqualified | east | **inactive** | trainee | 300 m | offline | 资格失效、证书过期 |
| `DEMO-RSP-003` | `DEMO-ARC-003` | demo-openid-disabled | east | active | core | 420 m | online | 账号禁用优先判定 |
| `DEMO-RSP-004` | `DEMO-ARC-004` | demo-openid-rescuer-far | east | active | trainee | 36000 m | offline | 超出 30 km 自动离线 |
| `DEMO-RSP-005` | `DEMO-ARC-005` | demo-openid-rescuer-edge | east | active | official | **30000 m** | online | 半径边界不判超界 |
| `DEMO-RSP-006` | `DEMO-ARC-006` | demo-openid-rescuer-west | west | active | official | 180 m | online | 第二校区 |
| `DEMO-RSP-007` | `DEMO-ARC-007` | 无（未绑定） | east | active | honor | 700 m | online | 档案未绑定分支 |

技能差异：`DEMO-RSP-002` 仅 CPR、`DEMO-RSP-004` 无 STB，其余三项齐全；等级覆盖
`honor/trainee/official/core`；证书签发日期覆盖「未过期」（2025–2026 签发，按现有规则
到期 = 签发 + 3 年）与「已过期」（2022 签发，用于解释资格失效）。

---

## 4. AED 设备与地理性质（TEST-002 证据）

`aed-devices.json` 的字段就是后端模型 `AedDeviceRecord`（无 campus 字段），因此**校区归属
由「最近校区中心」推导**，下表已用与校验脚本相同的实现算好，可供种子/筛选对照：

| 设备 id | 名称 | 最近校区 | 距最近校区中心 | 半径分类 | 状态 |
| --- | --- | --- | --- | --- | --- |
| `AED-DEMO-01` | 东区-图书信息中心 | east | 120 m | 内 | online |
| `AED-DEMO-02` | 东区-教学一号楼 | east | 260 m | 内 | online |
| `AED-DEMO-03` | 东区-体育馆 | east | 520 m | 内 | online |
| `AED-DEMO-04` | 东区-学生公寓三号楼 | east | 900 m | 内 | online |
| `AED-DEMO-05` | 东区-综合服务楼 | east | 1500 m | 内 | **offline**（带 statusNote） |
| `AED-DEMO-06` | 西区-教学二号楼 | west | 200 m | 内 | online |
| `AED-DEMO-07` | 西区-食堂 | west | 600 m | 内 | online |
| `AED-DEMO-08` | 西区-田径场 | west | 1100 m | 内 | **offline**（带 statusNote） |
| `AED-DEMO-09` | 东区-校外备勤点 | east | **30000 m** | 边界 | online |
| `AED-DEMO-10` | 东区-远端仓储点 | east | 36000 m | 外 | online |

覆盖的既有语义（数值规则不得为了数据而修改）：

- **最近排序**：`GET /api/aed/nearby` 现有实现为「online 优先，再按距离升序」，半径过滤是
  `distance <= radius`（包含边界）。以东区中心查询 3000 m 得
  `01 → 02 → 03 → 04 → 05`；以西区中心查询 3000 m 得 `06 → 07 → 08`。
- **半径边界**：`radius=30000` 时命中 9 台（含 `AED-DEMO-09`，排除 `AED-DEMO-10`）；
  `radius=29999` 时恰好排除 `AED-DEMO-09`，可用于证明边界样本没有浮点漂移。
- **校区归属 / 最近校区**：viewer 在东区中心解析为 `east`，在西区中心解析为 `west`；
  两校区中心相距约 5866 m，大于样本与中心的最大近距离（1500 m），不会出现归属歧义。
- **服务半径**：来自 `config/site.json` 的 `serviceRadiusKm = 30`，即 30000 m；急救员
  「> 30000 m 才判定离线，等于边界不因距离离线」由 `DEMO-RSP-004/005` 覆盖。
- 点位是合成坐标，仅保证数学性质，**不代表任何真实设施位置**；同时已被脚本验证不是对
  任何旧坐标的常量平移。

`verify-demo.mjs` 的期望表（`EXPECTED_DEVICE_*` / `EXPECTED_PROFILE_*` / `EXPECTED_AVAILABILITY`）
锁定上述设计值；修改坐标或距离时同步更新期望并说明理由，禁止只改期望掩盖数据问题。

---

## 5. 课堂数据

- `knowledge-summary.json`：统计横幅 4 项，`aed_count = 10` 与设备数量一致（脚本校验）；
  其余数值为合成演示值。`updatedAt` 为演示时间。
- `courses.json`：4 门课程（3 个 `emergency` + 1 个 `first-aid`，`type` 均为 `article`），
  含封面、富文本占位正文、免责声明、步骤与要点、`pinned/sortOrder/viewCount/cacheVersion`
  等字段，用于验证列表、分类筛选、详情与阅读量计数。**不提供视频课程**：原实现的 video
  分支需要真实/自建视频素材，本轮没有可授权的视频，故不放进演示数据（见 §8）。
- `quick-actions.json`：2 张关键步骤卡片 × 3 步，结构与课程步骤一致。

课程封面与步骤图片路径为 `/static/images/course-cover-*.png`，头像为
`/static/images/avatar-default.png`，AED 图片为 `/static/images/aed-placeholder.png`，
关键步骤图标复用仓库已有 `/static/icons/*.svg`。这些占位图由 TASK-004 生成/同步，本目录
只引用路径，不携带二进制素材。

---

## 6. 后端与小程序如何消费本目录

**后端（TASK-003）**

1. 按 CONTRACT-003 提供 `init-demo-data --data-root <绝对目录>`：把本目录 `data/` 的 11 个
   文件按现有 CLR 形状准备到 CLI 可读的 data-root（同名字段、camelCase），再调用现有迁移器：
   `dotnet run --project backend/src/AedBackend.Api -- migrate-json-to-sqlite --data-root <JSON绝对目录> --db <SQLite绝对路径>`。
2. 迁移器只覆盖上述 11 类集合，**不包含学校档案/身份绑定表**；TASK-003 必须用
   `roles.json.demoRoles[].schoolBinding` 补齐 `SchoolPersonProfile` 与审批通过的
   `SchoolBindingApplication`，并让 `demoRoles[].openId` 与 `rescuer-profiles.json` 的
   `boundOpenId` 对齐，否则演示急救员不满足真实授权。
3. 演示登录（`POST /api/auth/wechat-login` 适配点）只能把 `roles.json` 的 `loginCode`
   映射到对应 `openId`，不得接受任意 OpenId；真实模式必须拒绝这些 code。
4. 站点相关内容（机构名、标题、校区 code/名称/中心、检索城市、服务半径）只能来自
   `config/site.json`；种子、筛选、校验、展示不得再写死校区清单或机构名。
5. `admin-users.json` 为空：管理员通过 `POST /api/admin/auth/bootstrap` 创建，不预置账号或
   弱口令；`admin-audit-logs.json`、`help-requests.json`、`rescue-sessions.json` 初始为空，
   求助/会话由运行验收时真实创建。

**小程序 Mock（TASK-004）**

- Mock 数据必须从本目录生成/改写而来（可加生成脚本，但产物需注明 `demo-v1` 来源与重建入口），
  不得手工维护第二份演示档案，也不得沿用旧学校、旧校区 code、旧手机号/档案。
- 校区 code 与中心必须读 `config/site.json`，删除封闭 `CampusCode` 联合类型；
  本仓已把 `miniprogram/contracts/openapi.yaml` 的校区字段改为动态字符串。
- 头像/封面等静态占位图约定在 `/static/images/*`，由 TASK-004 生成自建占位图。

> 当前后端/前端源码仍是旧硬编码校区实现（用于后续任务改造，不在 TASK-002 写入范围）：
> 后端 `RescuerEndpoints` 的 `Campuses` 常量、`AdminRescuerEndpoints` 与
> `AdminVolunteerApplicationEndpoints` 的 `ValidCampuses`、`UserArchiveMapping.ResolveCollege`
> 的校区显示名分支、以及旧 Seed 默认值，都需要在 TASK-003 改为读取 `config/site.json`；
> 小程序 `types/rescuer.ts` 的 `CampusCode` 与 `utils/constants.ts` 的 `CAMPUS_CENTERS`
> 需要在 TASK-004 改为配置驱动。否则新校区 code 会被旧校验拒绝或抛出异常。

---

## 7. 素材授权状态

| 素材 | 状态 | 说明 |
| --- | --- | --- |
| 用户/急救员头像 | 未提供文件 | 引用 `/static/images/avatar-default.png`，由 TASK-004 生成中性占位图 |
| AED 设备图片 | 未提供文件 | 引用 `/static/images/aed-placeholder.png`，同上 |
| 课程封面/步骤图 | 未提供文件 | 引用 `/static/images/course-cover-*.png`，由 TASK-004 生成纯色/图形占位图 |
| 关键步骤图标 | 复用仓库已有 SVG | `/static/icons/guide-aed.svg`，随源码导出，无第三方照片 |
| 人物照片 / 视频 / 证件照 | **不在交付集** | 未取得授权，不进入公开分发集，也不使用外链图床 |

本目录不含任何二进制媒体、二维码、真实证照或带 EXIF 的照片；文本内容均为中性演示占位。

---

## 8. 已知边界与未覆盖项

- **视频课程分支未覆盖**：没有可授权的视频素材，4 门课程均为 `article`；video 类型课程、
  视频播放与 `videos[].url` 非空校验未在演示数据中启用，需要 TASK-004 明确取舍
  （自建占位视频或保持 article）。
- **地图底图/GPS/真实定位未验证**：坐标是合成的 GCJ-02 点，页面需标识「合成位置」；
  地图 Key 缺失时的不可用状态属于 TASK-004/TASK-007 的验证范围。
- **真实通知/拨号/OSS 未验证**：演示数据不含订阅模板、手机号仅合成，演示模式不得触发
  真实微信推送、真实拨号或云存储写入。
- **运行级三端验收未做**：TASK-002 只做到数据层与契约层校验（JSON 解析、引用完整、
  距离性质、OpenAPI 解析）；第三校区在运行期生效属于 TASK-007 的 TEST-003。
- **不保证数据兼容旧库**：本目录是新模板的演示源，不是生产数据迁移工具；真实部署的
  校区变更/数据迁移需要另行设计。

---

## 9. 重建与校验

```powershell
# 仓库根目录：校验全部 demo 数据、引用关系、校区一致性与距离性质（TEST-002 证据）
node demo/tools/verify-demo.mjs
```

脚本退出码 0 表示全部检查通过；失败会打印 `FAIL ...` 明细并以非 0 退出。脚本会读取
`config/site.json`、`demo/roles.json` 与 `demo/data/*.json`，不访问网络、不写文件。
