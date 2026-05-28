import { describe, expect, it } from 'vitest'
import { validateIdNumber } from './id-number'

describe('id number validator', () => {
  it('accepts valid 18 digit id numbers with checksum', () => {
    expect(validateIdNumber('REDACTED')).toBe('')
  })

  it('rejects malformed or bad checksum id numbers', () => {
    expect(validateIdNumber('123')).toContain('格式')
    expect(validateIdNumber('REDACTED')).toContain('校验位')
  })
})
