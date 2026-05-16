/**
 * 登录态存取
 *
 * 请求层和登录服务都依赖这里，避免互相导入造成循环依赖。
 */
import type { LoginResponse } from '@/types/common'
import {
  CURRENT_USER_ID_STORAGE_KEY,
  TOKEN_EXPIRE_KEY,
  TOKEN_STORAGE_KEY,
} from '@/utils/constants'

export function getToken(): string {
  try {
    return (uni.getStorageSync(TOKEN_STORAGE_KEY) as string) || ''
  } catch {
    return ''
  }
}

export function getTokenExpireAt(): number {
  return (uni.getStorageSync(TOKEN_EXPIRE_KEY) as number) || 0
}

export function persistAuthSession(result: LoginResponse): void {
  uni.setStorageSync(TOKEN_STORAGE_KEY, result.token)
  uni.setStorageSync(TOKEN_EXPIRE_KEY, Date.now() + result.expiresIn * 1000)
  uni.setStorageSync(CURRENT_USER_ID_STORAGE_KEY, result.userInfo.id)
}

export function clearAuthSession(): void {
  uni.removeStorageSync(TOKEN_STORAGE_KEY)
  uni.removeStorageSync(TOKEN_EXPIRE_KEY)
  uni.removeStorageSync(CURRENT_USER_ID_STORAGE_KEY)
}
