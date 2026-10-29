#!/usr/bin/env node
// 微信构建后同步静态资源，并校验代码引用的静态文件真实存在。
//
// 为什么需要校验：static/images 下的占位素材是脚本生成的（pnpm assets:generate），
// 一旦代码引用了不存在的路径，小程序运行时会静默显示空白。这里在构建阶段直接失败。
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync } from 'node:fs'
import { dirname, extname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const rootDir = resolve(scriptDir, '..')
const sourceStaticDir = resolve(rootDir, 'static')
const targetRoots = [
  resolve(rootDir, 'dist/build/mp-weixin'),
  resolve(rootDir, 'dist/dev/mp-weixin'),
]

// 与 uni-app 微信构建输出一致；静态路径在代码里写作 /static/...
const REFERENCE_PATTERN = /\/static\/(?:images|icons)\/[A-Za-z0-9_@.-]+/g
const SCANNED_EXTENSIONS = new Set(['.ts', '.js', '.vue', '.json', '.scss'])
const SKIPPED_DIRECTORIES = new Set(['node_modules', 'dist', '.git'])

if (!existsSync(sourceStaticDir)) {
  console.error(`[sync-static] 源静态目录不存在：${sourceStaticDir}`)
  process.exit(1)
}

function listFiles(directory) {
  const entries = []
  for (const name of readdirSync(directory)) {
    const absolutePath = join(directory, name)
    const stat = statSync(absolutePath)
    if (stat.isDirectory()) {
      if (SKIPPED_DIRECTORIES.has(name)) continue
      entries.push(...listFiles(absolutePath))
    } else if (SCANNED_EXTENSIONS.has(extname(name))) {
      entries.push(absolutePath)
    }
  }
  return entries
}

function collectReferencedAssets() {
  const references = new Set()
  for (const filePath of listFiles(rootDir)) {
    const content = readFileSync(filePath, 'utf8')
    for (const match of content.matchAll(REFERENCE_PATTERN)) {
      references.add(match[0])
    }
  }
  return [...references].sort()
}

function toStaticRelativeUrl(publicPath) {
  return publicPath.replace(/^\/static\//, '')
}

function assertAssetsExist(references, staticDir, label) {
  const missing = references.filter(
    (publicPath) => !existsSync(resolve(staticDir, toStaticRelativeUrl(publicPath))),
  )
  if (missing.length > 0) {
    console.error(`[sync-static] ${label} 缺少被引用的静态文件：`)
    for (const publicPath of missing) {
      console.error(`  - ${publicPath}`)
    }
    console.error('[sync-static] 请运行 pnpm assets:generate 或补齐素材后再构建')
    process.exit(1)
  }
}

const references = collectReferencedAssets()
if (references.length === 0) {
  console.error('[sync-static] 未在源码中扫描到任何 /static/images|icons 引用，请检查扫描范围')
  process.exit(1)
}
assertAssetsExist(references, sourceStaticDir, 'static/ 源目录')
console.log(`[sync-static] 引用校验通过：${references.length} 个静态文件`)

function syncToTarget(targetRoot) {
  if (!existsSync(targetRoot)) {
    return false
  }

  const targetStaticDir = resolve(targetRoot, 'static')
  rmSync(targetStaticDir, { recursive: true, force: true })
  mkdirSync(targetStaticDir, { recursive: true })
  cpSync(sourceStaticDir, targetStaticDir, { recursive: true, force: true })
  assertAssetsExist(references, targetStaticDir, targetStaticDir)
  console.log(`[sync-static] copied static -> ${targetStaticDir}`)
  return true
}

const copiedCount = targetRoots.reduce((count, targetRoot) => {
  return count + (syncToTarget(targetRoot) ? 1 : 0)
}, 0)

if (copiedCount === 0) {
  console.error(
    '[sync-static] 未找到 mp-weixin 构建产物（dist/build/mp-weixin 或 dist/dev/mp-weixin），请先运行构建',
  )
  process.exit(1)
}
