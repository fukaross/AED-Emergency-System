import { describe, expect, it } from 'vitest'
import {
  buildParticipantColorMap,
  createSessionColorSeed,
  participantColorFor,
} from '@/pages-sub/rescue/session-color'

describe('session participant colors', () => {
  it('is stable within the same session seed', () => {
    const first = participantColorFor('session-seed', 'openid-a')
    const second = participantColorFor('session-seed', 'openid-a')
    expect(second).toEqual(first)
  })

  it('changes across different session seeds', () => {
    const sessionA = participantColorFor('session-seed-a', 'openid-a')
    const sessionB = participantColorFor('session-seed-b', 'openid-a')
    expect(sessionB).not.toEqual(sessionA)
  })

  it('builds a map covering every participant', () => {
    const map = buildParticipantColorMap(
      [{ openId: 'openid-a' }, { openId: 'openid-b' }, { openId: 'openid-c' }],
      'session-seed',
    )
    expect(map.size).toBe(3)
    expect(map.has('openid-a')).toBe(true)
    expect(map.has('openid-b')).toBe(true)
    expect(map.has('openid-c')).toBe(true)
  })

  it('produces readable hsl colors', () => {
    const color = participantColorFor('session-seed', 'openid-a')
    expect(color.background).toMatch(/^hsl\(\d{1,3}, 60%, 90%\)$/)
    expect(color.accent).toMatch(/^hsl\(\d{1,3}, 70%, 42%\)$/)
  })

  it('creates a non-empty random seed per session', () => {
    expect(createSessionColorSeed().length).toBeGreaterThan(0)
    expect(createSessionColorSeed()).not.toBe(createSessionColorSeed())
  })
})
