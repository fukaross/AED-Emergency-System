// 微信登录流程、token 存取、登录态判断（不碰用户资料，那归 user store 管）
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
const DEFAULT_EXPIRES_IN = 30 * 24 * 60 * 60

interface WeChatLoginResponse {
  readonly token: string
  readonly openId: string
  readonly isNewUser: boolean
  readonly expiresIn?: number
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

/** 判断当前是否已登录且 Token 未过期（仅本地时间判断） */
export function isAuthenticated(): boolean {
  const token = readToken()
  const expireAt = getTokenExpireAt()
  if (!token || !expireAt) return false
  return Date.now() < expireAt
}

/**
 * 启动时初始化登录态。
 *
 * RESTORE_AUTH_SESSION = false（如开发环境）时，清除所有登录数据。
 * RESTORE_AUTH_SESSION = true  时，只要本地有 token 就恢复登录态；
 * token 是否真正有效由服务端鉴权结果决定，前端不基于本地时间提前判定过期。
 */
export function initializeAuthState(): boolean {
  const token = readToken()
  if (!token) return false

  if (!RESTORE_AUTH_SESSION) {
    clearAuthSession()
    return false
  }

  return true
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
    expiresIn: response.expiresIn ?? DEFAULT_EXPIRES_IN,
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
