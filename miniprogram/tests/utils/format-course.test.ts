import { describe, it, expect } from 'vitest'
import { formatViews } from '@/utils/format-course'

describe('formatViews', () => {
  it('returns plain digits for values below 1000', () => {
    expect(formatViews(128)).toBe('128')
  })

  it('returns plain digits for thousands without k suffix', () => {
    expect(formatViews(9520)).toBe('9520')
  })

  it('returns plain digits for values above 10000 without 万 suffix', () => {
    expect(formatViews(19000)).toBe('19000')
  })

  it('handles zero and invalid values', () => {
    expect(formatViews(0)).toBe('0')
    expect(formatViews(undefined)).toBe('0')
    expect(formatViews(-3)).toBe('0')
  })
})
