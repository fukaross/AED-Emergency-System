import { mkdir, writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { loadEnv } from 'vite'

const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const projectDirectory = resolve(scriptDirectory, '..')
const viteMode = process.argv[2]
if (!['production', 'preview'].includes(viteMode)) {
  throw new Error('Pass the Vite mode used for the mini-program build (production or preview)')
}

const buildEnvironment = loadEnv(viteMode, projectDirectory, 'VITE_')
const appModes = new Set(['mock', 'local-demo', 'wechat'])
const mode = buildEnvironment.VITE_APP_MODE?.trim() || 'mock'
const configuredApiBase = buildEnvironment.VITE_API_BASE_URL?.trim() || ''
const apiBaseUrl = (configuredApiBase || 'http://127.0.0.1:5150').replace(/\/+$/, '')
const placeholderHost = /^https?:\/\/(?:[^/]*\.)?api\.example\.com(?:[/:?#]|$)/i

if (!appModes.has(mode)) {
  throw new Error(`Cannot record build metadata for unsupported VITE_APP_MODE=${mode}`)
}

if (mode !== 'mock' && !configuredApiBase) {
  throw new Error(`VITE_APP_MODE=${mode} requires VITE_API_BASE_URL`)
}

if (!/^https?:\/\/[^\s/?#]+(?:[/?#][^\s]*)?$/i.test(apiBaseUrl) || placeholderHost.test(apiBaseUrl)) {
  throw new Error('VITE_API_BASE_URL must be a configured http(s) URL, not a placeholder')
}

const metadataPath = resolve(scriptDirectory, '../dist/build/mp-weixin-build-config.json')
const metadata = { schemaVersion: 1, mode, apiBaseUrl }

await mkdir(dirname(metadataPath), { recursive: true })
await writeFile(metadataPath, `${JSON.stringify(metadata, null, 2)}\n`, 'utf8')
