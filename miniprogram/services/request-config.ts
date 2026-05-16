import {
  API_BASE_URL,
  IS_PLACEHOLDER_API_BASE_URL,
  shouldWarnPlaceholderApiBaseUrl,
} from '@/utils/runtime-env'

export const BASE_URL = API_BASE_URL
export const IS_DEV = import.meta.env.VITE_APP_ENV === 'development'
export const AUTH_LOGIN_PATH = '/api/auth/login'
export const AUTH_REFRESH_PATH = '/api/auth/refresh'
export const PLACEHOLDER_API_BASE_URL_MESSAGE =
  '当前接口基址仍是占位地址 api.example.com，请切换到开发/预览构建，或配置真实后端地址。'

let hasWarnedPlaceholderApiBaseUrl = false

export function isPlaceholderApiBaseUrl(): boolean {
  return IS_PLACEHOLDER_API_BASE_URL
}

export function warnPlaceholderApiBaseUrlOnce(): void {
  if (!shouldWarnPlaceholderApiBaseUrl() || hasWarnedPlaceholderApiBaseUrl) {
    return
  }

  console.warn(`[Request] ${PLACEHOLDER_API_BASE_URL_MESSAGE}`)
  hasWarnedPlaceholderApiBaseUrl = true
}
