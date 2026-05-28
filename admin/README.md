# AED Admin Portal

Vue 3 + Vite + TypeScript 管理端 Web SPA，用于校园 AED 急救小程序的后台管理。

## 当前能力

- 管理员登录、超级管理员初始化与会话管理
- 仪表盘概览
- 求助列表、求助详情、审核关闭与误报标记
- 仪表盘 5 秒刷新，求助列表 / 详情 10 秒刷新，减少小程序与管理端状态滞后
- AED 设备列表、详情、新增、编辑、删除、图片上传与离线备注管理
- 急救员列表与详情编辑：头像拖拽/本地上传、URL 输入、完整手机号与身份证号维护、技能和考核结果编辑
- 用户投影列表、详情与局部更新
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
