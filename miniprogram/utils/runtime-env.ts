export const APP_ENV = import.meta.env.VITE_APP_ENV || 'development'

const rawApiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'
const rawUseMock = import.meta.env.VITE_USE_MOCK === 'true'
const rawUsePublicDemoData =
  import.meta.env.VITE_USE_PUBLIC_DEMO_DATA === 'true'
const rawUseRealWeChatFlow =
  import.meta.env.VITE_USE_REAL_WECHAT_FLOW === 'true'
const rawRestoreAuthSession = import.meta.env.VITE_RESTORE_AUTH_SESSION
const rawHelpBackendReady = import.meta.env.VITE_HELP_BACKEND_READY === 'true'
const rawAedBackendReady = import.meta.env.VITE_AED_BACKEND_READY === 'true'
const rawRescuerBackendReady =
  import.meta.env.VITE_RESCUER_BACKEND_READY === 'true'

export const API_BASE_URL = rawApiBaseUrl
export const IS_PLACEHOLDER_API_BASE_URL =
  /^https?:\/\/api\.example\.com(?:\/|$)/i.test(rawApiBaseUrl)

export const USE_MOCK = rawUseMock || IS_PLACEHOLDER_API_BASE_URL

export const USE_PUBLIC_DEMO_DATA =
  USE_MOCK || rawUsePublicDemoData

export const HELP_BACKEND_READY = rawHelpBackendReady

export const AED_BACKEND_READY = rawAedBackendReady

export const RESCUER_BACKEND_READY = rawRescuerBackendReady

export const USE_REAL_WECHAT_FLOW =
  !USE_MOCK || rawUseRealWeChatFlow

export const RESTORE_AUTH_SESSION =
  rawRestoreAuthSession === 'true' ||
  (rawRestoreAuthSession !== 'false' && APP_ENV === 'production')

export const USE_REAL_LOCATION = import.meta.env.VITE_USE_REAL_LOCATION === 'true'

export const USE_LOCAL_HELP_READ_MODEL = !HELP_BACKEND_READY

export const AUTO_LOCATE_ON_INIT =
  import.meta.env.VITE_AUTO_LOCATE_ON_INIT === 'true'

export const IS_DEV_LIKE = APP_ENV === 'development' || APP_ENV === 'preview'

export function shouldWarnPlaceholderApiBaseUrl(): boolean {
  return IS_PLACEHOLDER_API_BASE_URL && !rawUseMock
}
