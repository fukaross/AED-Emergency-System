import { defineConfig, loadEnv } from 'vite'
import { resolve } from 'node:path'
import uni from '@dcloudio/vite-plugin-uni'
import { resolveApiBaseUrl, resolveAppMode } from './utils/app-mode'

// https://vitejs.dev/config/
// 1) 构建前校验 VITE_APP_MODE / VITE_API_BASE_URL：非法档位或非 mock 缺地址直接失败，
//    不允许静默产出 Mock 包（错误只含键名与规则，不打印任何密钥）。
// 2) miniprogram/config/site-config.ts 需要读取仓库根 config/site.json，
//    因此开发服务器只额外放行目标仓库根目录（不扩大到整盘）。
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, __dirname, '')
  const appMode = resolveAppMode(env.VITE_APP_MODE)
  resolveApiBaseUrl(appMode, env.VITE_API_BASE_URL || '')

  return {
    plugins: [uni()],
    server: {
      fs: {
        allow: [resolve(__dirname, '..')],
      },
    },
  }
})
