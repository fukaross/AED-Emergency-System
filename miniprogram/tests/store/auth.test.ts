import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@/utils/runtime-env', () => ({
  API_BASE_URL: 'REDACTED',
  IS_PLACEHOLDER_API_BASE_URL: false,
  RESTORE_AUTH_SESSION: false,
  USER_BACKEND_READY: false,
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

const rescueApiMocks = vi.hoisted(() => ({
  fetchMineRescueSessions: vi.fn(),
}))

vi.mock('@/api/rescue-session', () => ({
  fetchMineRescueSessions: rescueApiMocks.fetchMineRescueSessions,
  fetchRescueSession: vi.fn(),
  joinRescueSession: vi.fn(),
  postRescueMessage: vi.fn(),
  postRescueStatusEvent: vi.fn(),
  reportRescueLocation: vi.fn(),
}))

vi.stubGlobal('uni', {
  getStorageSync: vi.fn(() => ''),
})

import { wxLogin } from '@/services/auth-service'
import { useAuthStore } from '@/store/auth'
import { useUserStore } from '@/store/user'
import { useRescueSessionStore } from '@/store/rescue-session'

describe('auth store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    rescueApiMocks.fetchMineRescueSessions.mockReset()
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
      primaryRole: 'student',
      certified: false,
    })
    expect(userStore.userInfo?.name).not.toBe('REDACTED')
  })

  it('does not expose the previous account sessions after logout and account switch', async () => {
    rescueApiMocks.fetchMineRescueSessions.mockResolvedValueOnce([{
      helpId: 'help-old',
      requestNo: 'REQ-OLD',
      helpStatus: 'responding',
      type: 'help_hand',
      address: '旧账号敏感地址',
      joined: true,
      joinedAt: '2026-07-18T10:00:00.000Z',
      unreadCount: 3,
      lastActivityAt: null,
    }])
    const authStore = useAuthStore()
    const userStore = useUserStore()
    const rescueStore = useRescueSessionStore()
    userStore.setLoginProfile({
      id: 'old-user',
      name: '旧用户',
      avatarUrl: '',
      gender: 'unknown',
      phone: '',
      primaryRole: 'student',
      certified: true,
      level: null,
      schoolProfile: null,
      rescuerSummary: null,
    })
    await rescueStore.loadMineSessions('old-user')

    authStore.logout()
    await authStore.login()
    userStore.setLoginProfile({ ...userStore.userInfo!, certified: true })

    expect(userStore.userInfo?.id).toBe('obTw23eNrLnMEI3Bbgk8abcd')
    expect(userStore.isCertified).toBe(true)
    expect(rescueStore.mineItems).toEqual([])
    expect(rescueStore.totalUnread).toBe(0)
  })
})
