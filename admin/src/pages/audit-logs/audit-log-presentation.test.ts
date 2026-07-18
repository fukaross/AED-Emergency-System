import { describe, expect, it } from 'vitest'
import {
  formatJson,
  formatTimeSeconds,
  getEventNameCn,
  getEventTypeLabel,
  getEventTypeStyle,
  maskIp,
} from './audit-log-presentation'

describe('audit-log-presentation', () => {
  it('maps known audit events and preserves unknown contract values', () => {
    expect(getEventNameCn('help.close')).toBe('关闭求助')
    expect(getEventNameCn('future.event')).toBe('future.event')
    expect(getEventTypeLabel('admin')).toBe('管理')
    expect(getEventTypeLabel('future')).toBe('future')
    expect(getEventTypeStyle('help')).toEqual({
      background: '#fdf6ec',
      color: '#e6a23c',
      border: '1px solid #fdf6ec',
    })
    expect(getEventTypeStyle('future').color).toBe('#909399')
  })

  it('formats valid timestamps and exposes invalid external values', () => {
    const value = new Date(2026, 6, 18, 9, 5, 7)
    expect(formatTimeSeconds(value.toISOString())).toBe('07-18 09:05:07')
    expect(formatTimeSeconds('invalid-date')).toBe('invalid-date')
    expect(formatTimeSeconds(null)).toBe('—')
  })

  it('masks IP display values without changing unrecognized values', () => {
    expect(maskIp('192.168.10.20')).toBe('192.168.*.*')
    expect(maskIp('2001:db8:1:2')).toBe('2001:db8:*:*')
    expect(maskIp('localhost')).toBe('localhost')
    expect(maskIp(null)).toBe('—')
  })

  it('omits duplicated changed fields without mutating metadata', () => {
    const metadata = { changedFields: ['status'], reason: 'done' }
    expect(formatJson(metadata)).toBe('{\n  "reason": "done"\n}')
    expect(metadata.changedFields).toEqual(['status'])
    expect(formatJson({ changedFields: [] })).toBe('无额外属性')
    expect(formatJson(null)).toBe('无附加信息')
  })
})
