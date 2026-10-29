import { describe, it, expect } from 'vitest'
import {
  maskPhone,
  maskIdNumber,
  formatDistance,
  formatDate,
  formatChineseDateTime,
  formatRelativeTime,
  formatMessageTime,
} from '@/utils/format'

describe('maskPhone', () => {
  it('masks middle 4 digits of 11-digit phone', () => {
    expect(maskPhone('13512345678')).toBe('135****5678')
  })

  it('returns original if too short', () => {
    expect(maskPhone('12345')).toBe('12345')
  })

  it('handles empty string', () => {
    expect(maskPhone('')).toBe('')
  })
})

describe('maskIdNumber', () => {
  it('masks middle digits of id number', () => {
    expect(maskIdNumber('469027200410123456')).toBe('469027******3456')
  })

  it('returns original if too short', () => {
    expect(maskIdNumber('1234567')).toBe('1234567')
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

describe('formatChineseDateTime', () => {
  it('formats Date objects without English weekday text', () => {
    const result = formatChineseDateTime(new Date(2026, 4, 21, 23, 29, 10))

    expect(result).toBe('2026年05月21日 23:29')
  })

  it('formats ISO values with Chinese date units', () => {
    const result = formatChineseDateTime('2026-05-21T15:29:10.000Z')

    expect(result).toMatch(/^\d{4}年\d{2}月\d{2}日 \d{2}:\d{2}$/)
    expect(result).not.toMatch(/Thu|May|GMT|CST/)
  })

  it('returns empty string for invalid values', () => {
    expect(formatChineseDateTime('not-a-date')).toBe('')
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

describe('formatMessageTime', () => {
  const now = new Date(2026, 4, 21, 12, 0, 0)

  it('formats same day as HH:mm:ss', () => {
    expect(formatMessageTime(new Date(2026, 4, 21, 9, 5, 7), now)).toBe('09:05:07')
  })

  it('formats yesterday with prefix', () => {
    expect(formatMessageTime(new Date(2026, 4, 20, 23, 5, 7), now)).toBe('昨天 23:05:07')
  })

  it('formats same year as MM-DD HH:mm:ss', () => {
    expect(formatMessageTime(new Date(2026, 3, 15, 9, 5, 7), now)).toBe('04-15 09:05:07')
  })

  it('formats other years with full date', () => {
    expect(formatMessageTime(new Date(2025, 11, 31, 9, 5, 7), now)).toBe('2025-12-31 09:05:07')
  })

  it('returns empty string for invalid values', () => {
    expect(formatMessageTime('not-a-date', now)).toBe('')
  })
})
