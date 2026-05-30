import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@/utils/runtime-env', () => ({
  API_BASE_URL: 'REDACTED',
  IS_PLACEHOLDER_API_BASE_URL: false,
  RESTORE_AUTH_SESSION: false,
  USE_MOCK: true,
  USE_PUBLIC_DEMO_DATA: true,
  USE_REAL_WECHAT_FLOW: true,
}))

vi.mock('@/services/auth-service', () => ({
  clearAuth: vi.fn(),
  initializeAuthState: vi.fn(() => false),
  isAuthenticated: vi.fn(() => false),
  wxLogin: vi.fn(() =>
    Promise.resolve({
      token: 'real-token',
      expiresIn: 2592000,
      userInfo: {
        id: 'obTw23eNrLnMEI3Bbgk8abcd',
        name: '微信用户 obTw...abcd',
        avatarUrl: '',
        isNewUser: true,
      },
    }),
  ),
}))

vi.stubGlobal('uni', {
  getStorageSync: vi.fn(() => ''),
})

import { wxLogin } from '@/services/auth-service'
import { useAuthStore } from '@/store/auth'
import { useUserStore } from '@/store/user'

describe('auth store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('uses real wechat login profile instead of mock archive user', async () => {
    const authStore = useAuthStore()
    const userStore = useUserStore()

    await authStore.login()

    expect(wxLogin).toHaveBeenCalledTimes(1)
    expect(authStore.isLoggedIn).toBe(true)
    expect(userStore.userInfo).toMatchObject({
      id: 'obTw23eNrLnMEI3Bbgk8abcd',
      name: '微信用户 obTw...abcd',
      certified: false,
    })
    expect(userStore.userInfo?.name).not.toBe('REDACTED')
  })
})
