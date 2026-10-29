import { afterEach, describe, it, expect, vi } from 'vitest'
import { MAP_CENTER, CAMPUSES } from '@/config/site-config'
import { DEMO_AED_DEVICES } from '@/mock/generated/demo-aed-devices.generated'
import {
  SYNTHETIC_LOCATION_NOTICE,
  calculateDistance,
  chooseLocationPoint,
  getCurrentLocation,
  isWechatDevtoolsPlatform,
  shouldUseManualLocationPicker,
} from '@/services/geo-service'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('synthetic location (mock / local-demo modes)', () => {
  it('returns a config-center synthetic point with a visible notice', async () => {
    const result = await getCurrentLocation()

    expect(result.success).toBe(false)
    expect(result.isSynthetic).toBe(true)
    expect(result.isDefault).toBe(true)
    expect(result.showSystemLocation).toBe(false)
    expect(result.point).toEqual(MAP_CENTER)
    expect(result.errorMessage).toBe(SYNTHETIC_LOCATION_NOTICE)
    expect(result.errorMessage).toContain('合成位置')
  })

  it('never reports real GPS even for interactive location mode', async () => {
    const getLocation = vi.fn()
    vi.stubGlobal('uni', { getLocation })

    const result = await getCurrentLocation({ mode: 'interactive' })

    expect(result.isSynthetic).toBe(true)
    expect(getLocation).not.toHaveBeenCalled()
  })
})

describe('devtools location routing', () => {
  it('recognizes WeChat devtools platform explicitly', () => {
    expect(isWechatDevtoolsPlatform('devtools')).toBe(true)
    expect(isWechatDevtoolsPlatform('windows')).toBe(false)
  })

  it('uses manual picker only for interactive actions in devtools', () => {
    expect(shouldUseManualLocationPicker('devtools', 'interactive')).toBe(true)
    expect(shouldUseManualLocationPicker('devtools', 'auto')).toBe(false)
    expect(shouldUseManualLocationPicker('windows', 'interactive')).toBe(false)
  })
})

describe('chooseLocationPoint', () => {
  it('opens the map at the configured map center by default', async () => {
    const chooseLocation = vi.fn().mockResolvedValue({
      latitude: 31.2315,
      longitude: 121.4785,
      name: '演示选点',
      address: '示范园区（合成点位）',
    })
    vi.stubGlobal('uni', { chooseLocation })

    const result = await chooseLocationPoint()

    expect(chooseLocation).toHaveBeenCalledWith({
      latitude: MAP_CENTER.lat,
      longitude: MAP_CENTER.lng,
    })
    expect(result).toMatchObject({
      success: true,
      point: { lat: 31.2315, lng: 121.4785 },
      address: '示范园区（合成点位）',
      name: '演示选点',
      isSynthetic: false,
    })
  })

  it('uses the provided center coordinate for initial map view', async () => {
    const chooseLocation = vi.fn().mockResolvedValue({
      latitude: 31.24,
      longitude: 121.5,
      name: '演示楼',
      address: '示范园区东区演示楼',
    })
    vi.stubGlobal('uni', { chooseLocation })

    const result = await chooseLocationPoint({ lat: 31.24, lng: 121.5 })

    expect(chooseLocation).toHaveBeenCalledWith({
      latitude: 31.24,
      longitude: 121.5,
    })
    expect(result).toMatchObject({
      success: true,
      point: { lat: 31.24, lng: 121.5 },
      name: '演示楼',
    })
  })

  it('surfaces a cancelled pick without pretending success', async () => {
    const chooseLocation = vi.fn().mockRejectedValue({ errMsg: 'chooseLocation:fail cancel' })
    vi.stubGlobal('uni', { chooseLocation })

    const result = await chooseLocationPoint()

    expect(result.success).toBe(false)
    expect(result.failureReason).toBe('cancelled')
    expect(result.errorMessage).toBe('未选择位置，请重试')
  })
})

describe('calculateDistance (Haversine)', () => {
  it('returns 0 for same point', () => {
    expect(calculateDistance(MAP_CENTER, MAP_CENTER)).toBe(0)
  })

  it('calculates a plausible distance between two demo AED devices', () => {
    const first = DEMO_AED_DEVICES[0]
    const second = DEMO_AED_DEVICES[1]
    const distance = calculateDistance(
      { lat: first.lat, lng: first.lng },
      { lat: second.lat, lng: second.lng },
    )

    expect(distance).toBeGreaterThan(150)
    expect(distance).toBeLessThan(500)
  })

  it('roughly matches the configured distance between map center and east campus', () => {
    const distance = calculateDistance(MAP_CENTER, CAMPUSES[0].center)

    expect(distance).toBeGreaterThan(2000)
    expect(distance).toBeLessThan(4000)
  })

  it('is symmetric', () => {
    const first = DEMO_AED_DEVICES[0]
    const second = DEMO_AED_DEVICES[1]
    const a = { lat: first.lat, lng: first.lng }
    const b = { lat: second.lat, lng: second.lng }

    expect(calculateDistance(a, b)).toBeCloseTo(calculateDistance(b, a), 2)
  })
})
