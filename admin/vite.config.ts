import { defineConfig } from 'vite'
import type { Plugin } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'
import { ADMIN_BASE_PATH, ADMIN_BUILD_OUT_DIR } from './src/config/build-output'
import { siteConfig } from './src/config/site-config'

const projectRoot = resolve(__dirname)
// 只放开读取目标仓根 config/ 所需的范围，不把整个磁盘加入 fs.allow。
const siteConfigRoot = resolve(__dirname, '..', 'config')

function escapeHtml(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

/**
 * 用共享站点配置替换 index.html 的品牌占位符，避免在 HTML 里维护第二份站点标题。
 *
 * site-config 在配置加载阶段完成校验：站点配置非法时构建会直接失败，
 * 而不是静默沿用旧品牌。占位符为 `%ADMIN_TITLE%`。
 */
function siteBrandingPlugin(): Plugin {
  return {
    name: 'aed-admin-site-branding',
    transformIndexHtml(html) {
      const title = escapeHtml(`${siteConfig.adminTitle} · ${siteConfig.organizationName}`)
      return html.replaceAll('%ADMIN_TITLE%', title)
    },
  }
}

export default defineConfig({
  plugins: [vue(), siteBrandingPlugin()],
  base: ADMIN_BASE_PATH,
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src'),
    },
  },
  server: {
    fs: {
      allow: [projectRoot, siteConfigRoot],
    },
    proxy: {
      '/api': {
        // 与 backend/src/AedBackend.Api/Properties/launchSettings.json 的本地 http 端口
        // 及 appsettings.Development.json 的 PublicBaseUrl 一致。
        target: 'http://127.0.0.1:5150',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: ADMIN_BUILD_OUT_DIR,
    // 输出目录在 admin/ 之外，显式开启清理；Vite 只清空该 outDir 目录本身。
    emptyOutDir: true,
    rollupOptions: {
      output: {
        manualChunks: {
          vue: ['vue', 'vue-router', 'pinia'],
          elementPlus: ['element-plus', '@element-plus/icons-vue'],
          echarts: ['echarts', 'vue-echarts'],
        },
      },
    },
  },
})
