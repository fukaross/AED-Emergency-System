import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { HelpRequestDetail } from '@/types/help'

const apiMocks = vi.hoisted(() => ({
  fetchHelpDetail: vi.fn(),
  fetchHelpList: vi.fn(),
  guestCloseHelp: vi.fn(),
  guestQueryHelp: vi.fn(),
  publishHelp: vi.fn(),
  submitHelpReport: vi.fn(),
}))

vi.mock('@/api/help', () => apiMocks)

vi.stubGlobal('uni', {
  getStorageSync: vi.fn(() => undefined),
  removeStorageSync: vi.fn(),
  setStorageSync: vi.fn(),
})

import { useHelpStore } from '@/store/help'

const helpDetail = {
  id: 'help-1',
  requestNo: 'HELP0001',
  status: 'published',
  lat: 19.9743,
  lng: 110.5178,
  address: 'REDACTED校区',
  content: '需要帮助',
  type: 'general',
  images: [],
  requesterPhone: 'REDACTED',
  responders: [],
  reports: [],
  timeline: [],
  createdAt: '2026-05-24T10:00:00.000Z',
} as HelpRequestDetail

describe('help store silent reads', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
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
})
