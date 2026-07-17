import { describe, expect, it } from 'vitest'
import type { RescueParticipant } from '@/types/rescue-session'
import {
  buildParticipantMarker,
  buildSosMarker,
  buildViewParticipantMarker,
  interpolateGeo,
  PARTICIPANT_MARKER_OFFSET,
  SOS_MARKER_ID,
} from '@/utils/rescue-markers'

function buildParticipant(overrides: Partial<RescueParticipant> = {}): RescueParticipant {
  return {
    openId: 'openid-1',
    displayName: '张急救',
    joinedAt: '2026-07-17T10:00:00.000Z',
    takenAed: false,
    arrived: false,
    signal: 'ok',
    location: { lat: 19.9745, lng: 110.5175, reportedAt: '2026-07-17T10:00:05.000Z' },
    ...overrides,
  }
}

describe('rescue-markers', () => {
  it('builds the SOS marker with the emergency icon', () => {
    const marker = buildSosMarker({ lat: 19.97, lng: 110.51, address: '测试点' })
    expect(marker.id).toBe(SOS_MARKER_ID)
    expect(marker.iconPath).toBe('/static/images/emergency-call.png')
    expect(marker.alpha).toBe(1)
  })

  it('highlights participants carrying AED with the AED icon and larger size', () => {
    const normal = buildParticipantMarker(buildParticipant(), 0)
    const highlighted = buildParticipantMarker(buildParticipant({ takenAed: true }), 1)

    expect(normal?.iconPath).toBe('/static/images/avatar-default.png')
    expect(highlighted?.iconPath).toBe('/static/images/aed-marker-online.png')
    expect(highlighted?.width).toBeGreaterThan(normal?.width ?? 0)
    expect(highlighted?.id).toBe(PARTICIPANT_MARKER_OFFSET + 1)
  })

  it('dims lost signal participants and removes gone or unlocated ones', () => {
    expect(buildParticipantMarker(buildParticipant({ signal: 'lost' }), 0)?.alpha).toBe(0.5)
    expect(buildParticipantMarker(buildParticipant({ signal: 'gone' }), 0)).toBeNull()
    expect(buildParticipantMarker(buildParticipant({ location: null }), 0)).toBeNull()
  })

  it('builds view participant markers in the dedicated id range', () => {
    const marker = buildViewParticipantMarker(
      { displayName: '李急救', takenAed: true, arrived: false, signal: 'ok', location: { lat: 1, lng: 2, reportedAt: '' } },
      3,
    )
    expect(marker?.id).toBe(3003)
    expect(marker?.iconPath).toBe('/static/images/aed-marker-online.png')
  })

  it('interpolates positions linearly and clamps t', () => {
    const from = { lat: 0, lng: 0 }
    const to = { lat: 10, lng: 20 }
    expect(interpolateGeo(from, to, 0)).toEqual({ lat: 0, lng: 0 })
    expect(interpolateGeo(from, to, 0.5)).toEqual({ lat: 5, lng: 10 })
    expect(interpolateGeo(from, to, 1)).toEqual(to)
    expect(interpolateGeo(from, to, 1.5)).toEqual(to)
    expect(interpolateGeo(from, to, -1)).toEqual(from)
  })
})
