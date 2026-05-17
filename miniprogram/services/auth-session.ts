/**
 * 登录态存取
 *
 * 请求层和登录服务都依赖这里，避免互相导入造成循环依赖。
 */
import type { LoginResponse } from '@/types/common'
import {
  AUTH_USER_INFO_STORAGE_KEY,
  CURRENT_USER_ID_STORAGE_KEY,
  TOKEN_EXPIRE_KEY,
  TOKEN_STORAGE_KEY,
} from '@/utils/constants'
import { RESTORE_AUTH_SESSION } from '@/utils/runtime-env'

let startupSessionChecked = false
let runtimeSessionAllowed = RESTORE_AUTH_SESSION

export function getToken(): string {
  discardStartupSessionIfNeeded()
  try {
    return (uni.getStorageSync(TOKEN_STORAGE_KEY) as string) || ''
  } catch {
    return ''
  }
}

export function getTokenExpireAt(): number {
  discardStartupSessionIfNeeded()
  return (uni.getStorageSync(TOKEN_EXPIRE_KEY) as number) || 0
}

export function persistAuthSession(result: LoginResponse): void {
  startupSessionChecked = true
  runtimeSessionAllowed = true
  uni.setStorageSync(TOKEN_STORAGE_KEY, result.token)
  uni.setStorageSync(TOKEN_EXPIRE_KEY, Date.now() + result.expiresIn * 1000)
  uni.setStorageSync(CURRENT_USER_ID_STORAGE_KEY, result.userInfo.id)
  uni.setStorageSync(AUTH_USER_INFO_STORAGE_KEY, result.userInfo)
}

export function clearAuthSession(): void {
  startupSessionChecked = true
  runtimeSessionAllowed = false
  uni.removeStorageSync(TOKEN_STORAGE_KEY)
  uni.removeStorageSync(TOKEN_EXPIRE_KEY)
  uni.removeStorageSync(CURRENT_USER_ID_STORAGE_KEY)
  uni.removeStorageSync(AUTH_USER_INFO_STORAGE_KEY)
}

function discardStartupSessionIfNeeded(): void {
  if (startupSessionChecked || runtimeSessionAllowed) return
  clearAuthSession()
}
