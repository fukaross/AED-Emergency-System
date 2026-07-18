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

const errorMocks = vi.hoisted(() => ({
  handleAuthenticationRejection: vi.fn(),
  handleErrorCode: vi.fn(),
  handleSessionAuthenticationRejection: vi.fn(),
  terminateAuthentication: vi.fn(),
}))

vi.mock('@/services/request-errors', () => errorMocks)

const storage = new Map<string, unknown>()
const requests: UniApp.RequestOptions[] = []

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => storage.get(key)),
  removeStorageSync: vi.fn((key: string) => storage.delete(key)),
  request: vi.fn((options: UniApp.RequestOptions) => requests.push(options)),
  setStorageSync: vi.fn((key: string, value: unknown) => storage.set(key, value)),
  showToast: vi.fn(),
})

import { get, put } from '@/services/request'
import { clearAuthSession, persistAuthSession } from '@/services/auth-session'
import type { LoginResponse } from '@/types/common'

function buildLogin(token: string, id: string): LoginResponse {
  return {
    token,
    expiresIn: 2592000,
    userInfo: { id, name: id, avatarUrl: '', isNewUser: false },
  }
}

interface MockApiResponse {
  readonly index: number
  readonly statusCode: number
  readonly code: number
  readonly data?: unknown
}

function respond(response: MockApiResponse): void {
  respondRaw(response.index, response.statusCode, {
    code: response.code,
    message: response.code === 0
      ? 'success'
      : response.code === 1018 ? '账号已禁用' : 'unauthorized',
    data: response.data,
  })
}

function respondRaw(index: number, statusCode: number, data: unknown): void {
  requests[index]?.success?.({
    statusCode,
    data,
    header: {},
    cookies: [],
  })
}

function failRequest(index: number, message: string): void {
  requests[index]?.fail?.({ errMsg: message })
}

