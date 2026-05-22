# AED Admin Portal

Vue 3 + Vite + TypeScript 管理端 Web SPA，用于校园 AED 急救小程序的后台管理。

## 当前能力

- 管理员登录、超级管理员初始化与会话管理
- 仪表盘概览
- 求助列表、求助详情、审核关闭与误报标记
- 用户投影列表、详情与局部更新
- 超级管理员创建、删除普通管理员
- 管理端审计日志查询

## 本地命令

```powershell
pnpm install
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
