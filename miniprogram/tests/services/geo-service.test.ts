import { afterEach, describe, it, expect, vi } from 'vitest'
import {
  calculateDistance,
  chooseLocationPoint,
  getCurrentLocation,
  isWechatDevtoolsPlatform,
  shouldSkipInitialRealLocation,
  shouldUseManualLocationPicker,
  shouldUseDefaultLocation,
} from '@/services/geo-service'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('shouldUseDefaultLocation', () => {
  it('uses campus center in preview mock mode when real location is disabled', () => {
    expect(shouldUseDefaultLocation('preview', true, false)).toBe(true)
  })

  it('allows real location in preview mock mode when enabled explicitly', () => {
    expect(shouldUseDefaultLocation('preview', true, true)).toBe(false)
  })

  it('does not force default location outside preview mock mode', () => {
    expect(shouldUseDefaultLocation('development', true, false)).toBe(false)
    expect(shouldUseDefaultLocation('preview', false, false)).toBe(false)
    expect(shouldUseDefaultLocation('production', false, false)).toBe(false)
  })
})

describe('shouldSkipInitialRealLocation', () => {
  it('skips initial real location only in preview mock mode when auto locate is disabled', () => {
    expect(shouldSkipInitialRealLocation('preview', true, true, false)).toBe(true)
  })

  it('does not skip initial real location when auto locate is enabled', () => {
    expect(shouldSkipInitialRealLocation('preview', true, true, true)).toBe(false)
  })

  it('does not skip initial real location outside preview real-location mode', () => {
    expect(shouldSkipInitialRealLocation('development', true, true, false)).toBe(false)
    expect(shouldSkipInitialRealLocation('preview', true, false, false)).toBe(false)
    expect(shouldSkipInitialRealLocation('preview', false, true, false)).toBe(false)
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
  it('returns the selected map point and place text', async () => {
    const chooseLocation = vi.fn().mockResolvedValue({
      latitude: 19.976547,
      longitude: 110.51346,
      name: '理科楼',
      address: 'REDACTED',
    })
    vi.stubGlobal('uni', { chooseLocation })

    const result = await chooseLocationPoint()

    expect(chooseLocation).toHaveBeenCalledWith({
      latitude: 19.976547,
      longitude: 110.51346,
    })
    expect(result).toMatchObject({
      success: true,
      point: { lat: 19.976547, lng: 110.51346 },
      address: 'REDACTED',
      name: '理科楼',
    })
  })

  it('uses the provided center coordinate for initial map view', async () => {
    const chooseLocation = vi.fn().mockResolvedValue({
      latitude: 19.98,
      longitude: 110.52,
      name: '三号宿舍楼',
      address: 'REDACTED三号宿舍楼',
    })
    vi.stubGlobal('uni', { chooseLocation })

    const result = await chooseLocationPoint({ lat: 19.98, lng: 110.52 })

    expect(chooseLocation).toHaveBeenCalledWith({
      latitude: 19.98,
      longitude: 110.52,
    })
    expect(result).toMatchObject({
      success: true,
      point: { lat: 19.98, lng: 110.52 },
      name: '三号宿舍楼',
    })
  })
})

describe('location permission handling', () => {
  it('does not claim that every caller applied the campus fallback', async () => {
    vi.stubGlobal('uni', {
      getDeviceInfo: () => ({ platform: 'windows' }),
      getSetting: vi.fn().mockResolvedValue({ authSetting: { 'scope.userLocation': false } }),
      authorize: vi.fn(),
      showModal: vi.fn(),
      openSetting: vi.fn(),
    })

    const result = await getCurrentLocation()

    expect(result.errorMessage).toBe('定位权限被拒绝，请开启定位权限后重试')
    expect(result.errorMessage).not.toContain('已使用校园中心点')
  })

  it('shows the settings guide at most once after a persisted denial', async () => {
    const showModal = vi.fn()
    const authorize = vi.fn()
    vi.stubGlobal('uni', {
      getDeviceInfo: () => ({ platform: 'windows' }),
      getSetting: vi.fn().mockResolvedValue({ authSetting: { 'scope.userLocation': false } }),
      authorize,
      showModal,
      openSetting: vi.fn(),
    })

    await getCurrentLocation({ mode: 'interactive' })
    await getCurrentLocation({ mode: 'interactive' })

    expect(authorize).not.toHaveBeenCalled()
    expect(showModal).toHaveBeenCalledTimes(1)
  })

  it('does not log exact coordinates after a successful location request', async () => {
    const logSpy = vi.spyOn(console, 'log').mockImplementation(() => undefined)
    vi.stubGlobal('uni', {
      getDeviceInfo: () => ({ platform: 'windows' }),
      getSetting: vi.fn().mockResolvedValue({ authSetting: { 'scope.userLocation': true } }),
      getLocation: vi.fn().mockResolvedValue({ latitude: 19.97, longitude: 110.51 }),
    })

    const result = await getCurrentLocation()

    expect(result.success).toBe(true)
    expect(logSpy).not.toHaveBeenCalled()
  })
})

describe('calculateDistance (Haversine)', () => {
  it('returns 0 for same point', () => {
    const d = calculateDistance(
      { lat: 19.976547, lng: 110.51346 },
      { lat: 19.976547, lng: 110.51346 },
    )
    expect(d).toBe(0)
  })

  it('calculates distance between two campus AEDs', () => {
    // AED-01 to AED-02 (roughly 140m)
    const d = calculateDistance(
      { lat: 19.974447, lng: 110.517041 },
      { lat: 19.975735, lng: 110.517149 },
    )
    expect(d).toBeGreaterThan(100)
    expect(d).toBeLessThan(200)
  })

  it('calculates reasonable distance for campus diagonal', () => {
    // AED-09 (south) to AED-12 (north-east), roughly 300m
    const d = calculateDistance(
      { lat: 19.972782, lng: 110.517886 },
      { lat: 19.975272, lng: 110.518919 },
    )
    expect(d).toBeGreaterThan(250)
    expect(d).toBeLessThan(400)
  })

  it('is symmetric', () => {
    const a = { lat: 19.974447, lng: 110.517041 }
    const b = { lat: 19.975735, lng: 110.517149 }
    expect(calculateDistance(a, b)).toBeCloseTo(calculateDistance(b, a), 2)
  })
})
