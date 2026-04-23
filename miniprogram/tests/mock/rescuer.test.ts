import { beforeEach, describe, expect, it } from 'vitest'
import {
  __resetMockRescuerState,
  __setMockRescuerNow,
  getMockMyRescuerAvailability,
  getMockRescuerDetail,
  queryMockNearbyRescuers,
  updateMockMyRescuerAvailability,
} from '@/mock/rescuer'

describe('mock rescuers', () => {
  beforeEach(() => {
    __resetMockRescuerState()
  })

  it('filters list by nearest campus and sorts online first', () => {
    const result = queryMockNearbyRescuers({
      lat: 19.9743,
      lng: 110.5178,
    })

    expect(result.campus).toBe('guilinyang')
    expect(result.items.every((item) => item.campus === 'guilinyang')).toBe(true)
    expect(result.items[0]?.availabilityStatus).toBe('online')
    expect(result.items.at(-1)?.availabilityStatus).toBe('offline')
  })

  it('returns full contact in rescuer detail for login-only scene', () => {
    const detail = getMockRescuerDetail('rescuer-001')

    expect(detail?.name).toBe('周雪')
    expect(detail?.phone).toBe('REDACTED')
    expect(detail?.phoneMasked).toBe('135****6789')
    expect(detail?.skills.stb).toBe(true)
  })

  it('returns empty state list when viewer is outside both campus service radii', () => {
    const result = queryMockNearbyRescuers({
      lat: 20.45,
      lng: 110.9,
    })

    expect(result.viewerCampusDistance).toBeGreaterThan(30000)
    expect(result.items).toHaveLength(0)
  })

  it('keeps availability runtime status separate from qualification status', () => {
    const detail = getMockRescuerDetail('rescuer-004')

    expect(detail?.qualificationStatus).toBe('inactive')
    expect(detail?.availabilityStatus).toBe('offline')
  })

  it('supports manual offline window within 10 hours', () => {
    __setMockRescuerNow('2026-04-22T10:00:00.000Z')

    const updated = updateMockMyRescuerAvailability({
      manualOfflineUntil: '2026-04-22T18:00:00.000Z',
    })

    expect(updated.availabilityStatus).toBe('offline')
    expect(updated.manualOfflineUntil).toBe('2026-04-22T18:00:00.000Z')
  })

  it('rejects manual offline window longer than 10 hours', () => {
    __setMockRescuerNow('2026-04-22T10:00:00.000Z')

    expect(() =>
      updateMockMyRescuerAvailability({
        manualOfflineUntil: '2026-04-22T21:00:00.000Z',
      }),
    ).toThrow('单次离线时长不能超过 10 小时')
  })

  it('auto recovers to online after manual offline expiry', () => {
    __setMockRescuerNow('2026-04-22T13:00:00.000Z')

    const availability = getMockMyRescuerAvailability()

    expect(availability.manualOfflineUntil).toBe('2026-04-22T12:00:00.000Z')
    expect(availability.availabilityStatus).toBe('online')
  })
})