describe('request authentication ownership', () => {
  beforeEach(() => {
    storage.clear()
    requests.length = 0
    vi.clearAllMocks()
    errorMocks.handleAuthenticationRejection.mockImplementation(() => {
      errorMocks.terminateAuthentication()
    })
    errorMocks.handleSessionAuthenticationRejection.mockImplementation(() => {
      errorMocks.terminateAuthentication()
    })
    errorMocks.handleErrorCode.mockImplementation((code: number) => {
      if (code === 1002) errorMocks.terminateAuthentication()
    })
  })

  it('does not replay a location request after the account changes', async () => {
    persistAuthSession(buildLogin('token-a', 'user-a'))
    const pending = put('/api/help/help-1/session/location', { lat: 1, lng: 2 })

    respond({ index: 0, statusCode: 401, code: 1002 })
    expect(requests).toHaveLength(2)
    persistAuthSession(buildLogin('token-b', 'user-b'))
    respond({ index: 1, statusCode: 200, code: 0, data: {
      token: 'late-token-a',
      openId: 'user-a',
      isNewUser: false,
    } })

    await expect(pending).rejects.toThrow('登录账号已变更')
    expect(requests).toHaveLength(2)
    expect(storage.get('aed_auth_token')).toBe('token-b')
    expect(errorMocks.handleErrorCode).not.toHaveBeenCalled()
    expect(errorMocks.terminateAuthentication).not.toHaveBeenCalled()
  })

  it('does not persist or replay a late refresh after logout', async () => {
    persistAuthSession(buildLogin('token-a', 'user-a'))
    const pending = put('/api/help/help-1/session/location', { lat: 1, lng: 2 })

    respond({ index: 0, statusCode: 401, code: 1002 })
    clearAuthSession()
    respond({ index: 1, statusCode: 200, code: 0, data: {
      token: 'late-token-a',
      openId: 'user-a',
      isNewUser: false,
    } })

    await expect(pending).rejects.toThrow('登录账号已变更')
    expect(requests).toHaveLength(2)
    expect(storage.has('aed_auth_token')).toBe(false)
    expect(errorMocks.terminateAuthentication).not.toHaveBeenCalled()
  })

  it('treats a bare HTTP 401 as an authentication terminal response', async () => {
    persistAuthSession(buildLogin('token-a', 'user-a'))
    const pending = get('/api/private')

    respondRaw(0, 401, { invalid: true })
    expect(requests).toHaveLength(2)
    respondRaw(1, 401, { invalid: true })

    await expect(pending).rejects.toThrow('刷新 Token 响应格式不正确')
    expect(errorMocks.handleSessionAuthenticationRejection).toHaveBeenCalledWith(
      expect.objectContaining({
        code: 1002,
        message: '登录已过期，请重新登录',
        showPrompt: true,
      }),
    )
    expect(errorMocks.terminateAuthentication).toHaveBeenCalledTimes(1)
  })

  it('does not terminate authentication when a refreshed replay fails with 500', async () => {
    persistAuthSession(buildLogin('token-a', 'user-a'))
    const pending = get('/api/private')

    respond({ index: 0, statusCode: 401, code: 1002 })
    respond({ index: 1, statusCode: 200, code: 0, data: {
      token: 'token-a-refreshed',
      openId: 'user-a',
      isNewUser: false,
    } })
    await Promise.resolve()
    await Promise.resolve()
    expect(requests).toHaveLength(3)
    respondRaw(2, 500, { code: 5000, message: 'server failed' })

    await expect(pending).rejects.toThrow('server failed')
    expect(errorMocks.handleErrorCode).toHaveBeenCalledWith(5000, 'server failed')
    expect(errorMocks.terminateAuthentication).not.toHaveBeenCalled()
  })

  it('keeps authentication when a silent refresh request fails in transit', async () => {
    persistAuthSession(buildLogin('token-a', 'user-a'))
    const pending = get('/api/private', undefined, { skipErrorHandler: true })

    respond({ index: 0, statusCode: 401, code: 1002 })
    expect(requests).toHaveLength(2)
    failRequest(1, 'request:fail timeout')

    await expect(pending).rejects.toThrow('request:fail timeout')
    expect(storage.get('aed_auth_token')).toBe('token-a')
    expect(errorMocks.handleSessionAuthenticationRejection).not.toHaveBeenCalled()
    expect(errorMocks.handleErrorCode).not.toHaveBeenCalled()
    expect(errorMocks.terminateAuthentication).not.toHaveBeenCalled()
  })

  it('terminates authentication when refresh rejects a disabled account', async () => {
    persistAuthSession(buildLogin('token-a', 'user-a'))
    const pending = get('/api/private')

    respond({ index: 0, statusCode: 401, code: 1002 })
    expect(requests).toHaveLength(2)
    respond({ index: 1, statusCode: 403, code: 1018 })

    await expect(pending).rejects.toThrow('账号已禁用')
    expect(errorMocks.handleSessionAuthenticationRejection).toHaveBeenCalledWith(
      expect.objectContaining({
        code: 1018,
        message: '账号已禁用',
        showPrompt: true,
      }),
    )
    expect(errorMocks.terminateAuthentication).toHaveBeenCalledTimes(1)
  })

  it('terminates silently when a silent refresh rejects a disabled account', async () => {
    persistAuthSession(buildLogin('token-a', 'user-a'))
    const pending = get('/api/private', undefined, { skipErrorHandler: true })

    respond({ index: 0, statusCode: 401, code: 1002 })
    respond({ index: 1, statusCode: 403, code: 1018 })

    await expect(pending).rejects.toThrow('账号已禁用')
    expect(errorMocks.handleSessionAuthenticationRejection).toHaveBeenCalledWith(
      expect.objectContaining({
        code: 1018,
        message: '账号已禁用',
        showPrompt: false,
      }),
    )
    expect(errorMocks.handleErrorCode).not.toHaveBeenCalled()
    expect(errorMocks.terminateAuthentication).toHaveBeenCalledTimes(1)
  })
})
