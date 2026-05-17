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
import { AUTH_LOGIN_PATH } from '@/services/request-config'
import type { LoginResponse } from '@/types/common'
import { loadMockUserProfile } from '@/mock/user'
import {
  RESTORE_AUTH_SESSION,
  USE_MOCK,
  USE_REAL_WECHAT_FLOW,
} from '@/utils/runtime-env'
import { buildWeChatDisplayName } from '@/utils/wechat-display'

const MOCK_TOKEN = 'mock-token'
const MOCK_EXPIRES_IN = 7 * 24 * 60 * 60
const REAL_EXPIRES_IN = 7 * 24 * 60 * 60

interface WeChatLoginResponse {
  readonly token: string
  readonly openId: string
  readonly isNewUser: boolean
  readonly phone?: string | null
}

interface WeChatPhoneResponse {
  readonly openId: string
  readonly phone: string
}

/** 微信登录并获取 Token */
export async function wxLogin(): Promise<LoginResponse> {
  if (shouldUseMockAuth()) {
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
  const result = toLoginResponse(
    await post<WeChatLoginResponse>(AUTH_LOGIN_PATH, { code }),
  )

  persistAuthSession(result)

  return result
}

export async function bindWeChatPhone(code: string): Promise<WeChatPhoneResponse> {
  if (shouldUseMockAuth()) {
    const userInfo = loadMockUserProfile()
    return Promise.resolve({
      openId: userInfo.id,
      phone: userInfo.phone,
    })
  }

  return post<WeChatPhoneResponse>('/api/auth/wechat-phone', { code })
}

export async function bindManualPhone(phone: string): Promise<WeChatPhoneResponse> {
  if (shouldUseMockAuth()) {
    const userInfo = loadMockUserProfile()
    return Promise.resolve({
      openId: userInfo.id,
      phone: phone || userInfo.phone,
    })
  }

  return post<WeChatPhoneResponse>('/api/auth/wechat-phone', { phone })
}

/** 判断当前是否已登录且 Token 未过期 */
export function isAuthenticated(): boolean {
  const token = readToken()
  const expireAt = getTokenExpireAt()
  if (!token || !expireAt) return false
  return Date.now() < expireAt
}

export function initializeAuthState(): boolean {
  const authenticated = isAuthenticated()
  if (!authenticated) return false
  if (RESTORE_AUTH_SESSION) return true

  clearAuthSession()
  return false
}

/** 清除登录态 */
export function clearAuth(): void {
  clearAuthSession()
}

/** 获取当前 Token（仅供调试或特殊场景） */
export function getToken(): string {
  return readToken()
}

function toLoginResponse(response: WeChatLoginResponse): LoginResponse {
  return {
    token: response.token,
    expiresIn: REAL_EXPIRES_IN,
    userInfo: {
      id: response.openId,
      name: buildWeChatDisplayName(response.openId),
      avatarUrl: '',
      isNewUser: response.isNewUser,
    },
  }
}

function shouldUseMockAuth(): boolean {
  return USE_MOCK && !USE_REAL_WECHAT_FLOW
}
