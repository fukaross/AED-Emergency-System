import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@/api/rescuer', () => ({
  fetchNearbyRescuers: vi.fn(),
  fetchRescuerDetail: vi.fn(),
}))

vi.stubGlobal('uni', {
  showToast: vi.fn(),
})

import { fetchNearbyRescuers, fetchRescuerDetail } from '@/api/rescuer'
import { useRescuerStore } from '@/store/rescuer'

describe('rescuer store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('loads detail silently without mutating modal state', async () => {
    vi.mocked(fetchRescuerDetail).mockResolvedValue({
      id: 'rescuer-001',
      surname: '周',
      avatarUrl: '/static/images/avatar-default.png',
      campus: 'guilinyang',
      campusName: 'REDACTED校区',
      level: 'core',
      availabilityStatus: 'online',
      qualificationStatus: 'active',
      phone: 'REDACTED',
      phoneMasked: '135****6789',
      certificateNoMasked: '4600****0138',
      workUnit: '教育学院',
      skills: { cpr: true, aed: true, stb: true },
    })

    const store = useRescuerStore()
    const detail = await store.loadRescuerDetail('rescuer-001')

    expect(detail.phone).toBe('REDACTED')
    expect(store.showDetail).toBe(false)
    expect(store.currentDetail).toBeNull()
  })

  it('keeps detail during close animation until cleared', () => {
    const store = useRescuerStore()
    const detail = {
      id: 'rescuer-001',
      surname: '周',
      avatarUrl: '/static/images/avatar-default.png',
      campus: 'guilinyang',
      campusName: 'REDACTED校区',
      level: 'core',
      availabilityStatus: 'online',
      qualificationStatus: 'active',
      phone: 'REDACTED',
      phoneMasked: '135****6789',
      certificateNoMasked: '4600****0138',
      workUnit: '教育学院',
      skills: { cpr: true, aed: true, stb: true },
    }
    store.currentDetail = detail
    store.showDetail = true

    store.closeDetail()

    expect(store.showDetail).toBe(false)
    expect(store.currentDetail).toEqual(detail)

    store.clearDetail()

    expect(store.currentDetail).toBeNull()
  })

  it('stores load error when nearby rescuers request fails', async () => {
    vi.mocked(fetchNearbyRescuers).mockRejectedValue(new Error('network'))

    const store = useRescuerStore()

    await expect(store.loadNearbyRescuers(19.97, 110.51)).rejects.toThrow('network')
    expect(store.loadError).toBe('加载急救员失败，请稍后重试')
  })
})
