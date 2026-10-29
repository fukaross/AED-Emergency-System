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
  USER_BACKEND_READY: false,
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
      campus: 'east',
      campusName: '东区',
      viewerCampusDistance: 0,
      autoOfflineRadius: 30000,
      items: [],
    }
    requestMocks.get.mockResolvedValueOnce(response)

    const result = await fetchNearbyRescuers({ lat: 31.23, lng: 121.47 })

    expect(result).toBe(response)
    expect(requestMocks.get).toHaveBeenCalledWith(
      '/api/rescuers/nearby',
      { lat: 31.23, lng: 121.47 },
    )
    expect(rescuerMock.queryMockNearbyRescuers).not.toHaveBeenCalled()
  })

  it('uses real rescuer detail endpoint when rescuer backend is ready', async () => {
    const response = {
      id: 'rescuer-DEMO-RSP-001',
      name: '演示乙（急救员·东区）',
      avatarUrl: 'https://api.example.test/uploads/rescuer/avatar.jpg',
      campus: 'east',
      campusName: '东区',
      level: 'trainee',
      availabilityStatus: 'online',
      qualificationStatus: 'active',
      phone: '13900000001',
      phoneMasked: '139****0001',
      certificateNoMasked: 'TEST****0001',
      workUnit: null,
      skills: { cpr: true, aed: true, stb: true },
    }
    requestMocks.get.mockResolvedValueOnce(response)

    const result = await fetchRescuerDetail('rescuer-DEMO-RSP-001')

    expect(result.avatarUrl).toBe(response.avatarUrl)
    expect(requestMocks.get).toHaveBeenCalledWith('/api/rescuers/rescuer-DEMO-RSP-001')
    expect(rescuerMock.getMockRescuerDetail).not.toHaveBeenCalled()
  })
})
