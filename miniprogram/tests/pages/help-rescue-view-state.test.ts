import { describe, expect, it } from 'vitest'
import {
  canShowReportAction,
  canApplyRescueView,
  collectRescueMapPoints,
  isActiveHelpStatus,
  projectRescueViewParticipants,
  shouldClearRescueViewForError,
} from '@/pages-sub/help/rescue-view-state'

describe('help rescue view state', () => {
  it('shows report action only when the backend grants the capability', () => {
    expect(canShowReportAction({ canSubmitReport: true })).toBe(true)
    expect(canShowReportAction({ canSubmitReport: false })).toBe(false)
    expect(canShowReportAction(null)).toBe(false)
  })

  it('rejects an in-flight response after the request closes', () => {
    expect(canApplyRescueView({
      requestRevision: 1,
      currentRevision: 2,
      requestedHelpId: 'help-1',
      currentHelpId: 'help-1',
      helpStatus: 'closed',
    })).toBe(false)
    expect(isActiveHelpStatus('closed')).toBe(false)
    expect(isActiveHelpStatus('responding')).toBe(true)
  })

  it('clears sensitive locations when rescue-view access closes or is revoked', () => {
    expect(shouldClearRescueViewForError({ statusCode: 403 })).toBe(true)
    expect(shouldClearRescueViewForError({ statusCode: 409 })).toBe(true)
    expect(shouldClearRescueViewForError({ statusCode: 401 })).toBe(true)
    expect(shouldClearRescueViewForError({ code: 1002 })).toBe(true)
    expect(shouldClearRescueViewForError({ code: 1018 })).toBe(true)
    expect(shouldClearRescueViewForError({ code: 1017 })).toBe(true)
    expect(shouldClearRescueViewForError(new Error('network'))).toBe(false)
  })

  it('rejects stale responses independently of the current help status', () => {
    expect(canApplyRescueView({
      requestRevision: 1,
      currentRevision: 2,
      requestedHelpId: 'help-1',
      currentHelpId: 'help-1',
      helpStatus: 'responding',
    })).toBe(false)
    expect(canApplyRescueView({
      requestRevision: 2,
      currentRevision: 2,
      requestedHelpId: 'help-old',
      currentHelpId: 'help-new',
      helpStatus: 'responding',
    })).toBe(false)
  })

  it('includes current user, SOS and every visible participant in map bounds', () => {
    expect(collectRescueMapPoints({
      userLocation: { lat: 1, lng: 2 },
      sos: { lat: 3, lng: 4 },
      participants: [
        { participantId: 'p1', displayName: '一', takenAed: false, arrived: false, signal: 'ok', location: { lat: 5, lng: 6, reportedAt: '' } },
        { participantId: 'p2', displayName: '二', takenAed: false, arrived: false, signal: 'gone', location: null },
      ],
    })).toEqual([
      { latitude: 1, longitude: 2 },
      { latitude: 3, longitude: 4 },
      { latitude: 5, longitude: 6 },
    ])
  })

  it('projects custom TTL thresholds against the server-calibrated clock', () => {
    const participants = [{
      participantId: 'p1',
      displayName: '急救员一',
      takenAed: false,
      arrived: false,
      signal: 'ok' as const,
      location: { lat: 5, lng: 6, reportedAt: '2026-07-17T09:59:30.000Z' },
    }]
    const timing = {
      signalLostAfterSeconds: 20,
      locationGoneAfterSeconds: 40,
      locationReportIntervalSeconds: 5,
    }
    const localNow = Date.parse('2026-07-17T12:00:00.000Z')
    const serverClockOffsetMs = -2 * 60 * 60 * 1000

    expect(projectRescueViewParticipants({
      participants,
      timing,
      serverClockOffsetMs,
      localNow,
    })[0]).toMatchObject({ signal: 'lost', location: { lat: 5, lng: 6 } })

    expect(projectRescueViewParticipants({
      participants,
      timing,
      serverClockOffsetMs,
      localNow: localNow + 11_000,
    })[0]).toMatchObject({ signal: 'gone', location: null })
  })
})
