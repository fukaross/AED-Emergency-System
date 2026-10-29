import { describe, expect, it } from 'vitest'
import type { RescueParticipant } from '@/types/rescue-session'
import {
  buildParticipantMarker,
  buildSosMarker,
  buildViewParticipantMarker,
  interpolateGeo,
  interpolateViewParticipants,
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
    location: { lat: 31.2318, lng: 121.4768, reportedAt: '2026-07-17T10:00:05.000Z' },
    ...overrides,
  }
}

describe('rescue-markers', () => {
  it('builds the SOS marker with the emergency icon', () => {
    const marker = buildSosMarker({ lat: 31.23, lng: 121.47, address: '测试点' })
    expect(marker.id).toBe(SOS_MARKER_ID)
    expect(marker.iconPath).toBe('/static/images/emergency-call.png')
    expect(marker.alpha).toBe(1)
  })

  it('labels participants carrying AED without reusing the static AED icon', () => {
    const normal = buildParticipantMarker(buildParticipant(), 0)
    const highlighted = buildParticipantMarker(buildParticipant({ takenAed: true }), 1)

    expect(normal?.iconPath).toBe('/static/images/avatar-default.png')
    expect(highlighted?.iconPath).toBe('/static/images/avatar-default.png')
    expect(highlighted?.callout?.content).toContain('携带 AED')
    expect(highlighted?.width).toBeGreaterThan(normal?.width ?? 0)
    expect(highlighted?.id).toBeGreaterThanOrEqual(PARTICIPANT_MARKER_OFFSET)
    expect(buildParticipantMarker(buildParticipant(), 99)?.id).toBe(normal?.id)
  })

  it('dims lost signal participants and removes gone or unlocated ones', () => {
    expect(buildParticipantMarker(buildParticipant({ signal: 'lost' }), 0)?.alpha).toBe(0.5)
    expect(buildParticipantMarker(buildParticipant({ signal: 'gone' }), 0)).toBeNull()
    expect(buildParticipantMarker(buildParticipant({ location: null }), 0)).toBeNull()
  })

  it('builds view participant markers in the dedicated id range', () => {
    const marker = buildViewParticipantMarker(
      { participantId: 'participant-1', displayName: '李急救', takenAed: true, arrived: false, signal: 'ok', location: { lat: 1, lng: 2, reportedAt: '' } },
      3,
    )
    expect(marker?.id).toBe(buildViewParticipantMarker(
      { participantId: 'participant-1', displayName: '李急救', takenAed: true, arrived: false, signal: 'ok', location: { lat: 1, lng: 2, reportedAt: '' } },
      88,
    )?.id)
    expect(marker?.iconPath).toBe('/static/images/avatar-default.png')
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

  it('interpolates rescue-view participants by stable participant id', () => {
    const current = [{
      participantId: 'participant-1',
      displayName: '李急救',
      takenAed: false,
      arrived: false,
      signal: 'ok' as const,
      location: { lat: 10, lng: 20, reportedAt: 'old' },
    }]
    const target = [{
      ...current[0],
      takenAed: true,
      location: { lat: 12, lng: 24, reportedAt: 'new' },
    }]

    const halfway = interpolateViewParticipants(current, target, 0.5)

    expect(halfway[0].location).toMatchObject({ lat: 11, lng: 22, reportedAt: 'new' })
    expect(halfway[0].takenAed).toBe(true)
  })
})
