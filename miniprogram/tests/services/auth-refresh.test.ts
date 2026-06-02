import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@/utils/runtime-env', () => ({
  API_BASE_URL: 'REDACTED',
  IS_PLACEHOLDER_API_BASE_URL: false,
  RESTORE_AUTH_SESSION: true,
  USER_BACKEND_READY: false,
  USE_MOCK: false,
  USE_REAL_WECHAT_FLOW: true,
  shouldWarnPlaceholderApiBaseUrl: vi.fn(() => false),
}))

const storage = new Map<string, unknown>()

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => storage.get(key)),
  removeStorageSync: vi.fn((key: string) => {
    storage.delete(key)
  }),
  setStorageSync: vi.fn((key: string, value: unknown) => {
    storage.set(key, value)
  }),
  request: vi.fn(),
})

import { refreshAuthSession } from '@/services/auth-refresh'
import {
  AUTH_USER_INFO_STORAGE_KEY,
  CURRENT_USER_ID_STORAGE_KEY,
  TOKEN_EXPIRE_KEY,
  TOKEN_STORAGE_KEY,
} from '@/utils/constants'

function mockRefreshSuccess(data: Record<string, unknown>) {
  // eslint-disable-next-line no-restricted-properties
  const mockCall = (vi.mocked(uni.request) as ReturnType<typeof vi.fn>).mock.calls[0]![0]!
  mockCall.success({
    statusCode: 200,
    data: { code: 0, message: 'success', data },
  })
}

function mockRefreshFailure(statusCode: number, code: number, message: string) {
  // eslint-disable-next-line no-restricted-properties
  const mockCall = (vi.mocked(uni.request) as ReturnType<typeof vi.fn>).mock.calls[0]![0]!
  mockCall.success({
    statusCode,
    data: { code, message },
  })
}

describe('auth refresh', () => {
  beforeEach(() => {
    storage.clear()
    vi.clearAllMocks()
  })

  it('converts WeChatLoginResponse to LoginResponse and persists session', async () => {
    storage.set(TOKEN_STORAGE_KEY, 'old-token')

    const responsePromise = refreshAuthSession(10000)
    mockRefreshSuccess({
      token: 'new-token',
      openId: 'o-test-openid',
      isNewUser: false,
      expiresIn: 2592000,
    })

    await responsePromise

    expect(storage.get(TOKEN_STORAGE_KEY)).toBe('new-token')
    const expireAt = storage.get(TOKEN_EXPIRE_KEY) as number
    expect(expireAt).toBeGreaterThan(Date.now() + 2591000 * 1000)
    expect(storage.get(CURRENT_USER_ID_STORAGE_KEY)).toBe('o-test-openid')

    const userInfo = storage.get(AUTH_USER_INFO_STORAGE_KEY) as Record<string, unknown>
    expect(userInfo.id).toBe('o-test-openid')
    expect(userInfo.isNewUser).toBe(false)
    expect(typeof userInfo.name).toBe('string')
  })

  it('uses default expiresIn when server does not return it', async () => {
    storage.set(TOKEN_STORAGE_KEY, 'old-token')

    const responsePromise = refreshAuthSession(10000)
    mockRefreshSuccess({
      token: 'new-token',
      openId: 'o-test-openid',
      isNewUser: false,
    })

    await responsePromise

    const expireAt = storage.get(TOKEN_EXPIRE_KEY) as number
    expect(expireAt).toBeGreaterThan(Date.now() + 29 * 24 * 3600 * 1000)
  })

  it('rejects when server returns 401 (token invalid)', async () => {
    storage.set(TOKEN_STORAGE_KEY, 'old-token')

    const responsePromise = refreshAuthSession(10000)
    mockRefreshFailure(401, 1002, 'Token 无效或已过期')

    await expect(responsePromise).rejects.toThrow('Token 无效或已过期')
  })

  it('rejects when token is missing from storage', async () => {
    await expect(refreshAuthSession(10000)).rejects.toThrow('缺少刷新 Token 所需的登录态')
  })
})
