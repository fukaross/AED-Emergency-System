import { describe, expect, it } from 'vitest'
import {
  buildParticipantColorMap,
  participantColorFor,
  sessionColorSeedFromHelpId,
} from './rescue-session-color'

describe('rescue session color (admin)', () => {
  it('derives the same seed for the same help id', () => {
    expect(sessionColorSeedFromHelpId('help-1')).toBe(sessionColorSeedFromHelpId('help-1'))
    expect(sessionColorSeedFromHelpId('help-1')).not.toBe(sessionColorSeedFromHelpId('help-2'))
  })

  it('is stable for the same session and open id', () => {
    const seed = sessionColorSeedFromHelpId('help-1')
    expect(participantColorFor(seed, 'openid-a')).toEqual(participantColorFor(seed, 'openid-a'))
  })

  it('builds a map covering all participants', () => {
    const map = buildParticipantColorMap('help-1', [
      { openId: 'openid-a' },
      { openId: 'openid-b' },
    ])
    expect(map.size).toBe(2)
    expect(map.has('openid-a')).toBe(true)
    expect(map.get('openid-b')?.accent).toMatch(/^hsl\(\d{1,3}, 70%, 42%\)$/)
  })
})
