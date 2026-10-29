import { defineConfig } from 'vitest/config'
import { resolve } from 'path'

export default defineConfig({
  test: {
    globals: true,
    environment: 'node',
    include: ['tests/**/*.test.ts'],
    coverage: {
      provider: 'v8',
      include: ['utils/**', 'services/**', 'store/**'],
    },
  },
  server: {
    // 测试与被测源码会读取仓库根 config/site.json，仅放行目标仓库根目录。
    fs: {
      allow: [resolve(__dirname, '..')],
    },
  },
  resolve: {
    alias: {
      '@': resolve(__dirname, '.'),
    },
  },
})
