/**
 * 登录与鉴权服务
 *
 * 职责：微信登录流程、Token 管理、登录态判断
 * 不管用户资料（由 user store 管理）
 */
import { post } from '@/services/request'
import { TOKEN_STORAGE_KEY, TOKEN_EXPIRE_KEY } from '@/utils/constants'
import type { LoginResponse } from '@/types/common'

/** 微信登录并获取 Token */
export async function wxLogin(): Promise<LoginResponse> {
  const { code } = await uni.login({ provider: 'weixin' })
  const result = await post<LoginResponse>('/api/auth/login', { code })

  uni.setStorageSync(TOKEN_STORAGE_KEY, result.token)
  uni.setStorageSync(
    TOKEN_EXPIRE_KEY,
    Date.now() + result.expiresIn * 1000,
  )

  return result
}

/** 判断当前是否已登录且 Token 未过期 */
export function isAuthenticated(): boolean {
  const token = uni.getStorageSync(TOKEN_STORAGE_KEY) as string
  const expireAt = uni.getStorageSync(TOKEN_EXPIRE_KEY) as number
  if (!token || !expireAt) return false
  return Date.now() < expireAt
}

/** 清除登录态 */
export function clearAuth(): void {
  uni.removeStorageSync(TOKEN_STORAGE_KEY)
  uni.removeStorageSync(TOKEN_EXPIRE_KEY)
}

/** 获取当前 Token（仅供调试或特殊场景） */
export function getToken(): string {
  return (uni.getStorageSync(TOKEN_STORAGE_KEY) as string) || ''
}
