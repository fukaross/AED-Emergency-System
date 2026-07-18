import { describe, expect, it } from 'vitest'
import { formatCompactDateTime, formatDateTime, formatTimeOfDay } from './date-time'

describe('formatCompactDateTime', () => {
  it('formats valid values and preserves invalid external values', () => {
    const value = new Date(2026, 6, 18, 9, 5, 0)
    expect(formatCompactDateTime(value)).toBe('07-18 09:05')
    expect(formatCompactDateTime('invalid-date')).toBe('invalid-date')
    expect(formatCompactDateTime(null)).toBe('')
  })

  it('shares full and time-only formatting without hiding invalid values', () => {
    const value = new Date(2026, 6, 18, 9, 5, 7)
    expect(formatDateTime(value)).toBe('2026-07-18 09:05:07')
    expect(formatTimeOfDay(value)).toBe('09:05')
    expect(formatTimeOfDay(value, true)).toBe('09:05:07')
    expect(formatDateTime('invalid-date')).toBe('invalid-date')
  })
})
