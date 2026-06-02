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
})

import { initializeAuthState } from '@/services/auth-service'
import { TOKEN_EXPIRE_KEY, TOKEN_STORAGE_KEY } from '@/utils/constants'

describe('auth service (restore enabled)', () => {
  beforeEach(() => {
    storage.clear()
    vi.clearAllMocks()
  })

  it('restores login state when token exists and restore is enabled', () => {
    storage.set(TOKEN_STORAGE_KEY, 'valid-token')
    storage.set(TOKEN_EXPIRE_KEY, Date.now() + 3600000)

    expect(initializeAuthState()).toBe(true)
    expect(storage.has(TOKEN_STORAGE_KEY)).toBe(true)
    expect(storage.has(TOKEN_EXPIRE_KEY)).toBe(true)
  })

  it('restores login state even when local expire time has passed', () => {
    storage.set(TOKEN_STORAGE_KEY, 'stale-token')
    storage.set(TOKEN_EXPIRE_KEY, Date.now() - 86400000)

    expect(initializeAuthState()).toBe(true)
    expect(storage.has(TOKEN_STORAGE_KEY)).toBe(true)
  })

  it('returns false when no token in storage', () => {
    expect(initializeAuthState()).toBe(false)
  })
})
