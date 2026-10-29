import { describe, expect, it } from 'vitest'
import {
  formatDateOnly,
  formatTime,
  getBindingStatusCn,
  getBindingTagType,
  getInitials,
  getLevelCn,
  getRoleCn,
  maskCertNo,
  maskPhone,
  truncateOpenId,
} from './user-detail-presentation'

describe('user detail presentation', () => {
  it('maps roles, levels, and binding statuses', () => {
    expect(getRoleCn('teacher')).toBe('教师')
    expect(getLevelCn('official')).toBe('正式')
    expect(getLevelCn(null)).toBe('—')
    expect(getBindingStatusCn('replaced')).toBe('已替换')
    expect(getBindingTagType('active')).toBe('success')
    expect(getBindingTagType('unbound')).toBe('danger')
  })

  it('keeps the existing date display contract', () => {
    expect(formatTime('2026-07-18T08:09:00')).toBe('2026-07-18 08:09')
    expect(formatDateOnly('2026-07-18T08:09:00')).toBe('2026-07-18')
    expect(formatTime(null)).toBe('—')
    expect(formatDateOnly('invalid')).toBe('invalid')
  })

  it('preserves identity masking and compact display rules', () => {
    expect(getInitials('小明')).toBe('小')
    expect(getInitials('')).toBe('U')
    expect(truncateOpenId('1234567890123456')).toBe('123456...123456')
    expect(maskPhone('13812345678')).toBe('138****5678')
    expect(maskPhone(null)).toBe('—')
    expect(maskCertNo('1234567890')).toBe('12345678****')
  })
})
