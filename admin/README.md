# AED Admin Portal

Vue 3 + Vite + TypeScript 管理端 Web SPA，用于校园 AED 急救小程序的后台管理。

## 仓库信息

- 远程：`https://github.com/fukaross/AED-Admin`（private，master 分支）
- 相邻仓库：
  - `Design/` — 前端工程 + 设计/接口规格（`fukaross/REDACTED`）
  - `AedBackend/` — 后端工程（`fukaross/REDACTED`，private）

## 当前能力

- 管理员登录、超级管理员初始化与会话管理
- 仪表盘概览
- 求助列表、求助详情、审核关闭与误报标记
- 急救会话只读监控：`/rescue-sessions` 提供进行中/历史列表与详情，展示 SOS、参与者最新位置、信号、拿取 AED/到达状态及消息时间线
- 仪表盘 5 秒刷新，求助列表 / 详情 10 秒刷新，减少小程序与管理端状态滞后
- 活动急救会话每 10 秒刷新；页面隐藏时暂停，恢复可见后立即补拉，仅最新响应可落地，关闭后停止轮询
- AED 设备列表、详情、新增、编辑、删除、图片上传与离线备注管理
- 急救员列表与详情编辑：头像拖拽/本地上传、URL 输入、完整手机号与身份证号维护、技能和考核结果编辑
- 急救课堂统计横幅、关键步骤、课程管理；课堂图片支持 URL / 本地上传 / 预览，关键步骤与课程步骤支持排序编辑
- 用户列表、详情与局部更新；用户管理只维护身份、账号状态和备注，急救资格从急救员档案只读同步
- 志愿者申请审核；审核通过后由后端事务化创建急救员草稿
- 学校档案维护、学校绑定审核、急救员认领审核
- 学校档案删除会解绑用户；学校绑定审核记录删除仅超级管理员可用
- 超级管理员创建、查看、删除普通管理员，并可重置普通管理员密码
- 管理端审计日志查询

## 本地命令

```powershell
pnpm install
pnpm test
pnpm lint
pnpm typecheck
pnpm build
```

## 部署边界

- API base 为 `/api/admin`。
- Vite base 为 `/admin/`。
- 构建产物部署到 `../AedBackend/src/AedBackend.Api/wwwroot/admin/`，由后端 `/admin/` 静态托管。
- 生产访问地址为 `REDACTED/admin/`。
- 不提交 `.env*`、`dist/`、`node_modules/`、运行时数据和构建产物。
- AED 设备地图选点与急救会话多 marker 监控均需配置高德地图 JS API 2.0：`VITE_AMAP_KEY` / `VITE_AMAP_SECURITY_CODE`（参见 `.env.example`）。
- 急救会话一期只通过 Admin session 执行 HTTP 只读查询，不提供发言、代改状态或位置写入。
