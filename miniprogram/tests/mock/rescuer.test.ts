import { beforeEach, describe, expect, it, vi } from 'vitest'
import { CAMPUSES, MAP_CENTER, SERVICE_RADIUS_METERS } from '@/config/site-config'
import { DEMO_RESCUER_PROFILES } from '@/mock/generated/demo-rescuer-profiles.generated'
import { setActiveDemoRole, resetActiveDemoRole } from '@/mock/demo-role'
import {
  __resetMockRescuerState,
  __setMockRescuerNow,
  getMockMyRescuerAvailability,
  getMockRescuerDetail,
  queryMockNearbyRescuers,
  updateMockMyRescuerAvailability,
} from '@/mock/rescuer'
import { MOCK_RESCUERS } from '@/mock/rescuer-fixtures'

const storage = new Map<string, string>()

vi.stubGlobal('uni', {
  getStorageSync: vi.fn((key: string) => storage.get(key) ?? ''),
  setStorageSync: vi.fn((key: string, value: string) => {
    storage.set(key, value)
  }),
  removeStorageSync: vi.fn((key: string) => {
    storage.delete(key)
  }),
})

const EAST = CAMPUSES[0]
const WEST = CAMPUSES[1]
const ACTIVE_RESCUER_PROFILE_ID = 'DEMO-RSP-001'

function queryAt(campus: { center: { lat: number; lng: number } }) {
  return queryMockNearbyRescuers({ lat: campus.center.lat, lng: campus.center.lng })
}

describe('mock rescuers (demo-v1 synthetic profiles)', () => {
  beforeEach(() => {
    storage.clear()
    resetActiveDemoRole()
    __resetMockRescuerState()
  })

  it('projects every demo profile one-to-one without inventing extra records', () => {
    expect(MOCK_RESCUERS).toHaveLength(DEMO_RESCUER_PROFILES.length)
    expect(MOCK_RESCUERS.map((item) => item.profileId)).toEqual(
      DEMO_RESCUER_PROFILES.map((profile) => profile.id),
    )
  })

  it('filters list by nearest configured campus and sorts online first', () => {
    const result = queryAt(EAST)

    expect(result.campus).toBe(EAST.code)
    expect(result.campusName).toBe(EAST.name)
    expect(result.items.every((item) => item.campus === EAST.code)).toBe(true)
    expect(result.items[0]?.availabilityStatus).toBe('online')
    expect(result.items.at(-1)?.availabilityStatus).toBe('offline')
    expect(result.autoOfflineRadius).toBe(SERVICE_RADIUS_METERS)
  })

  it('resolves the viewer campus by config order, not by a hardcoded pair', () => {
    for (const campus of CAMPUSES) {
      const result = queryAt(campus)
      expect(result.campus).toBe(campus.code)
      expect(result.viewerCampusDistance).toBe(0)
    }
  })

  it('returns full contact in rescuer detail for login-only scene', () => {
    const detail = getMockRescuerDetail(`rescuer-${ACTIVE_RESCUER_PROFILE_ID}`)

    expect(detail?.name).toBe('演示乙（急救员·东区）')
    expect(detail?.phone).toBe('13000000002')
    expect(detail?.phoneMasked).toBe('130****0002')
    expect(detail?.skills.stb).toBe(true)
  })

  it('derives skill flags from demo profile booleans', () => {
    MOCK_RESCUERS.forEach((record, index) => {
      const profile = DEMO_RESCUER_PROFILES[index]
      expect(record.skills).toEqual({
        cpr: profile.skillCpr,
        aed: profile.skillAed,
        stb: profile.skillStb,
      })
    })
  })

  it('uses the demo campus field instead of guessing from training text', () => {
    MOCK_RESCUERS.forEach((record, index) => {
      expect(record.campus).toBe(DEMO_RESCUER_PROFILES[index].campus)
    })
  })

  it('returns empty state list when viewer is outside every campus service radius', () => {
    const result = queryMockNearbyRescuers({ lat: 0, lng: 0 })

    expect(result.viewerCampusDistance).toBeGreaterThan(SERVICE_RADIUS_METERS)
    expect(result.items).toHaveLength(0)
  })

  it('keeps availability runtime status separate from qualification status', () => {
    // DEMO-RSP-004 资格有效但距校区中心 36 km，应自动离线
    const detail = getMockRescuerDetail('rescuer-DEMO-RSP-004')

    expect(detail?.qualificationStatus).toBe('active')
    expect(detail?.availabilityStatus).toBe('offline')
    expect(detail?.campus).toBe('east')
  })

  it('matches campus split and online-offline counts of the demo data', () => {
    const east = queryAt(EAST)
    const west = queryAt(WEST)
    const countStatus = (items: typeof east.items, status: 'online' | 'offline') =>
      items.filter((item) => item.availabilityStatus === status).length

    // 东区有效资格档案：001/003/004/005/007（002 资格失效被过滤）
    expect(east.items).toHaveLength(5)
    expect(countStatus(east.items, 'online')).toBe(4)
    expect(countStatus(east.items, 'offline')).toBe(1)

    // 西区只有 DEMO-RSP-006
    expect(west.items).toHaveLength(1)
    expect(west.items[0]?.id).toBe('rescuer-DEMO-RSP-006')
    expect(west.items[0]?.availabilityStatus).toBe('online')
  })

  it('supports manual offline window within 10 hours for the current rescuer', () => {
    setActiveDemoRole('demo-rescuer')
    __setMockRescuerNow('2026-10-26T10:00:00.000Z')

    const updated = updateMockMyRescuerAvailability({
      manualOfflineUntil: '2026-10-26T18:00:00.000Z',
    })

    expect(updated.availabilityStatus).toBe('offline')
    expect(updated.manualOfflineUntil).toBe('2026-10-26T18:00:00.000Z')
  })

  it('rejects manual offline window longer than 10 hours', () => {
    setActiveDemoRole('demo-rescuer')
    __setMockRescuerNow('2026-10-26T10:00:00.000Z')

    expect(() =>
      updateMockMyRescuerAvailability({
        manualOfflineUntil: '2026-10-26T21:00:00.000Z',
      }),
    ).toThrow('单次离线时长不能超过 10 小时')
  })

  it('auto recovers to online after the manual offline window expires', () => {
    setActiveDemoRole('demo-rescuer')
    __setMockRescuerNow('2026-10-26T10:00:00.000Z')
    updateMockMyRescuerAvailability({
      manualOfflineUntil: '2026-10-26T12:00:00.000Z',
    })

    __setMockRescuerNow('2026-10-26T13:00:00.000Z')
    const availability = getMockMyRescuerAvailability()

    expect(availability.manualOfflineUntil).toBe('2026-10-26T12:00:00.000Z')
    expect(availability.availabilityStatus).toBe('online')
  })

  it('reports unavailable availability settings for non-rescuer demo roles', () => {
    setActiveDemoRole('demo-requester')

    expect(() => getMockMyRescuerAvailability()).toThrow('当前演示角色不是急救员')
  })

  it('exposes the demo map center from config for mock defaults', () => {
    expect(MAP_CENTER.lat).toBe(31.2315)
    expect(MAP_CENTER.lng).toBe(121.4785)
  })
})
