import { beforeEach, describe, expect, it } from 'vitest'
import {
  __resetMockRescuerState,
  __setMockRescuerNow,
  getMockMyRescuerAvailability,
  getMockRescuerDetail,
  queryMockNearbyRescuers,
  updateMockMyRescuerAvailability,
} from '@/mock/rescuer'
import { MOCK_RESCUERS } from '@/mock/rescuer-fixtures'
import {
  FEATURED_MOCK_RESCUER_ID,
  resolveCampusCodeFromSource,
} from '@/mock/rescuer-qualification-fixtures'
import { SOURCE_RESCUER_ARCHIVE_ROWS } from '@/mock/rescuer-source-rows'

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

    expect(detail?.name).toBe('REDACTED')
    expect(detail?.phone).toBe('REDACTED')
    expect(detail?.phoneMasked).toBe('182****7261')
    expect(detail?.skills.stb).toBe(true)
  })

  it('pins the featured new rescuer online in Guilinyang campus', () => {
    const result = queryMockNearbyRescuers({
      lat: 19.9743,
      lng: 110.5178,
    })
    const detail = getMockRescuerDetail(FEATURED_MOCK_RESCUER_ID)

    expect(result.items[0]?.id).toBe(FEATURED_MOCK_RESCUER_ID)
    expect(result.items[0]?.name).toBe('REDACTED')
    expect(result.items[0]?.level).toBe('honor')
    expect(result.items[0]?.availabilityStatus).toBe('online')
    expect(detail?.campus).toBe('guilinyang')
  })

  it('derives skill flags from source assessment results', () => {
    expect(MOCK_RESCUERS).toHaveLength(SOURCE_RESCUER_ARCHIVE_ROWS.length)

    MOCK_RESCUERS.forEach((record, index) => {
      const row = SOURCE_RESCUER_ARCHIVE_ROWS[index]
      const cprAedPassed = row.cprAedCombinedResult === '合格'

      expect(record.skills).toEqual({
        cpr: row.cprAssessmentResult === '合格' || cprAedPassed,
        aed: row.aedAssessmentResult === '合格' || cprAedPassed,
        stb: row.traumaAssessmentResult === '合格',
      })
    })
  })

  it('derives campus from source training session instead of array position', () => {
    MOCK_RESCUERS.forEach((record, index) => {
      expect(record.campus).toBe(
        resolveCampusCodeFromSource(SOURCE_RESCUER_ARCHIVE_ROWS[index]),
      )
    })
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
    const detail = getMockRescuerDetail('rescuer-025')

    expect(detail?.qualificationStatus).toBe('active')
    expect(detail?.availabilityStatus).toBe('offline')
  })

  it('matches campus split and online-offline ratio after featured override', () => {
    const guilinyang = queryMockNearbyRescuers({
      lat: 19.9743,
      lng: 110.5178,
    })
    const fucheng = queryMockNearbyRescuers({
      lat: 19.9988,
      lng: 110.358,
    })

    const guilinyangOnline = guilinyang.items.filter(
      (item) => item.availabilityStatus === 'online',
    ).length
    const guilinyangOffline = guilinyang.items.filter(
      (item) => item.availabilityStatus === 'offline',
    ).length
    const fuchengOnline = fucheng.items.filter(
      (item) => item.availabilityStatus === 'online',
    ).length
    const fuchengOffline = fucheng.items.filter(
      (item) => item.availabilityStatus === 'offline',
    ).length

    expect(guilinyang.items).toHaveLength(22)
    expect(fucheng.items).toHaveLength(9)
    expect(guilinyangOnline).toBe(17)
    expect(guilinyangOffline).toBe(5)
    expect(fuchengOnline).toBe(8)
    expect(fuchengOffline).toBe(1)
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
