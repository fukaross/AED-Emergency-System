import { beforeEach, describe, expect, it, vi } from 'vitest'

const requestMocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}))

vi.mock('@/utils/runtime-env', () => ({
  USE_LOCAL_HELP_READ_MODEL: false,
  USER_BACKEND_READY: false,
  USE_MOCK: false,
  USE_REAL_WECHAT_FLOW: true,
}))

vi.mock('@/services/request', () => requestMocks)

vi.mock('@/mock/help', () => ({
  closeMockGuestHelpRequest: vi.fn(),
  closeMockHelpRequest: vi.fn(),
  getMockHelpDetail: vi.fn(),
  listMockHelpRequests: vi.fn(),
  publishMockHelpRequest: vi.fn(),
  queryMockGuestHelp: vi.fn(),
  respondMockHelpRequest: vi.fn(),
  submitMockHelpReport: vi.fn(),
}))

vi.mock('@/mock/help-storage', () => ({
  cachePublishedHelpRecord: vi.fn(),
}))

import { fetchHelpDetail, fetchHelpList } from '@/api/help'

describe('help api silent reads', () => {
  beforeEach(() => {
    requestMocks.get.mockReset()
  })

  it('passes skipErrorHandler for silent help list reads', async () => {
    requestMocks.get.mockResolvedValueOnce({
      items: [],
      total: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0,
    })

    await fetchHelpList(1, 20, { silent: true })

    expect(requestMocks.get).toHaveBeenCalledWith(
      '/api/help/list',
      { page: 1, pageSize: 20 },
      { skipErrorHandler: true },
    )
  })

  it('keeps normal help list reads user-visible', async () => {
    requestMocks.get.mockResolvedValueOnce({
      items: [],
      total: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0,
    })

    await fetchHelpList(1, 20)

    expect(requestMocks.get).toHaveBeenCalledWith(
      '/api/help/list',
      { page: 1, pageSize: 20 },
      { skipErrorHandler: undefined },
    )
  })

  it('passes skipErrorHandler for silent help detail reads', async () => {
    requestMocks.get.mockResolvedValueOnce({})

    await fetchHelpDetail('help-1', { silent: true })

    expect(requestMocks.get).toHaveBeenCalledWith(
      '/api/help/help-1',
      undefined,
      { skipErrorHandler: true },
    )
  })
})
