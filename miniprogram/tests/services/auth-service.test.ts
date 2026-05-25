import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@/utils/runtime-env', () => ({
  API_BASE_URL: 'REDACTED',
  IS_PLACEHOLDER_API_BASE_URL: false,
  RESTORE_AUTH_SESSION: false,
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
import {
  AUTH_USER_INFO_STORAGE_KEY,
  CURRENT_USER_ID_STORAGE_KEY,
  TOKEN_EXPIRE_KEY,
  TOKEN_STORAGE_KEY,
} from '@/utils/constants'

describe('auth service', () => {
  beforeEach(() => {
    storage.clear()
    vi.clearAllMocks()
  })

  it('does not restore stored login state when restore is disabled', () => {
    storage.set(TOKEN_STORAGE_KEY, 'old-token')
    storage.set(TOKEN_EXPIRE_KEY, Date.now() + 60000)
    storage.set(CURRENT_USER_ID_STORAGE_KEY, 'openid-old')
    storage.set(AUTH_USER_INFO_STORAGE_KEY, { id: 'openid-old' })

    expect(initializeAuthState()).toBe(false)
    expect(storage.has(TOKEN_STORAGE_KEY)).toBe(false)
    expect(storage.has(TOKEN_EXPIRE_KEY)).toBe(false)
    expect(storage.has(CURRENT_USER_ID_STORAGE_KEY)).toBe(false)
    expect(storage.has(AUTH_USER_INFO_STORAGE_KEY)).toBe(false)
  })

  it('returns false when no token in storage', () => {
    expect(initializeAuthState()).toBe(false)
  })

  it('clears session when restore disabled even with valid token', () => {
    storage.set(TOKEN_STORAGE_KEY, 'valid-token')
    storage.set(TOKEN_EXPIRE_KEY, Date.now() + 3600000)
    storage.set(AUTH_USER_INFO_STORAGE_KEY, { id: 'x' })

    expect(initializeAuthState()).toBe(false)
    expect(storage.has(TOKEN_STORAGE_KEY)).toBe(false)
  })
})
