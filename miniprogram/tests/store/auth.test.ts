import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@/utils/runtime-env', () => ({
  API_BASE_URL: 'REDACTED',
  APP_ENV: 'test',
  IS_PLACEHOLDER_API_BASE_URL: false,
  RESTORE_AUTH_SESSION: false,
  USER_BACKEND_READY: false,
  USE_MOCK: true,
  USE_PUBLIC_DEMO_DATA: true,
  USE_REAL_LOCATION: false,
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

const rescueSocketMocks = vi.hoisted(() => ({
  closeAllRescueSockets: vi.fn(),
}))

vi.mock('@/api/rescue-session', () => ({
  fetchMineRescueSessions: rescueApiMocks.fetchMineRescueSessions,
  fetchRescueSession: vi.fn(),
  joinRescueSession: vi.fn(),
  postRescueMessage: vi.fn(),
  postRescueStatusEvent: vi.fn(),
  reportRescueLocation: vi.fn(),
}))

vi.mock('@/services/rescue-socket', () => rescueSocketMocks)

vi.stubGlobal('uni', {
  getStorageSync: vi.fn(() => ''),
  removeStorageSync: vi.fn(),
  setStorageSync: vi.fn(),
  showModal: vi.fn(),
})

import { clearAuth, wxLogin } from '@/services/auth-service'
import { handleErrorCode } from '@/services/request-errors'
import { useAuthStore } from '@/store/auth'
import { useHelpStore } from '@/store/help'
import { useUserStore } from '@/store/user'
import { useRescueSessionStore } from '@/store/rescue-session'
import type { RescueSessionSnapshot } from '@/types/rescue-session'

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

  it('actively closes rescue sockets on logout', () => {
    const authStore = useAuthStore()

    authStore.logout()

    expect(rescueSocketMocks.closeAllRescueSockets).toHaveBeenCalledTimes(1)
  })

  it('terminates all authenticated state immediately on code 1002', async () => {
    const authStore = useAuthStore()
    const helpStore = useHelpStore()
    const rescueStore = useRescueSessionStore()
    const userStore = useUserStore()
    await authStore.login()
    helpStore.guestSession = {
      requestNo: 'GUEST-1',
      followupCode: 'follow-up',
      closeToken: 'close-token',
    }
    helpStore.helpList = [{
      id: 'help-a',
      requestNo: 'REQ-A',
      content: '',
      type: 'general',
      status: 'published',
      address: '账号 A 地址',
      createdAt: '2026-07-18T10:00:00.000Z',
      respondersCount: 0,
      latestEta: '',
      userRole: 'requester',
    }]
    helpStore.currentRequest = {
      id: 'help-a',
      requestNo: 'REQ-A',
      status: 'published',
      type: 'general',
      address: '账号 A 地址',
      content: '',
      lat: 19.97,
      lng: 110.51,
      images: [],
      requesterPhone: 'REDACTED',
      responders: [],
      estimatedArrivalTime: '',
      reports: [],
      timeline: [],
      createdAt: '2026-07-18T10:00:00.000Z',
      viewerRole: 'requester',
      canSubmitReport: false,
    }
    rescueStore.mineItems = [{
      helpId: 'help-a',
      requestNo: 'REQ-A',
      helpStatus: 'responding',
      type: 'general',
      address: '账号 A 地址',
      joined: true,
      joinedAt: '2026-07-18T10:00:00.000Z',
      unreadCount: 1,
      lastActivityAt: null,
    }]
    rescueStore.snapshot = buildRescueSnapshot()

    handleErrorCode(1002, 'Token 已失效')

    expect(clearAuth).toHaveBeenCalled()
    expect(authStore.isLoggedIn).toBe(false)
    expect(userStore.userInfo).toBeNull()
    expect(helpStore.helpList).toEqual([])
    expect(helpStore.currentRequest).toBeNull()
    expect(helpStore.guestSession?.followupCode).toBe('follow-up')
    expect(rescueStore.mineItems).toEqual([])
    expect(rescueStore.snapshot).toBeNull()
  })

  it('does not revive storage when a pending login finishes after logout', async () => {
    let finishLogin: ((value: Awaited<ReturnType<typeof wxLogin>>) => void) | undefined
    vi.mocked(wxLogin).mockReturnValueOnce(new Promise((resolve) => {
      finishLogin = resolve
    }))
    const authStore = useAuthStore()
    const pending = authStore.login()

    authStore.logout()
    finishLogin?.({
      token: 'late-token',
      expiresIn: 2592000,
      userInfo: {
        id: 'late-user',
        name: '迟到账号',
        avatarUrl: '',
        isNewUser: false,
      },
    })
    await pending

    expect(authStore.isLoggedIn).toBe(false)
    expect(uni.setStorageSync).not.toHaveBeenCalledWith('aed_auth_token', 'late-token')
  })
})

function buildRescueSnapshot(): RescueSessionSnapshot {
  return {
    helpId: 'help-a',
    requestNo: 'REQ-A',
    helpStatus: 'responding',
    sos: { lat: 19.97, lng: 110.51, address: '账号 A 地址' },
    me: { openId: 'user-a', takenAed: false, arrived: false, lastReadSeq: 0 },
    participants: [],
    messages: [],
    latestSeq: 0,
    serverTime: '2026-07-18T10:00:00.000Z',
    timing: {
      signalLostAfterSeconds: 45,
      locationGoneAfterSeconds: 90,
      locationReportIntervalSeconds: 5,
    },
  }
}
