// 运行档位与配置推导 —— 三档位：mock | local-demo | wechat
//
// 档位是唯一模式真源；原先逐模块的 VITE_*_BACKEND_READY / VITE_USE_MOCK /
// VITE_USE_REAL_WECHAT_FLOW 全部由档位推导，不再手工同步，也不再根据
// “示例 API 域名” 猜测模式（配置缺失或非法一律显式报错，不静默降级到 Mock）。
import { RESCUE_PHONE } from '@/config/site-config'
import {
  AppConfigError,
  resolveApiBaseUrl,
  resolveAppMode,
  type AppMode,
} from '@/utils/app-mode'

export { AppConfigError, resolveApiBaseUrl, resolveAppMode }
export type { AppMode }

export const APP_ENV = import.meta.env.VITE_APP_ENV || 'development'

export const APP_MODE: AppMode = resolveConfiguredAppMode()

function resolveConfiguredAppMode(): AppMode {
  const mode = resolveAppMode(import.meta.env.VITE_APP_MODE)
  if (!import.meta.env.VITE_APP_MODE?.trim()) {
    console.info(
      '[runtime-env] 未设置 VITE_APP_MODE，按 mock（仅前端演示，不访问后端）运行',
    )
  }
  return mode
}

export const API_BASE_URL = resolveApiBaseUrl(
  APP_MODE,
  import.meta.env.VITE_API_BASE_URL || '',
)

// ===== 档位推导 =====

/** 纯前端演示档：只跑本地合成 Mock，不访问后端。 */
export const USE_MOCK = APP_MODE === 'mock'

/** 本地演示档：真实后端 + 受限本地身份适配。 */
export const IS_LOCAL_DEMO = APP_MODE === 'local-demo'

/** 真实微信档：wx.login、真实业务 API 与部署配置。 */
export const IS_WECHAT_MODE = APP_MODE === 'wechat'

/** 演示档（mock / local-demo）：需要显示模式徽标、禁用真实外发副作用。 */
export const IS_DEMO_MODE = USE_MOCK || IS_LOCAL_DEMO

const BACKEND_ENABLED = APP_MODE !== 'mock'

export const HELP_BACKEND_READY = BACKEND_ENABLED
export const AED_BACKEND_READY = BACKEND_ENABLED
export const RESCUER_BACKEND_READY = BACKEND_ENABLED
export const CLASSROOM_BACKEND_READY = BACKEND_ENABLED
export const USER_BACKEND_READY = BACKEND_ENABLED
export const SESSION_BACKEND_READY = BACKEND_ENABLED

/** Mock 档下公开只读内容全部来自 demo 合成数据。 */
export const USE_PUBLIC_DEMO_DATA = USE_MOCK

/** 只有真实微信档走 wx.login / 真实订阅消息。 */
export const USE_REAL_WECHAT_FLOW = IS_WECHAT_MODE

/** 只有真实微信档使用真实 GPS；演示档使用合成位置并显式标识。 */
export const USE_REAL_LOCATION = IS_WECHAT_MODE

export const USE_SYNTHETIC_LOCATION = !USE_REAL_LOCATION

/** 演示档不允许真实拨号；rescuePhone 为空时任何档位都不提供热线。 */
export const RESCUE_PHONE_ENABLED = IS_WECHAT_MODE && RESCUE_PHONE !== ''

export const USE_LOCAL_HELP_READ_MODEL = !HELP_BACKEND_READY

export const RESTORE_AUTH_SESSION =
  import.meta.env.VITE_RESTORE_AUTH_SESSION === 'true' ||
  (import.meta.env.VITE_RESTORE_AUTH_SESSION !== 'false' &&
    APP_ENV === 'production')

export const IS_DEV_LIKE = APP_ENV === 'development' || APP_ENV === 'preview'

const MODE_LABELS: Readonly<Record<AppMode, string>> = {
  mock: '演示模式 · 本地合成数据',
  'local-demo': '本地演示 · 真实后端',
  wechat: '真实微信模式',
}

export const MODE_LABEL = MODE_LABELS[APP_MODE]
