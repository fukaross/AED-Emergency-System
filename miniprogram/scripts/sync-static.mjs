import { cpSync, existsSync, mkdirSync, rmSync } from 'node:fs'
import { resolve } from 'node:path'

const rootDir = process.cwd()
const sourceStaticDir = resolve(rootDir, 'static')
const targetRoots = [
  resolve(rootDir, 'dist/build/mp-weixin'),
  resolve(rootDir, 'dist/dev/mp-weixin'),
]

if (!existsSync(sourceStaticDir)) {
  throw new Error(`[sync-static] source directory not found: ${sourceStaticDir}`)
}

function syncToTarget(targetRoot) {
  if (!existsSync(targetRoot)) {
    return false
  }

  const targetStaticDir = resolve(targetRoot, 'static')
  rmSync(targetStaticDir, { recursive: true, force: true })
  mkdirSync(targetStaticDir, { recursive: true })
  cpSync(sourceStaticDir, targetStaticDir, { recursive: true, force: true })
  console.log(`[sync-static] copied static -> ${targetStaticDir}`)
  return true
}

const copiedCount = targetRoots.reduce((count, targetRoot) => {
  return count + (syncToTarget(targetRoot) ? 1 : 0)
}, 0)

if (copiedCount === 0) {
  console.warn('[sync-static] no mp-weixin output directory found under dist/')
}
