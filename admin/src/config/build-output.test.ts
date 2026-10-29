import { readFileSync } from 'node:fs'
import { basename, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { ADMIN_BASE_PATH, ADMIN_BUILD_OUT_DIR } from './build-output'

// 测试文件位于 admin/src/config/，向上一级即 admin/ 根目录。
const adminRoot = fileURLToPath(new URL('../../', import.meta.url))
const backendWwwRoot = resolve(adminRoot, '..', 'backend', 'src', 'AedBackend.Api', 'wwwroot')
const resolvedOutDir = resolve(adminRoot, ADMIN_BUILD_OUT_DIR)
const viteConfigSource = readFileSync(new URL('../../vite.config.ts', import.meta.url), 'utf8')

describe('Admin 构建输出约束（CONTRACT-004 / TEST-007）', () => {
  it('保留 /admin/ base', () => {
    expect(ADMIN_BASE_PATH).toBe('/admin/')
    expect(viteConfigSource).toContain('base: ADMIN_BASE_PATH')
  })

  it('输出只到目标 backend 的 wwwroot/admin', () => {
    expect(relative(backendWwwRoot, resolvedOutDir)).toBe('admin')
    expect(basename(resolvedOutDir)).toBe('admin')
  })

  it('不再指向旧工程相邻的 AedBackend 目录', () => {
    expect(ADMIN_BUILD_OUT_DIR.replace(/\\/g, '/')).not.toMatch(/^\.\.\/AedBackend\//)
    expect(viteConfigSource).not.toContain("outDir: '../AedBackend/")
  })

  it('vite.config 使用统一输出常量并显式清理该输出目录', () => {
    expect(viteConfigSource).toContain('outDir: ADMIN_BUILD_OUT_DIR')
    expect(viteConfigSource).toContain('emptyOutDir: true')
  })
})
