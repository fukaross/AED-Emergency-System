import { beforeEach, describe, expect, it, vi } from 'vitest'

const requestMocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}))

const userMock = vi.hoisted(() => ({
  loadMockUserProfile: vi.fn(),
  queryMockResponderClaims: vi.fn(),
  queryMockSchoolBindings: vi.fn(),
  saveMockResponderClaim: vi.fn(),
  saveMockSchoolBinding: vi.fn(),
  saveMockVolunteerApplication: vi.fn(),
  saveMockUserProfile: vi.fn(),
}))

vi.mock('@/utils/runtime-env', () => ({
  USER_BACKEND_READY: true,
  USE_MOCK: true,
}))

vi.mock('@/services/request', () => requestMocks)
vi.mock('@/mock/user', () => userMock)

import {
  fetchArchiveDetail,
  fetchArchivePhone,
  fetchArchives,
} from '@/api/user'

describe('user archive api', () => {
  beforeEach(() => {
    requestMocks.get.mockReset()
  })

  it('uses real archive list endpoint even when global mock is enabled', async () => {
    const response = {
      total: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
      canViewFullPhone: true,
      selfArchive: null,
      items: [],
    }
    requestMocks.get.mockResolvedValueOnce(response)

    const result = await fetchArchives({ keyword: '陈', page: 1, pageSize: 20 })

    expect(result).toBe(response)
    expect(requestMocks.get).toHaveBeenCalledWith('/api/user/archives', {
      keyword: '陈',
      page: 1,
      pageSize: 20,
    })
    expect(userMock.loadMockUserProfile).not.toHaveBeenCalled()
  })

  it('uses real archive detail endpoint', async () => {
    const response = { id: 'rescuer-DEMO-RSP-001', certificateRecords: [] }
    requestMocks.get.mockResolvedValueOnce(response)

    const result = await fetchArchiveDetail('rescuer-DEMO-RSP-001')

    expect(result).toBe(response)
    expect(requestMocks.get).toHaveBeenCalledWith('/api/user/archives/rescuer-DEMO-RSP-001')
  })

  it('uses real archive phone endpoint with purpose', async () => {
    const response = { phone: '13000000009' }
    requestMocks.get.mockResolvedValueOnce(response)

    const result = await fetchArchivePhone('rescuer-DEMO-RSP-001', 'emergency')

    expect(result).toBe(response)
    expect(requestMocks.get).toHaveBeenCalledWith(
      '/api/user/archives/rescuer-DEMO-RSP-001/phone',
      { purpose: 'emergency' },
    )
  })
})
