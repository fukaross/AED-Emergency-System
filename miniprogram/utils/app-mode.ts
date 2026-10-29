// 档位解析的纯函数（不依赖 import.meta.env）—— 供运行时与 vite 构建前校验共用。
//
// 规则：三档位 mock | local-demo | wechat；未设置按 mock（仅前端演示）；非法值、
// 非 mock 缺 API 地址、示例占位地址一律抛 AppConfigError，绝不静默回落到 Mock。

export type AppMode = 'mock' | 'local-demo' | 'wechat'

export class AppConfigError extends Error {
  constructor(message: string) {
    super(`[runtime-env] ${message}`)
    this.name = 'AppConfigError'
  }
}

export const APP_MODES: readonly AppMode[] = ['mock', 'local-demo', 'wechat']

// 小程序运行时没有标准 URL 全局对象，这里用正则做最小必要校验。
const HTTP_URL_PATTERN = /^https?:\/\/[^\s/?#]+(?:[/?#][^\s]*)?$/i
const PLACEHOLDER_API_HOST_PATTERN = /^https?:\/\/(?:[^/]*\.)?api\.example\.com(?:[/:?#]|$)/i
// mock 档不访问后端；占位基址与本地后端约定端口一致（127.0.0.1:5150），仅用于日志/诊断。
const MOCK_FALLBACK_API_BASE_URL = 'http://127.0.0.1:5150'

/**
 * 解析档位。
 * - 未设置：按 mock 运行（不需要后端，构建/演示开箱可用），由调用方给出可见提示；
 * - 非法值：抛 AppConfigError，不猜测、不回落。
 */
export function resolveAppMode(raw: string | undefined): AppMode {
  const value = (raw ?? '').trim()
  if (!value) return 'mock'
  if (!APP_MODES.includes(value as AppMode)) {
    throw new AppConfigError(
      `非法 VITE_APP_MODE="${value}"，可选值：${APP_MODES.join(' | ')}`,
    )
  }
  return value as AppMode
}

/**
 * 解析 API 基址。
 * mock 不访问后端，允许不配置；其它档位缺少合法地址时抛配置错误，绝不静默转 Mock。
 */
export function resolveApiBaseUrl(mode: AppMode, raw: string): string {
  const value = (raw ?? '').trim()

  if (!value) {
    if (mode === 'mock') return MOCK_FALLBACK_API_BASE_URL
    throw new AppConfigError(
      `VITE_APP_MODE=${mode} 需要配置 VITE_API_BASE_URL（当前为空）`,
    )
  }

  if (!HTTP_URL_PATTERN.test(value)) {
    throw new AppConfigError(
      `VITE_API_BASE_URL 必须是 http(s) 绝对地址，当前为 "${value}"`,
    )
  }

  if (PLACEHOLDER_API_HOST_PATTERN.test(value)) {
    throw new AppConfigError(
      'VITE_API_BASE_URL 仍是示例占位地址 api.example.com，请填写真实后端地址',
    )
  }

  return value.replace(/\/+$/, '')
}
