import { describe, it, expect } from 'vitest'
import { maskPhone, formatDistance, formatDate, formatRelativeTime } from '@/utils/format'

describe('maskPhone', () => {
  it('masks middle 4 digits of 11-digit phone', () => {
    expect(maskPhone('REDACTED')).toBe('135****5678')
  })

  it('returns original if too short', () => {
    expect(maskPhone('12345')).toBe('12345')
  })

  it('handles empty string', () => {
    expect(maskPhone('')).toBe('')
  })
})

describe('formatDistance', () => {
  it('formats meters below 1000', () => {
    expect(formatDistance(120)).toBe('120m')
  })

  it('formats kilometers above 1000', () => {
    expect(formatDistance(1500)).toBe('1.5km')
  })

  it('formats exact 1000', () => {
    expect(formatDistance(1000)).toBe('1.0km')
  })

  it('handles zero', () => {
    expect(formatDistance(0)).toBe('0m')
  })
})

describe('formatDate', () => {
  it('formats ISO date string', () => {
    const result = formatDate('2026-03-15T10:30:00Z')
    expect(result).toMatch(/2026-03-15/)
  })
})

describe('formatRelativeTime', () => {
  it('returns "刚刚" for recent time', () => {
    const now = new Date().toISOString()
    expect(formatRelativeTime(now)).toBe('刚刚')
  })

  it('returns minutes ago', () => {
    const fiveMinAgo = new Date(Date.now() - 5 * 60 * 1000).toISOString()
    expect(formatRelativeTime(fiveMinAgo)).toBe('5分钟前')
  })
})
