// 登录 token 自动刷新逻辑
import {
  captureAuthSession,
  createAuthSessionChangedError,
  isAuthEpochCurrent,
  isAuthSessionCurrent,
  replaceAuthSession,
  type AuthSessionContext,
} from '@/services/auth-session'
import { AUTH_LOGIN_PATH, AUTH_REFRESH_PATH, BASE_URL } from '@/services/request-config'
import { createApiError, extractApiResponse } from '@/services/request-response'
import type { LoginResponse } from '@/types/common'
import { ERROR_CODES } from '@/utils/constants'
import { buildWeChatDisplayName } from '@/utils/wechat-display'

const DEFAULT_EXPIRES_IN = 30 * 24 * 60 * 60

interface WeChatRefreshResponse {
  readonly token: string
  readonly openId: string
  readonly isNewUser: boolean
  readonly expiresIn?: number
  readonly phone?: string | null
}

interface RefreshFlight {
  readonly key: string
  readonly promise: Promise<void>
}

interface RefreshResponseContext {
  readonly expected: AuthSessionContext
  readonly resolve: () => void
  readonly reject: (reason?: unknown) => void
}

export interface RefreshAuthenticationRejection {
  readonly code: typeof ERROR_CODES.UNAUTHORIZED | typeof ERROR_CODES.ACCOUNT_DISABLED
  readonly message: string
}

let activeRefresh: RefreshFlight | null = null

export function canRefreshAuth(
  url: string,
  skipAuthRefresh: boolean,
  context = captureAuthSession(),
): boolean {
  if (skipAuthRefresh || url === AUTH_LOGIN_PATH || url === AUTH_REFRESH_PATH) {
    return false
  }

  return Boolean(context.token)
}

/** Distinguishes terminal auth rejection from a transport or server-side refresh failure. */
export function getRefreshAuthenticationRejection(
  error: unknown,
): RefreshAuthenticationRejection | null {
  if (!error || typeof error !== 'object') return null
  const candidate = error as {
    readonly code?: number
    readonly message?: string
    readonly statusCode?: number
  }
  if (candidate.code === ERROR_CODES.ACCOUNT_DISABLED) {
    return {
      code: ERROR_CODES.ACCOUNT_DISABLED,
      message: candidate.message || '账号已禁用',
    }
  }
  if (candidate.code === ERROR_CODES.UNAUTHORIZED || candidate.statusCode === 401) {
    return { code: ERROR_CODES.UNAUTHORIZED, message: '登录已过期，请重新登录' }
  }
  return null
}

export function refreshAuthSession(
  timeout: number,
  expected = captureAuthSession(),
): Promise<void> {
  if (!expected.token) {
    return Promise.reject(createApiError('缺少刷新 Token 所需的登录态', ERROR_CODES.UNAUTHORIZED))
  }

  if (!isAuthEpochCurrent(expected)) {
    return Promise.reject(createAuthSessionChangedError())
  }

  if (!isAuthSessionCurrent(expected)) return Promise.resolve()

  const key = `${expected.epoch}:${expected.token}`
  if (activeRefresh?.key === key) return activeRefresh.promise

  const promise = requestRefresh(timeout, expected)
  const flight = { key, promise }
  activeRefresh = flight
  promise.then(
    () => clearRefreshFlight(flight),
    () => clearRefreshFlight(flight),
  )
  return promise
}

function requestRefresh(
  timeout: number,
  expected: AuthSessionContext,
): Promise<void> {
  return new Promise((resolve, reject) => {
    uni.request({
      url: `${BASE_URL}${AUTH_REFRESH_PATH}`,
      method: 'POST',
      header: { Authorization: `Bearer ${expected.token}` },
      timeout,
      success: (res) => handleRefreshResponse(res, { expected, resolve, reject }),
      fail: (err) => reject(new Error(err.errMsg || '刷新 Token 请求失败')),
    })
  })
}

function handleRefreshResponse(
  res: UniApp.RequestSuccessCallbackResult,
  context: RefreshResponseContext,
): void {
  const { expected, reject, resolve } = context
  const body = extractApiResponse<WeChatRefreshResponse>(res.data)
  if (!body) {
    reject(createApiError('刷新 Token 响应格式不正确', undefined, res.statusCode))
    return
  }

  if (res.statusCode < 200 || res.statusCode >= 300 || body.code !== ERROR_CODES.SUCCESS) {
    reject(createApiError(body.message, body.code, res.statusCode))
    return
  }

  if (!replaceAuthSession(toLoginResponse(body.data), expected)) {
    reject(createAuthSessionChangedError())
    return
  }

  resolve()
}

function clearRefreshFlight(flight: RefreshFlight): void {
  if (activeRefresh === flight) activeRefresh = null
}

function toLoginResponse(response: WeChatRefreshResponse): LoginResponse {
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
