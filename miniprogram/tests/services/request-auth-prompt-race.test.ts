import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@/services/request-config', () => ({
  AUTH_LOGIN_PATH: '/api/auth/wechat-login',
  AUTH_REFRESH_PATH: '/api/auth/refresh',
  BASE_URL: 'https://api.example.test',
  IS_DEV: false,
  PLACEHOLDER_API_BASE_URL_MESSAGE: 'placeholder',
  isPlaceholderApiBaseUrl: () => false,
  warnPlaceholderApiBaseUrlOnce: vi.fn(),
}))

const storage = new Map<string, unknown>()
const requests: UniApp.RequestOptions[] = []
let authenticationActive = false
let terminationCount = 0

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => storage.get(key)),
  removeStorageSync: vi.fn((key: string) => storage.delete(key)),
  request: vi.fn((options: UniApp.RequestOptions) => requests.push(options)),
  setStorageSync: vi.fn((key: string, value: unknown) => storage.set(key, value)),
  showModal: vi.fn(),
  showToast: vi.fn(),
})

import { get } from '@/services/request'
import { registerAuthenticationTerminator } from '@/services/auth-termination'
import {
  captureAuthSession,
  clearAuthSession,
  persistAuthSession,
} from '@/services/auth-session'
import {
  handleSessionAuthenticationRejection,
  resetAuthenticationRejectionState,
} from '@/services/request-errors'
import type { LoginResponse } from '@/types/common'
import { ERROR_CODES } from '@/utils/constants'

function buildLogin(token = 'token-a'): LoginResponse {
  return {
    token,
    expiresIn: 2_592_000,
    userInfo: { id: 'user-a', name: 'user-a', avatarUrl: '', isNewUser: false },
  }
}

function respond(index: number, statusCode: number, code: number, message: string): void {
  requests[index]?.success?.({
    statusCode,
    data: { code, message, data: null },
    header: {},
    cookies: [],
  })
}

function respondRefreshSuccess(index: number): void {
  requests[index]?.success?.({
    statusCode: 200,
    data: {
      code: 0,
      message: 'success',
      data: { token: 'refreshed-token', openId: 'user-a', isNewUser: false },
    },
    header: {},
    cookies: [],
  })
}

async function waitForRequestCount(expected: number): Promise<void> {
  for (let index = 0; index < 10 && requests.length < expected; index += 1) {
    await Promise.resolve()
  }
  expect(requests).toHaveLength(expected)
}

describe('request authentication prompt ownership', () => {
  beforeEach(() => {
    storage.clear()
    requests.length = 0
    authenticationActive = true
    terminationCount = 0
    vi.clearAllMocks()
    resetAuthenticationRejectionState()
    registerAuthenticationTerminator(() => {
      if (!authenticationActive) return
      authenticationActive = false
      terminationCount += 1
      clearAuthSession()
    })
    persistAuthSession(buildLogin())
  })

  it('lets a visible waiter show the shared refresh rejection after a silent waiter terminates auth', async () => {
    const silent = get('/api/silent', undefined, { skipErrorHandler: true })
    const visible = get('/api/visible')
    respond(0, 401, 1002, '登录已过期')
    respond(1, 401, 1002, '登录已过期')
    expect(requests).toHaveLength(3)

    respond(2, 401, 1002, 'Token 无效或已过期')
    await Promise.allSettled([silent, visible])

    expect(terminationCount).toBe(1)
    expect(uni.showModal).toHaveBeenCalledTimes(1)
    expect(uni.showModal).toHaveBeenCalledWith({
      title: '登录过期',
      content: '请重新登录',
      showCancel: false,
    })
  })

  it('coalesces concurrent JSON replay 401 responses into one modal', async () => {
    const first = get('/api/first')
    const second = get('/api/second')
    respond(0, 401, 1002, '登录已过期')
    respond(1, 401, 1002, '登录已过期')
    respondRefreshSuccess(2)
    await waitForRequestCount(5)

    respond(3, 401, 1002, 'Token 无效或已过期')
    respond(4, 401, 1002, 'Token 无效或已过期')
    await Promise.allSettled([first, second])

    expect(terminationCount).toBe(1)
    expect(uni.showModal).toHaveBeenCalledTimes(1)
  })

  it('terminates authentication for the dedicated disabled-account code', async () => {
    const pending = get('/api/private')

    respond(0, 403, ERROR_CODES.ACCOUNT_DISABLED, '账号已禁用')

    await expect(pending).rejects.toThrow('账号已禁用')
    expect(terminationCount).toBe(1)
    expect(uni.showModal).toHaveBeenCalledWith({
      title: '提示',
      content: '账号已禁用',
      showCancel: false,
    })
  })

  it('keeps business forbidden responses logged in', async () => {
    const pending = get('/api/responder-only')

    respond(0, 403, 1003, '仅认证急救员可执行该操作')

    await expect(pending).rejects.toThrow('仅认证急救员可执行该操作')
    expect(terminationCount).toBe(0)
    expect(uni.showModal).toHaveBeenCalledWith({
      title: '提示',
      content: '没有权限执行此操作',
      showCancel: false,
    })
  })

  it('does not let an old waiter prompt after a new login succeeds', () => {
    const oldSession = captureAuthSession()
    handleSessionAuthenticationRejection({
      code: ERROR_CODES.UNAUTHORIZED,
      message: '登录已过期',
      showPrompt: false,
      session: oldSession,
    })
    expect(terminationCount).toBe(1)

    authenticationActive = true
    persistAuthSession(buildLogin('token-b'))
    resetAuthenticationRejectionState()
    handleSessionAuthenticationRejection({
      code: ERROR_CODES.UNAUTHORIZED,
      message: '登录已过期',
      showPrompt: true,
      session: oldSession,
    })

    expect(storage.get('aed_auth_token')).toBe('token-b')
    expect(uni.showModal).not.toHaveBeenCalled()
  })
})
