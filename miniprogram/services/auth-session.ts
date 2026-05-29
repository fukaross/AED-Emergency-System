// token 存取这一层自己待着，避免 login 服务和 request 层互相 import 循环依赖
import type { LoginResponse } from '@/types/common'
import {
  AUTH_USER_INFO_STORAGE_KEY,
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
  uni.setStorageSync(AUTH_USER_INFO_STORAGE_KEY, result.userInfo)
}

export function clearAuthSession(): void {
  uni.removeStorageSync(TOKEN_STORAGE_KEY)
  uni.removeStorageSync(TOKEN_EXPIRE_KEY)
  uni.removeStorageSync(CURRENT_USER_ID_STORAGE_KEY)
  uni.removeStorageSync(AUTH_USER_INFO_STORAGE_KEY)
}
