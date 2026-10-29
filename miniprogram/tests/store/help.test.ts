import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { HelpListItem, HelpRequestDetail } from '@/types/help'

const apiMocks = vi.hoisted(() => ({
  fetchHelpDetail: vi.fn(),
  fetchHelpList: vi.fn(),
  guestCloseHelp: vi.fn(),
  guestQueryHelp: vi.fn(),
  publishHelp: vi.fn(),
  submitHelpReport: vi.fn(),
}))

vi.mock('@/api/help', () => apiMocks)

const storage = new Map<string, unknown>()

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => storage.get(key)),
  removeStorageSync: vi.fn((key: string) => storage.delete(key)),
  setStorageSync: vi.fn((key: string, value: unknown) => storage.set(key, value)),
})

import { clearAuthSession, persistAuthSession } from '@/services/auth-session'
import { useHelpStore } from '@/store/help'
import type { LoginResponse } from '@/types/common'

const helpDetail = {
  id: 'help-1',
  requestNo: 'HELP0001',
  status: 'published',
  lat: 31.2315,
  lng: 121.4785,
  address: '东区',
  content: '需要帮助',
  type: 'general',
  images: [],
  requesterPhone: '13800000000',
  responders: [],
  reports: [],
  timeline: [],
  createdAt: '2026-05-24T10:00:00.000Z',
} as HelpRequestDetail

function buildListItem(id: string, address: string): HelpListItem {
  return {
    id,
    requestNo: id,
    status: 'published',
    type: 'general',
    address,
    content: '',
    respondersCount: 0,
    latestEta: '',
    createdAt: '2026-07-18T10:00:00.000Z',
    userRole: 'requester',
  }
}

describe('help store silent reads', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    storage.clear()
    vi.clearAllMocks()
    apiMocks.fetchHelpDetail.mockResolvedValue(helpDetail)
    apiMocks.fetchHelpList.mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0,
    })
  })

  it('forwards silent detail reads to the api layer', async () => {
    const store = useHelpStore()

    await store.loadDetail('help-1', true)

    expect(apiMocks.fetchHelpDetail).toHaveBeenCalledWith('help-1', {
      silent: true,
    })
    expect(store.currentRequest).toEqual(helpDetail)
  })

  it('does not show list loading state during silent polling reads', async () => {
    const store = useHelpStore()

    await store.loadList(1, 20, true)

    expect(apiMocks.fetchHelpList).toHaveBeenCalledWith(1, 20, {
      silent: true,
    })
    expect(store.loadingList).toBe(false)
  })

  it('keeps normal list reads user-visible', async () => {
    const store = useHelpStore()

    await store.loadList()

    expect(apiMocks.fetchHelpList).toHaveBeenCalledWith(1, 20, {
      silent: false,
    })
    expect(store.loadingList).toBe(false)
  })

  it('keeps loading visible while an older visible read overlaps a silent read', async () => {
    let finishVisible: ((value: ReturnType<typeof buildListResponse>) => void) | undefined
    apiMocks.fetchHelpList.mockReset()
    apiMocks.fetchHelpList
      .mockReturnValueOnce(new Promise((resolve) => {
        finishVisible = resolve
      }))
      .mockResolvedValueOnce(buildListResponse(buildListItem('silent', '静默刷新')))
    const store = useHelpStore()

    const visible = store.loadList()
    await store.loadList(1, 20, true)
    expect(store.loadingList).toBe(true)
    finishVisible?.(buildListResponse(buildListItem('visible', '可见刷新')))
    await visible

    expect(store.loadingList).toBe(false)
  })

  it('preserves guest follow-up credentials while clearing authenticated data', () => {
    const store = useHelpStore()
    store.currentRequest = helpDetail
    store.guestSession = {
      requestNo: 'GUEST-1',
      followupCode: 'follow-up',
      closeToken: 'close-token',
    }

    store.clearAuthenticatedState()

    expect(store.currentRequest).toBeNull()
    expect(store.helpList).toEqual([])
    expect(store.guestSession?.followupCode).toBe('follow-up')
  })

  it('ignores a late list response from a cleared account generation', async () => {
    let finishOld: ((value: ReturnType<typeof buildListResponse>) => void) | undefined
    apiMocks.fetchHelpList.mockReset()
    apiMocks.fetchHelpList
      .mockReturnValueOnce(new Promise((resolve) => {
        finishOld = resolve
      }))
      .mockResolvedValueOnce(buildListResponse(buildListItem('new', '新账号地址')))
    const store = useHelpStore()
    persistAuthSession(buildLogin('token-a', 'user-a'))

    const oldRequest = store.loadList()
    clearAuthSession()
    store.clearAuthenticatedState()
    persistAuthSession(buildLogin('token-b', 'user-b'))
    await store.loadList()
    finishOld?.(buildListResponse(buildListItem('old', '旧账号地址')))
    await oldRequest

    expect(store.helpList.map((item) => item.id)).toEqual(['new'])
  })

  it('ignores a late detail response after logout and login as account B', async () => {
    let finishOld: ((value: HelpRequestDetail) => void) | undefined
    apiMocks.fetchHelpDetail.mockReset()
    apiMocks.fetchHelpDetail
      .mockReturnValueOnce(new Promise((resolve) => {
        finishOld = resolve
      }))
      .mockResolvedValueOnce({ ...helpDetail, id: 'new', address: '新账号地址' })
    const store = useHelpStore()
    persistAuthSession(buildLogin('token-a', 'user-a'))

    const oldRequest = store.loadDetail('old')
    clearAuthSession()
    store.clearAuthenticatedState()
    persistAuthSession(buildLogin('token-b', 'user-b'))
    await store.loadDetail('new')
    finishOld?.({ ...helpDetail, id: 'old', address: '旧账号地址' })
    await oldRequest

    expect(store.currentRequest?.id).toBe('new')
    expect(store.currentRequest?.address).toBe('新账号地址')
  })
})

function buildListResponse(item: HelpListItem) {
  return {
    items: [item],
    total: 1,
    page: 1,
    pageSize: 20,
    totalPages: 1,
  }
}

function buildLogin(token: string, id: string): LoginResponse {
  return {
    token,
    expiresIn: 2592000,
    userInfo: { id, name: id, avatarUrl: '', isNewUser: false },
  }
}
