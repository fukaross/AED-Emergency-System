/**
 * 登录与鉴权服务
 *
 * 职责：微信登录流程、Token 管理、登录态判断
 * 不管用户资料（由 user store 管理）
 */
import { post } from '@/services/request'
import {
  clearAuthSession,
  getToken as readToken,
  getTokenExpireAt,
  persistAuthSession,
} from '@/services/auth-session'
import type { LoginResponse } from '@/types/common'
import { loadMockUserProfile } from '@/mock/user'
import { USE_MOCK } from '@/utils/runtime-env'

const MOCK_TOKEN = 'mock-token'
const MOCK_EXPIRES_IN = 7 * 24 * 60 * 60

/** 微信登录并获取 Token */
export async function wxLogin(): Promise<LoginResponse> {
  if (USE_MOCK) {
    const userInfo = loadMockUserProfile()
    const result: LoginResponse = {
      token: MOCK_TOKEN,
      expiresIn: MOCK_EXPIRES_IN,
      userInfo: {
        id: userInfo.id,
        name: userInfo.name,
        avatarUrl: userInfo.avatarUrl,
        isNewUser: false,
      },
    }

    persistAuthSession(result)
    console.log('[Mock Auth API] login success')
    return result
  }

  const { code } = await uni.login({ provider: 'weixin' })
  const result = await post<LoginResponse>('/api/auth/login', { code })

  persistAuthSession(result)

  return result
}

/** 判断当前是否已登录且 Token 未过期 */
export function isAuthenticated(): boolean {
  const token = readToken()
  const expireAt = getTokenExpireAt()
  if (!token || !expireAt) return false
  return Date.now() < expireAt
}

/** 清除登录态 */
export function clearAuth(): void {
  clearAuthSession()
}

/** 获取当前 Token（仅供调试或特殊场景） */
export function getToken(): string {
  return readToken()
}
