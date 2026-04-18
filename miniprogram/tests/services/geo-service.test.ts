import { describe, it, expect } from 'vitest'
import {
  calculateDistance,
  isWechatDevtoolsPlatform,
  shouldSkipInitialRealLocation,
  shouldUseManualLocationPicker,
  shouldUseDefaultLocation,
} from '@/services/geo-service'

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

describe('calculateDistance (Haversine)', () => {
  it('returns 0 for same point', () => {
    const d = calculateDistance(
      { lat: 19.976547, lng: 110.513460 },
      { lat: 19.976547, lng: 110.513460 },
    )
    expect(d).toBe(0)
  })

  it('calculates distance between two campus AEDs', () => {
    // AED-01 to AED-02 (roughly 140m)
    const d = calculateDistance(
      { lat: 19.976722, lng: 110.513617 },
      { lat: 19.977875, lng: 110.512867 },
    )
    expect(d).toBeGreaterThan(100)
    expect(d).toBeLessThan(200)
  })

  it('calculates reasonable distance for campus diagonal', () => {
    // AED-09 (south) to AED-12 (north-east), roughly 300m
    const d = calculateDistance(
      { lat: 19.974922, lng: 110.513606 },
      { lat: 19.977417, lng: 110.514642 },
    )
    expect(d).toBeGreaterThan(250)
    expect(d).toBeLessThan(400)
  })

  it('is symmetric', () => {
    const a = { lat: 19.976722, lng: 110.513617 }
    const b = { lat: 19.977875, lng: 110.512867 }
    expect(calculateDistance(a, b)).toBeCloseTo(calculateDistance(b, a), 2)
  })
})
