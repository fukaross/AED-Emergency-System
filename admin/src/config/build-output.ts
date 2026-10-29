/**
 * Admin SPA 构建输出约定（CONTRACT-004）。
 *
 * - `base` 保持 `/admin/`：由后端以 `wwwroot/admin` 静态托管。
 * - 输出目录相对 `admin/` 解析，只指向目标仓 `backend/src/AedBackend.Api/wwwroot/admin`；
 *   不使用旧工程相邻的 `AedBackend` 目录，也不输出到 `admin/dist`。
 * - `vite.config.ts` 以 `emptyOutDir: true` 清理该输出目录本身，清理范围不扩展到其父目录。
 */
export const ADMIN_BASE_PATH = '/admin/'

export const ADMIN_BUILD_OUT_DIR = '../backend/src/AedBackend.Api/wwwroot/admin'
