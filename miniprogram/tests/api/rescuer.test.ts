import { beforeEach, describe, expect, it, vi } from 'vitest'

const requestMocks = vi.hoisted(() => ({
  get: vi.fn(),
  put: vi.fn(),
}))

const rescuerMock = vi.hoisted(() => ({
  getMockMyRescuerAvailability: vi.fn(),
  getMockRescuerDetail: vi.fn(),
  queryMockNearbyRescuers: vi.fn(),
  updateMockMyRescuerAvailability: vi.fn(),
}))

vi.mock('@/utils/runtime-env', () => ({
  RESCUER_BACKEND_READY: true,
  USE_MOCK: true,
  USE_PUBLIC_DEMO_DATA: true,
}))

vi.mock('@/services/request', () => requestMocks)
vi.mock('@/mock/rescuer', () => rescuerMock)

import { fetchNearbyRescuers, fetchRescuerDetail } from '@/api/rescuer'

describe('rescuer api backend switch', () => {
  beforeEach(() => {
    requestMocks.get.mockReset()
    requestMocks.put.mockReset()
    rescuerMock.getMockRescuerDetail.mockReset()
    rescuerMock.queryMockNearbyRescuers.mockReset()
  })

  it('uses real nearby rescuer endpoint when rescuer backend is ready', async () => {
    const response = {
      campus: 'guilinyang',
      campusName: 'REDACTED校区',
      viewerCampusDistance: 0,
      autoOfflineRadius: 30000,
      items: [],
    }
    requestMocks.get.mockResolvedValueOnce(response)

    const result = await fetchNearbyRescuers({ lat: 19.97, lng: 110.51 })

    expect(result).toBe(response)
    expect(requestMocks.get).toHaveBeenCalledWith(
      '/api/rescuers/nearby',
      { lat: 19.97, lng: 110.51 },
    )
    expect(rescuerMock.queryMockNearbyRescuers).not.toHaveBeenCalled()
  })

  it('uses real rescuer detail endpoint when rescuer backend is ready', async () => {
    const response = {
      id: 'rescuer-001',
      name: 'REDACTED',
      avatarUrl: 'https://api.example.test/uploads/rescuer/avatar.jpg',
      campus: 'guilinyang',
      campusName: 'REDACTED校区',
      level: 'trainee',
      availabilityStatus: 'online',
      qualificationStatus: 'active',
      phone: 'REDACTED',
      phoneMasked: '139****0001',
      certificateNoMasked: 'TEST****0001',
      workUnit: null,
      skills: { cpr: true, aed: true, stb: true },
    }
    requestMocks.get.mockResolvedValueOnce(response)

    const result = await fetchRescuerDetail('rescuer-001')

    expect(result.avatarUrl).toBe(response.avatarUrl)
    expect(requestMocks.get).toHaveBeenCalledWith('/api/rescuers/rescuer-001')
    expect(rescuerMock.getMockRescuerDetail).not.toHaveBeenCalled()
  })
})
