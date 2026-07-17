import { describe, expect, it } from 'vitest'
import { computeSignal } from '@/utils/rescue-signal'

describe('rescue-signal computeSignal', () => {
  const now = Date.parse('2026-07-17T10:00:00.000Z')
  const at = (secondsAgo: number) => new Date(now - secondsAgo * 1000).toISOString()

  it('returns ok within the lost threshold (inclusive 45s)', () => {
    expect(computeSignal(at(0), now)).toBe('ok')
    expect(computeSignal(at(44), now)).toBe('ok')
    expect(computeSignal(at(45), now)).toBe('ok')
  })

  it('returns lost between lost and gone thresholds (inclusive 90s)', () => {
    expect(computeSignal(at(46), now)).toBe('lost')
    expect(computeSignal(at(89), now)).toBe('lost')
    expect(computeSignal(at(90), now)).toBe('lost')
  })

  it('returns gone beyond the gone threshold', () => {
    expect(computeSignal(at(91), now)).toBe('gone')
    expect(computeSignal(at(3600), now)).toBe('gone')
  })

  it('honors custom thresholds', () => {
    expect(computeSignal(at(11), now, 10, 20)).toBe('lost')
    expect(computeSignal(at(21), now, 10, 20)).toBe('gone')
  })
})
